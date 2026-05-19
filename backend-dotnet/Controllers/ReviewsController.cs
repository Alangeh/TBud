using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelReview.Api.Services;

namespace TravelReview.Api.Controllers;

[ApiController]
[Route("api")]
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    public ReviewsController(AppDbContext db, AuthService auth) { _db = db; _auth = auth; }

    [HttpGet("places/{place_id}/reviews")]
    public async Task<IActionResult> ForPlace(string place_id, CancellationToken ct)
    {
        var items = await _db.Reviews.AsNoTracking()
            .Where(r => r.place_id == place_id)
            .OrderByDescending(r => r.created_at)
            .ToListAsync(ct);
        var uids = items.Select(r => r.user_id).Distinct().ToList();
        var users = uids.Count == 0
            ? new List<UserDoc>()
            : await _db.Users.AsNoTracking().Where(u => uids.Contains(u.user_id)).ToListAsync(ct);
        var umap = users.ToDictionary(u => u.user_id);
        var enriched = items.Select(r => new {
            r.review_id, r.place_id, r.user_id, r.rating, r.text, r.photos,
            r.helpful_count, created_at = r.created_at.ToString("o"),
            user_name = umap.GetValueOrDefault(r.user_id)?.name ?? "Traveler",
            user_picture = umap.GetValueOrDefault(r.user_id)?.picture,
            user_verified = umap.GetValueOrDefault(r.user_id)?.verified ?? false,
        });
        return Ok(new { reviews = enriched });
    }

    [HttpPost("reviews")]
    public async Task<IActionResult> Create([FromBody] ReviewIn body, [FromHeader(Name = "Authorization")] string? authorization, CancellationToken ct)
    {
        var user = await _auth.ResolveAsync(authorization, ct);
        if (user is null) return Unauthorized(new { detail = "Invalid token" });
        var place = await _db.Places.FirstOrDefaultAsync(p => p.place_id == body.Place_id, ct);
        if (place is null) return NotFound(new { detail = "Place not found" });

        var doc = new ReviewDoc {
            review_id = AuthService.MakeId("rev"),
            place_id = body.Place_id, city_id = place.city_id, country_id = place.country_id,
            user_id = user.user_id, rating = body.Rating, text = body.Text,
            photos = (body.Photos ?? new()).Take(10).ToList(),
        };
        _db.Reviews.Add(doc);

        // Recompute place aggregate (include the new review's rating).
        var existing = await _db.Reviews.Where(r => r.place_id == body.Place_id)
            .Select(r => (int?)r.rating).ToListAsync(ct);
        existing.Add(body.Rating);
        place.rating = existing.Count == 0 ? 0 : Math.Round(existing.Average(x => x!.Value), 2);
        place.review_count = existing.Count;

        // Bump user stats.
        user.review_count += 1;
        if (!string.IsNullOrEmpty(place.country_id) && !user.countries_visited.Contains(place.country_id))
        {
            user.countries_visited = user.countries_visited.Append(place.country_id).ToList();
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new {
            review = new {
                doc.review_id, doc.place_id, doc.user_id, doc.rating, doc.text, doc.photos,
                doc.helpful_count, created_at = doc.created_at.ToString("o"),
                user_name = user.name, user_picture = user.picture, user_verified = user.verified,
            }
        });
    }

    [HttpPost("reviews/{review_id}/helpful")]
    public async Task<IActionResult> Helpful(string review_id, [FromHeader(Name = "Authorization")] string? authorization, CancellationToken ct)
    {
        var user = await _auth.ResolveAsync(authorization, ct);
        if (user is null) return Unauthorized(new { detail = "Invalid token" });
        var rev = await _db.Reviews.FirstOrDefaultAsync(r => r.review_id == review_id, ct);
        if (rev is null) return NotFound(new { detail = "Review not found" });
        bool voted;
        if (rev.helpful_voters.Contains(user.user_id))
        {
            rev.helpful_voters = rev.helpful_voters.Where(v => v != user.user_id).ToList();
            rev.helpful_count = Math.Max(0, rev.helpful_count - 1);
            voted = false;
        }
        else
        {
            rev.helpful_voters = rev.helpful_voters.Append(user.user_id).ToList();
            rev.helpful_count += 1;
            voted = true;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { helpful_count = rev.helpful_count, voted });
    }
}
