using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TravelReview.Api.Services;

namespace TravelReview.Api.Controllers;

[ApiController]
[Route("api")]
public class ReviewsController : ControllerBase
{
    private readonly MongoContext _db;
    private readonly AuthService _auth;
    public ReviewsController(MongoContext db, AuthService auth) { _db = db; _auth = auth; }

    [HttpGet("places/{place_id}/reviews")]
    public async Task<IActionResult> ForPlace(string place_id, CancellationToken ct)
    {
        var items = await _db.Reviews.Find(r => r.place_id == place_id)
            .SortByDescending(r => r.created_at).ToListAsync(ct);
        var uids = items.Select(r => r.user_id).Distinct().ToList();
        var users = uids.Count == 0 ? new List<UserDoc>()
            : await _db.Users.Find(u => uids.Contains(u.user_id)).ToListAsync(ct);
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
        var place = await _db.Places.Find(p => p.place_id == body.Place_id).FirstOrDefaultAsync(ct);
        if (place is null) return NotFound(new { detail = "Place not found" });

        var doc = new ReviewDoc {
            review_id = AuthService.MakeId("rev"),
            place_id = body.Place_id, city_id = place.city_id, country_id = place.country_id,
            user_id = user.user_id, rating = body.Rating, text = body.Text,
            photos = (body.Photos ?? new()).Take(10).ToList(),
        };
        await _db.Reviews.InsertOneAsync(doc, cancellationToken: ct);

        // Recompute place aggregate
        var all = await _db.Reviews.Find(r => r.place_id == body.Place_id).ToListAsync(ct);
        var avg = all.Count == 0 ? 0 : Math.Round(all.Average(r => r.rating), 2);
        await _db.Places.UpdateOneAsync(p => p.place_id == body.Place_id,
            Builders<PlaceDoc>.Update.Set(p => p.rating, avg).Set(p => p.review_count, all.Count), cancellationToken: ct);

        // Bump user stats
        var userUpdate = Builders<UserDoc>.Update.Inc(u => u.review_count, 1);
        if (!string.IsNullOrEmpty(place.country_id) && !user.countries_visited.Contains(place.country_id))
            userUpdate = userUpdate.AddToSet(u => u.countries_visited, place.country_id);
        await _db.Users.UpdateOneAsync(u => u.user_id == user.user_id, userUpdate, cancellationToken: ct);

        doc._id = null;
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
        var rev = await _db.Reviews.Find(r => r.review_id == review_id).FirstOrDefaultAsync(ct);
        if (rev is null) return NotFound(new { detail = "Review not found" });
        bool voted;
        if (rev.helpful_voters.Contains(user.user_id))
        {
            await _db.Reviews.UpdateOneAsync(r => r.review_id == review_id,
                Builders<ReviewDoc>.Update.Inc(r => r.helpful_count, -1).Pull(r => r.helpful_voters, user.user_id), cancellationToken: ct);
            voted = false;
        }
        else
        {
            await _db.Reviews.UpdateOneAsync(r => r.review_id == review_id,
                Builders<ReviewDoc>.Update.Inc(r => r.helpful_count, 1).AddToSet(r => r.helpful_voters, user.user_id), cancellationToken: ct);
            voted = true;
        }
        var fresh = await _db.Reviews.Find(r => r.review_id == review_id).FirstAsync(ct);
        return Ok(new { helpful_count = fresh.helpful_count, voted });
    }
}
