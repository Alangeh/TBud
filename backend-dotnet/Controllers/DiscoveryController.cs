using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelReview.Api.Services;

namespace TravelReview.Api.Controllers;

[ApiController]
[Route("api")]
public class DiscoveryController : ControllerBase
{
    private readonly AppDbContext _db;
    public DiscoveryController(AppDbContext db) { _db = db; }

    [HttpGet("/")]
    [HttpGet("")]
    public IActionResult Root() => Ok(new { service = "TravelReview", status = "ok" });

    [HttpGet("countries")]
    public async Task<IActionResult> Countries(CancellationToken ct)
    {
        var items = await _db.Countries.AsNoTracking().ToListAsync(ct);
        return Ok(new { countries = items });
    }

    [HttpGet("countries/{country_id}/cities")]
    public async Task<IActionResult> Cities(string country_id, CancellationToken ct)
    {
        var items = await _db.Cities.AsNoTracking().Where(c => c.country_id == country_id).ToListAsync(ct);
        return Ok(new { cities = items });
    }

    [HttpGet("cities/{city_id}/places")]
    public async Task<IActionResult> Places(string city_id, [FromQuery] string? category, CancellationToken ct)
    {
        var q = _db.Places.AsNoTracking().Where(p => p.city_id == city_id);
        if (!string.IsNullOrEmpty(category)) q = q.Where(p => p.category == category);
        var items = await q.ToListAsync(ct);
        return Ok(new { places = items });
    }

    [HttpGet("places/{place_id}")]
    public async Task<IActionResult> Place(string place_id, CancellationToken ct)
    {
        var p = await _db.Places.AsNoTracking().FirstOrDefaultAsync(x => x.place_id == place_id, ct);
        if (p is null) return NotFound(new { detail = "Place not found" });
        var city = await _db.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.city_id == p.city_id, ct);
        var country = string.IsNullOrEmpty(p.country_id)
            ? null
            : await _db.Countries.AsNoTracking().FirstOrDefaultAsync(c => c.country_id == p.country_id, ct);
        return Ok(new { place = p, city, country });
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(new { places = Array.Empty<object>(), cities = Array.Empty<object>(), countries = Array.Empty<object>() });
        var term = $"%{q.Trim()}%";
        var places = await _db.Places.AsNoTracking().Where(p => EF.Functions.Like(p.name, term)).Take(20).ToListAsync(ct);
        var cities = await _db.Cities.AsNoTracking().Where(c => EF.Functions.Like(c.name, term)).Take(20).ToListAsync(ct);
        var countries = await _db.Countries.AsNoTracking().Where(c => EF.Functions.Like(c.name, term)).Take(20).ToListAsync(ct);
        return Ok(new { places, cities, countries });
    }
}
