using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace TravelReview.Api.Services;

/// <summary>
/// Dynamic data hydration from public REST APIs (mirrors backend/hydration.py).
///
/// Strategy:
///   1) Pull top-120 countries by population from REST Countries v3.1 (keyless).
///   2) For each new country, always add the CAPITAL as a city (from REST
///      Countries data, guaranteed).
///   3) If RAPIDAPI_KEY is configured AND subscription is active, augment
///      with up to 2 extra major cities per country from GeoDB Cities.
///   4) Best-effort: enrich each city with a Wikipedia REST API thumbnail.
///
/// Idempotent: state tracked in HydrationStateDoc so re-runs on restart
/// are no-ops. Force a re-sync via POST /api/admin/refresh-data.
/// </summary>
public class HydrationService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<HydrationService> _log;

    public HydrationService(AppDbContext db, IConfiguration cfg, IHttpClientFactory http, ILogger<HydrationService> log)
    { _db = db; _cfg = cfg; _http = http; _log = log; }

    private const string RestCountriesUrl =
        "https://restcountries.com/v3.1/all?fields=name,cca2,cca3,capital,region,subregion,population,flags";
    private const string GeoDbBase = "https://wft-geo-db.p.rapidapi.com/v1/geo";
    private const string GeoDbHost = "wft-geo-db.p.rapidapi.com";
    private const int TopNCountries = 120;
    private const int CitiesPerCountry = 3;
    private static readonly TimeSpan GeoDbSpacing = TimeSpan.FromMilliseconds(1200);
    private static readonly string[] CuratedCodes = { "IT", "JP", "FR", "TH", "PE" };
    private const string WikiUa = "TravelReviewApp/1.0 (https://github.com/example/travelreview; contact@example.com)";

    public record Summary(bool Ok, int CountriesInserted, int CitiesInserted, List<string> Errors, double DurationSeconds, bool Skipped = false, string? Reason = null);

    public async Task<Summary> RunAsync(bool force = false, CancellationToken ct = default)
    {
        var state = await _db.HydrationState.FirstOrDefaultAsync(s => s.key == "v1", ct);
        if (state is { status: "completed" } && !force)
            return new Summary(true, 0, 0, new(), 0, true, "already_completed");
        if (state is { status: "running" } && !force)
            return new Summary(true, 0, 0, new(), 0, true, "already_running");

        var apiKey = (Environment.GetEnvironmentVariable("RAPIDAPI_KEY") ?? _cfg["Hydration:RapidApiKey"] ?? "").Trim();
        var startedAt = DateTime.UtcNow;
        if (state is null)
        {
            state = new HydrationStateDoc { key = "v1", status = "running", started_at = startedAt };
            _db.HydrationState.Add(state);
        }
        else
        {
            state.status = "running";
            state.started_at = startedAt;
            state.error = null;
            state.failed_at = null;
        }
        await _db.SaveChangesAsync(ct);

        var errors = new List<string>();
        int countriesInserted = 0, citiesInserted = 0;
        var http = _http.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(30);

        try
        {
            // 1) Countries -------------------------------------------------
            List<RestCountry> top;
            try
            {
                var resp = await http.GetAsync(RestCountriesUrl, ct);
                resp.EnsureSuccessStatusCode();
                var all = await resp.Content.ReadFromJsonAsync<List<RestCountry>>(cancellationToken: ct) ?? new();
                top = all.OrderByDescending(c => c.population ?? 0).Take(TopNCountries).ToList();
                _log.LogInformation("REST Countries: got {Total}, taking top {N}", all.Count, top.Count);
            }
            catch (Exception e)
            {
                _log.LogError(e, "REST Countries fetch failed");
                errors.Add($"rest_countries: {e.Message}");
                top = new();
            }

            var existingCodes = await _db.Countries.Select(c => c.code).ToListAsync(ct);
            var existingSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);
            var countryMeta = new Dictionary<string, RestCountry>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in top)
            {
                var code = (c.cca2 ?? "").ToUpperInvariant();
                var name = c.name?.common;
                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name)) continue;
                countryMeta[code] = c;
                if (existingSet.Contains(code)) continue;
                _db.Countries.Add(new CountryDoc
                {
                    country_id = $"c_{code.ToLowerInvariant()}",
                    name = name,
                    code = code,
                    description = CountryDescription(c),
                    image = c.flags?.png ?? c.flags?.svg ?? "",
                });
                countriesInserted++;
            }
            if (countriesInserted > 0) await _db.SaveChangesAsync(ct);
            _log.LogInformation("Inserted {N} new countries", countriesInserted);

            // 2) Cities ----------------------------------------------------
            var allCountries = await _db.Countries.AsNoTracking().ToListAsync(ct);
            // Sort by population (using REST meta when available)
            allCountries = allCountries.OrderByDescending(c => countryMeta.TryGetValue(c.code, out var m) ? (m.population ?? 0) : 0).ToList();

            string? geoDbDisabledReason = string.IsNullOrEmpty(apiKey) ? "RAPIDAPI_KEY not set" : null;
            int consecutiveFailures = 0;

            foreach (var country in allCountries)
            {
                if (ct.IsCancellationRequested) break;
                var code = country.code;
                if (string.IsNullOrEmpty(code)) continue;
                var hasCities = await _db.Cities.AnyAsync(c => c.country_id == country.country_id, ct);
                if (hasCities) continue;

                var docs = new List<CityDoc>();
                var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 2a) Capital fallback
                string? capital = null;
                if (countryMeta.TryGetValue(code, out var meta))
                    capital = meta.capital?.FirstOrDefault();
                if (!string.IsNullOrEmpty(capital))
                {
                    docs.Add(new CityDoc
                    {
                        city_id = $"ct_cap_{code.ToLowerInvariant()}",
                        country_id = country.country_id,
                        name = capital,
                        description = $"Capital of {country.name}",
                        image = "",
                    });
                    existingNames.Add(capital);
                }

                // 2b) GeoDB augmentation
                if (!string.IsNullOrEmpty(apiKey) && geoDbDisabledReason is null)
                {
                    var (cities, err) = await FetchGeoDbCitiesAsync(http, code, apiKey, CitiesPerCountry, ct);
                    if (err == "subscription")
                    {
                        geoDbDisabledReason = "GeoDB returned 403 'Not subscribed' — go to https://rapidapi.com/wirefreethought/api/geodb-cities/ and click 'Subscribe to Test' (Basic / free plan).";
                        errors.Add(geoDbDisabledReason);
                        _log.LogWarning(geoDbDisabledReason);
                    }
                    else if (err == "rate_limit")
                    {
                        consecutiveFailures++;
                        if (consecutiveFailures >= 5)
                        {
                            geoDbDisabledReason = "GeoDB rate limit hit 5x in a row — stopping.";
                            errors.Add(geoDbDisabledReason);
                            _log.LogWarning(geoDbDisabledReason);
                        }
                    }
                    else if (err is null)
                    {
                        consecutiveFailures = 0;
                        foreach (var city in cities)
                        {
                            var raw = city.id?.ToString() ?? city.wikiDataId ?? "";
                            var cname = city.name ?? city.city ?? "";
                            if (string.IsNullOrEmpty(raw) || string.IsNullOrEmpty(cname)) continue;
                            if (existingNames.Contains(cname)) continue;
                            existingNames.Add(cname);
                            docs.Add(new CityDoc
                            {
                                city_id = $"ct_geo_{raw}",
                                country_id = country.country_id,
                                name = cname,
                                description = (city.region ?? "") + (city.population.HasValue ? $" · pop. {city.population.Value:N0}" : ""),
                                image = "",
                            });
                        }
                    }
                }

                // 2c) Wikipedia thumbnail enrichment (best effort)
                foreach (var d in docs)
                {
                    var thumb = await WikiThumbnailAsync(http, $"{d.name}, {country.name}", ct)
                             ?? await WikiThumbnailAsync(http, d.name, ct);
                    if (!string.IsNullOrEmpty(thumb)) d.image = thumb;
                }

                if (docs.Count > 0)
                {
                    _db.Cities.AddRange(docs);
                    try { await _db.SaveChangesAsync(ct); citiesInserted += docs.Count; }
                    catch (Exception e) { errors.Add($"insert_cities {code}: {e.Message}"); }
                }

                if (!string.IsNullOrEmpty(apiKey) && geoDbDisabledReason is null)
                    await Task.Delay(GeoDbSpacing, ct);
            }

            var completedAt = DateTime.UtcNow;
            state.status = "completed";
            state.completed_at = completedAt;
            state.countries_inserted = countriesInserted;
            state.cities_inserted = citiesInserted;
            state.errors_json = JsonSerializer.Serialize(errors.Take(50));
            state.duration_seconds = (completedAt - startedAt).TotalSeconds;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Hydration done in {Sec}s: +{C} countries, +{Ci} cities, {E} errors",
                (int)state.duration_seconds, countriesInserted, citiesInserted, errors.Count);

            return new Summary(true, countriesInserted, citiesInserted, errors.Take(20).ToList(), state.duration_seconds);
        }
        catch (Exception e)
        {
            _log.LogError(e, "Hydration failed");
            state.status = "failed";
            state.error = e.Message;
            state.failed_at = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    public async Task<object> GetStatusAsync(CancellationToken ct = default)
    {
        var state = await _db.HydrationState.AsNoTracking().FirstOrDefaultAsync(s => s.key == "v1", ct);
        return new
        {
            state = state is null ? null : new
            {
                state.status,
                started_at = state.started_at?.ToString("o"),
                completed_at = state.completed_at?.ToString("o"),
                state.countries_inserted,
                state.cities_inserted,
                state.duration_seconds,
                errors = string.IsNullOrEmpty(state.errors_json) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(state.errors_json),
                state.error,
            },
            counts = new
            {
                countries = await _db.Countries.CountAsync(ct),
                cities = await _db.Cities.CountAsync(ct),
                places = await _db.Places.CountAsync(ct),
            }
        };
    }

    // --- HTTP helpers ---------------------------------------------------------
    private static string CountryDescription(RestCountry c)
    {
        var parts = new List<string>();
        var sub = c.subregion ?? c.region;
        if (!string.IsNullOrEmpty(sub)) parts.Add(sub);
        var cap = c.capital?.FirstOrDefault();
        if (!string.IsNullOrEmpty(cap)) parts.Add($"capital · {cap}");
        if (c.population is > 0) parts.Add($"pop. {c.population:N0}");
        return parts.Count > 0 ? string.Join(" · ", parts) : "Discover this country.";
    }

    private async Task<(List<GeoDbCity> cities, string? errorKind)> FetchGeoDbCitiesAsync(
        HttpClient http, string countryCode, string apiKey, int limit, CancellationToken ct)
    {
        var url = $"{GeoDbBase}/countries/{countryCode}/places?types=CITY&limit={limit}&sort=-population&minPopulation=10000";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-RapidAPI-Key", apiKey);
        req.Headers.Add("X-RapidAPI-Host", GeoDbHost);
        try
        {
            var resp = await http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (resp.IsSuccessStatusCode)
            {
                var parsed = JsonSerializer.Deserialize<GeoDbResponse>(body) ?? new();
                return (parsed.data ?? new(), null);
            }
            if ((int)resp.StatusCode == 403 || body.Contains("not subscribed", StringComparison.OrdinalIgnoreCase))
                return (new(), "subscription");
            if ((int)resp.StatusCode == 429) return (new(), "rate_limit");
            _log.LogWarning("GeoDB {Code} -> {Status}: {Body}", countryCode, (int)resp.StatusCode, body.Substring(0, Math.Min(200, body.Length)));
            return (new(), "other");
        }
        catch (Exception e)
        {
            _log.LogWarning("GeoDB network error for {Code}: {Msg}", countryCode, e.Message);
            return (new(), "other");
        }
    }

    private async Task<string?> WikiThumbnailAsync(HttpClient http, string title, CancellationToken ct)
    {
        try
        {
            var encoded = Uri.EscapeDataString(title.Replace(' ', '_'));
            var req = new HttpRequestMessage(HttpMethod.Get, $"https://en.wikipedia.org/api/rest_v1/page/summary/{encoded}");
            req.Headers.Add("User-Agent", WikiUa);
            req.Headers.Add("Accept", "application/json");
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var data = await resp.Content.ReadFromJsonAsync<WikiSummary>(cancellationToken: ct);
            return data?.thumbnail?.source;
        }
        catch { return null; }
    }

    // --- DTOs for external APIs ----------------------------------------------
    private class RestCountry
    {
        public RestCountryName? name { get; set; }
        public string? cca2 { get; set; }
        public string? cca3 { get; set; }
        public List<string>? capital { get; set; }
        public string? region { get; set; }
        public string? subregion { get; set; }
        public long? population { get; set; }
        public RestCountryFlags? flags { get; set; }
    }
    private class RestCountryName { public string? common { get; set; } public string? official { get; set; } }
    private class RestCountryFlags { public string? png { get; set; } public string? svg { get; set; } }

    private class GeoDbResponse { public List<GeoDbCity>? data { get; set; } }
    private class GeoDbCity
    {
        [JsonPropertyName("id")] public object? id { get; set; }
        public string? wikiDataId { get; set; }
        public string? name { get; set; }
        public string? city { get; set; }
        public string? region { get; set; }
        public long? population { get; set; }
        public double? latitude { get; set; }
        public double? longitude { get; set; }
    }

    private class WikiSummary { public WikiThumbnail? thumbnail { get; set; } }
    private class WikiThumbnail { public string? source { get; set; } }
}
