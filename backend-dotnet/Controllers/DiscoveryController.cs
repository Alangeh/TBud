using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TravelReview.Api.Services;

namespace TravelReview.Api.Controllers;

[ApiController]
[Route("api")]
public class DiscoveryController : ControllerBase
{
    private readonly MongoContext _db;
    public DiscoveryController(MongoContext db) { _db = db; }

    [HttpGet("/")]
    [HttpGet("")]
    public IActionResult Root() => Ok(new { service = "TravelReview", status = "ok" });

    [HttpGet("countries")]
    public async Task<IActionResult> Countries(CancellationToken ct)
    {
        var items = await _db.Countries.Find(FilterDefinition<CountryDoc>.Empty).ToListAsync(ct);
        items.ForEach(i => i._id = null);
        return Ok(new { countries = items });
    }

    [HttpGet("countries/{country_id}/cities")]
    public async Task<IActionResult> Cities(string country_id, CancellationToken ct)
    {
        var items = await _db.Cities.Find(c => c.country_id == country_id).ToListAsync(ct);
        items.ForEach(i => i._id = null);
        return Ok(new { cities = items });
    }

    [HttpGet("cities/{city_id}/places")]
    public async Task<IActionResult> Places(string city_id, [FromQuery] string? category, CancellationToken ct)
    {
        var filter = Builders<PlaceDoc>.Filter.Eq(p => p.city_id, city_id);
        if (!string.IsNullOrEmpty(category)) filter &= Builders<PlaceDoc>.Filter.Eq(p => p.category, category);
        var items = await _db.Places.Find(filter).ToListAsync(ct);
        items.ForEach(i => i._id = null);
        return Ok(new { places = items });
    }

    [HttpGet("places/{place_id}")]
    public async Task<IActionResult> Place(string place_id, CancellationToken ct)
    {
        var p = await _db.Places.Find(x => x.place_id == place_id).FirstOrDefaultAsync(ct);
        if (p is null) return NotFound(new { detail = "Place not found" });
        var city = await _db.Cities.Find(c => c.city_id == p.city_id).FirstOrDefaultAsync(ct);
        var country = string.IsNullOrEmpty(p.country_id) ? null : await _db.Countries.Find(c => c.country_id == p.country_id).FirstOrDefaultAsync(ct);
        p._id = null; if (city is not null) city._id = null; if (country is not null) country._id = null;
        return Ok(new { place = p, city, country });
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(new { places = Array.Empty<object>(), cities = Array.Empty<object>(), countries = Array.Empty<object>() });
        var rx = new MongoDB.Bson.BsonRegularExpression(q.Trim(), "i");
        var places = await _db.Places.Find(Builders<PlaceDoc>.Filter.Regex(p => p.name, rx)).Limit(20).ToListAsync(ct);
        var cities = await _db.Cities.Find(Builders<CityDoc>.Filter.Regex(c => c.name, rx)).Limit(20).ToListAsync(ct);
        var countries = await _db.Countries.Find(Builders<CountryDoc>.Filter.Regex(c => c.name, rx)).Limit(20).ToListAsync(ct);
        places.ForEach(i => i._id = null); cities.ForEach(i => i._id = null); countries.ForEach(i => i._id = null);
        return Ok(new { places, cities, countries });
    }
}
