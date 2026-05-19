using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TravelReview.Api.Tests;

/// <summary>
/// 16 endpoint tests mirroring the FastAPI pytest suite that passed for the Python backend.
/// Requires a local MongoDB reachable at MONGO_URL (default: mongodb://localhost:27017).
/// </summary>
public class EndpointTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _c;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public EndpointTests(ApiFactory f) { _c = f.CreateClient(); }

    private static string FreshEmail() => $"test_{Guid.NewGuid():N}@travelreview.app";

    private async Task<(string token, string userId)> RegisterAsync(string? email = null)
    {
        var res = await _c.PostAsJsonAsync("/api/auth/register",
            new { email = email ?? FreshEmail(), password = "demo1234", name = "Tester" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("token").GetString()!, body.GetProperty("user").GetProperty("user_id").GetString()!);
    }

    private void SetAuth(string? token)
    {
        _c.DefaultRequestHeaders.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    private static void AssertNoIdLeak(JsonElement root)
    {
        // Recursively walk the tree and assert no property named "_id".
        void Walk(JsonElement el)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var p in el.EnumerateObject())
                    {
                        Assert.NotEqual("_id", p.Name);
                        Walk(p.Value);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in el.EnumerateArray()) Walk(item);
                    break;
            }
        }
        Walk(root);
    }

    // ---------- 1. countries ----------
    [Fact]
    public async Task T01_Countries_Returns_Five_Seeded()
    {
        var body = await _c.GetFromJsonAsync<JsonElement>("/api/countries");
        Assert.Equal(5, body.GetProperty("countries").GetArrayLength());
        AssertNoIdLeak(body);
    }

    // ---------- 2. Italy cities ----------
    [Fact]
    public async Task T02_ItalyCities_Returns_Three()
    {
        var body = await _c.GetFromJsonAsync<JsonElement>("/api/countries/c_italy/cities");
        Assert.Equal(3, body.GetProperty("cities").GetArrayLength());
    }

    // ---------- 3. Rome places + restaurant filter ----------
    [Fact]
    public async Task T03_RomePlaces_And_RestaurantFilter()
    {
        var all = await _c.GetFromJsonAsync<JsonElement>("/api/cities/ct_rome/places");
        Assert.True(all.GetProperty("places").GetArrayLength() >= 2);
        var filtered = await _c.GetFromJsonAsync<JsonElement>("/api/cities/ct_rome/places?category=restaurant");
        foreach (var p in filtered.GetProperty("places").EnumerateArray())
            Assert.Equal("restaurant", p.GetProperty("category").GetString());
    }

    // ---------- 4. Place detail with city + country ----------
    [Fact]
    public async Task T04_PlaceDetail_Includes_City_And_Country()
    {
        var body = await _c.GetFromJsonAsync<JsonElement>("/api/places/p_colosseum");
        Assert.Equal("Colosseum", body.GetProperty("place").GetProperty("name").GetString());
        Assert.Equal("Rome", body.GetProperty("city").GetProperty("name").GetString());
        Assert.Equal("Italy", body.GetProperty("country").GetProperty("name").GetString());
    }

    // ---------- 5. Search ----------
    [Fact]
    public async Task T05_Search_Tokyo_Returns_Matches()
    {
        var body = await _c.GetFromJsonAsync<JsonElement>("/api/search?q=tokyo");
        var total = body.GetProperty("countries").GetArrayLength()
                  + body.GetProperty("cities").GetArrayLength()
                  + body.GetProperty("places").GetArrayLength();
        Assert.True(total >= 1);
    }

    // ---------- 6. Register new + duplicate 400 ----------
    [Fact]
    public async Task T06_Register_Duplicate_Returns_400()
    {
        var email = FreshEmail();
        var first = await _c.PostAsJsonAsync("/api/auth/register", new { email, password = "demo1234", name = "A" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await _c.PostAsJsonAsync("/api/auth/register", new { email, password = "demo1234", name = "B" });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    // ---------- 7. Login 200 + 401 ----------
    [Fact]
    public async Task T07_Login_Success_And_WrongPassword()
    {
        var email = FreshEmail();
        await _c.PostAsJsonAsync("/api/auth/register", new { email, password = "demo1234", name = "X" });

        var ok = await _c.PostAsJsonAsync("/api/auth/login", new { email, password = "demo1234" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var bad = await _c.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
    }

    // ---------- 8. /auth/me with and without Bearer ----------
    [Fact]
    public async Task T08_Me_Requires_Bearer()
    {
        var (token, _) = await RegisterAsync();
        SetAuth(null);
        var unauth = await _c.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        SetAuth(token);
        var ok = await _c.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        SetAuth(null);
    }

    // ---------- 9. KYC flips verified=true ----------
    [Fact]
    public async Task T09_Kyc_Flips_Verified_True()
    {
        var (token, _) = await RegisterAsync();
        SetAuth(token);
        var res = await _c.PostAsJsonAsync("/api/auth/kyc",
            new { document_type = "passport", image_base64 = "data:image/jpeg;base64,xxx" });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("user").GetProperty("verified").GetBoolean());
        SetAuth(null);
    }

    // ---------- 10. Google session with bogus token ----------
    [Fact]
    public async Task T10_GoogleSession_BogusToken_Returns_401()
    {
        var res = await _c.PostAsJsonAsync("/api/auth/google/session", new { session_token = "obviously-bogus-token" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ---------- 11. POST /reviews updates place + user stats ----------
    [Fact]
    public async Task T11_CreateReview_Updates_Aggregates()
    {
        var (token, userId) = await RegisterAsync();
        SetAuth(token);
        var beforePlace = await _c.GetFromJsonAsync<JsonElement>("/api/places/p_eiffel");
        var beforeCount = beforePlace.GetProperty("place").GetProperty("review_count").GetInt32();

        var res = await _c.PostAsJsonAsync("/api/reviews", new {
            place_id = "p_eiffel", rating = 5, text = "Stunning at sunset.", photos = new string[0]
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var afterPlace = await _c.GetFromJsonAsync<JsonElement>("/api/places/p_eiffel");
        Assert.Equal(beforeCount + 1, afterPlace.GetProperty("place").GetProperty("review_count").GetInt32());

        var me = await _c.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(1, me.GetProperty("user").GetProperty("review_count").GetInt32());
        var countries = me.GetProperty("user").GetProperty("countries_visited");
        Assert.Contains("c_france", countries.EnumerateArray().Select(x => x.GetString()));
        SetAuth(null);
    }

    // ---------- 12. /places/{id}/reviews enriched ----------
    [Fact]
    public async Task T12_PlaceReviews_Are_Enriched_With_User()
    {
        var (token, _) = await RegisterAsync();
        SetAuth(token);
        await _c.PostAsJsonAsync("/api/reviews", new {
            place_id = "p_uffizi", rating = 4, text = "Crowds, but worth it.", photos = new string[0]
        });
        SetAuth(null);

        var body = await _c.GetFromJsonAsync<JsonElement>("/api/places/p_uffizi/reviews");
        var first = body.GetProperty("reviews").EnumerateArray().First();
        Assert.Equal("Tester", first.GetProperty("user_name").GetString());
        Assert.False(first.GetProperty("user_verified").GetBoolean());
    }

    // ---------- 13. Helpful toggles ----------
    [Fact]
    public async Task T13_Helpful_Toggles_Zero_To_One_And_Back()
    {
        var (authorToken, _) = await RegisterAsync();
        SetAuth(authorToken);
        var created = await _c.PostAsJsonAsync("/api/reviews", new {
            place_id = "p_senso_ji", rating = 5, text = "Beautiful temple.", photos = new string[0]
        });
        var revBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var rid = revBody.GetProperty("review").GetProperty("review_id").GetString()!;
        SetAuth(null);

        var (voterToken, _) = await RegisterAsync();
        SetAuth(voterToken);
        var first = await _c.PostAsync($"/api/reviews/{rid}/helpful", null);
        var f = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, f.GetProperty("helpful_count").GetInt32());
        Assert.True(f.GetProperty("voted").GetBoolean());

        var second = await _c.PostAsync($"/api/reviews/{rid}/helpful", null);
        var s = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, s.GetProperty("helpful_count").GetInt32());
        Assert.False(s.GetProperty("voted").GetBoolean());
        SetAuth(null);
    }

    // ---------- 14. /users/me/reviews enriched ----------
    [Fact]
    public async Task T14_MyReviews_Enriched_With_Place()
    {
        var (token, _) = await RegisterAsync();
        SetAuth(token);
        await _c.PostAsJsonAsync("/api/reviews", new {
            place_id = "p_fushimi", rating = 5, text = "Endless torii.", photos = new string[0]
        });
        var body = await _c.GetFromJsonAsync<JsonElement>("/api/users/me/reviews");
        var first = body.GetProperty("reviews").EnumerateArray().First();
        Assert.Equal("Fushimi Inari", first.GetProperty("place_name").GetString());
        Assert.False(string.IsNullOrEmpty(first.GetProperty("place_image").GetString()));
        SetAuth(null);
    }

    // ---------- 15. Logout returns ok:true ----------
    [Fact]
    public async Task T15_Logout_Returns_Ok_True()
    {
        var (token, _) = await RegisterAsync();
        SetAuth(token);
        var res = await _c.PostAsync("/api/auth/logout", null);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("ok").GetBoolean());
        SetAuth(null);
    }

    // ---------- 16. No _id leak ----------
    [Fact]
    public async Task T16_No_Id_Leak_In_Any_Response()
    {
        var (token, _) = await RegisterAsync();
        SetAuth(token);
        foreach (var path in new[] {
            "/api/countries", "/api/countries/c_italy/cities", "/api/cities/ct_rome/places",
            "/api/places/p_colosseum", "/api/search?q=tokyo", "/api/auth/me", "/api/users/me/reviews",
        })
        {
            var body = await _c.GetFromJsonAsync<JsonElement>(path);
            AssertNoIdLeak(body);
        }
        SetAuth(null);
    }
}
