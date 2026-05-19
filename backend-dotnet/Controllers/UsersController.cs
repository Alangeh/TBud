using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelReview.Api.Services;

namespace TravelReview.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    public UsersController(AppDbContext db, AuthService auth) { _db = db; _auth = auth; }

    [HttpGet("me/reviews")]
    public async Task<IActionResult> MyReviews([FromHeader(Name = "Authorization")] string? authorization, CancellationToken ct)
    {
        var user = await _auth.ResolveAsync(authorization, ct);
        if (user is null) return Unauthorized(new { detail = "Invalid token" });
        var items = await _db.Reviews.AsNoTracking()
            .Where(r => r.user_id == user.user_id)
            .OrderByDescending(r => r.created_at)
            .ToListAsync(ct);
        var pids = items.Select(r => r.place_id).Distinct().ToList();
        var places = pids.Count == 0
            ? new List<PlaceDoc>()
            : await _db.Places.AsNoTracking().Where(p => pids.Contains(p.place_id)).ToListAsync(ct);
        var pmap = places.ToDictionary(p => p.place_id);
        var enriched = items.Select(r => new {
            r.review_id, r.place_id, r.rating, r.text, r.photos,
            created_at = r.created_at.ToString("o"),
            place_name = pmap.GetValueOrDefault(r.place_id)?.name,
            place_image = pmap.GetValueOrDefault(r.place_id)?.photos.FirstOrDefault(),
        });
        return Ok(new { reviews = enriched });
    }

    [HttpGet("{user_id}")]
    public async Task<IActionResult> Get(string user_id, CancellationToken ct)
    {
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.user_id == user_id, ct);
        if (u is null) return NotFound(new { detail = "User not found" });
        return Ok(new { user = PublicUser.From(u) });
    }

    [HttpPost("{user_id}/follow")]
    public async Task<IActionResult> Follow(string user_id, [FromHeader(Name = "Authorization")] string? authorization, CancellationToken ct)
    {
        var me = await _auth.ResolveAsync(authorization, ct);
        if (me is null) return Unauthorized(new { detail = "Invalid token" });
        if (user_id == me.user_id) return BadRequest(new { detail = "Cannot follow yourself" });
        var target = await _db.Users.FirstOrDefaultAsync(u => u.user_id == user_id, ct);
        if (target is null) return NotFound(new { detail = "User not found" });

        var following = me.following.Contains(user_id);
        if (following)
        {
            me.following = me.following.Where(f => f != user_id).ToList();
            me.following_count = Math.Max(0, me.following_count - 1);
            target.follower_count = Math.Max(0, target.follower_count - 1);
        }
        else
        {
            me.following = me.following.Append(user_id).ToList();
            me.following_count += 1;
            target.follower_count += 1;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { following = !following });
    }
}
