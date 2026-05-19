using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelReview.Api.Services;

namespace TravelReview.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    public AuthController(AppDbContext db, AuthService auth) { _db = db; _auth = auth; }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterIn body, CancellationToken ct)
    {
        var email = body.Email.ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.email == email, ct))
            return BadRequest(new { detail = "Email already registered" });
        var user = new UserDoc {
            user_id = AuthService.MakeId("usr"), email = email, name = body.Name,
            password_hash = AuthService.Hash(body.Password), auth_provider = "email",
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return Ok(new { token = _auth.IssueJwt(user.user_id), user = PublicUser.From(user) });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginIn body, CancellationToken ct)
    {
        var email = body.Email.ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.email == email, ct);
        if (user is null || !AuthService.Check(body.Password, user.password_hash))
            return Unauthorized(new { detail = "Invalid email or password" });
        return Ok(new { token = _auth.IssueJwt(user.user_id), user = PublicUser.From(user) });
    }

    [HttpPost("google/session")]
    public async Task<IActionResult> GoogleSession([FromBody] GoogleSessionIn body, CancellationToken ct)
    {
        var result = await _auth.VerifyGoogleAsync(body.Session_token, ct);
        if (result is null) return Unauthorized(new { detail = "Invalid Google session" });
        return Ok(new { token = result.Value.sessionToken, user = PublicUser.From(result.Value.user) });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me([FromHeader(Name = "Authorization")] string? authorization, CancellationToken ct)
    {
        var user = await _auth.ResolveAsync(authorization, ct);
        if (user is null) return Unauthorized(new { detail = "Invalid token" });
        return Ok(new { user = PublicUser.From(user) });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromHeader(Name = "Authorization")] string? authorization, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authorization["Bearer ".Length..].Trim();
            var sess = await _db.Sessions.FirstOrDefaultAsync(s => s.session_token == token, ct);
            if (sess is not null)
            {
                _db.Sessions.Remove(sess);
                await _db.SaveChangesAsync(ct);
            }
        }
        return Ok(new { ok = true });
    }

    [HttpPost("kyc")]
    public async Task<IActionResult> Kyc([FromBody] KycIn body, [FromHeader(Name = "Authorization")] string? authorization, CancellationToken ct)
    {
        var user = await _auth.ResolveAsync(authorization, ct);
        if (user is null) return Unauthorized(new { detail = "Invalid token" });
        user.verified = true;
        user.kyc_document_type = body.Document_type;
        user.kyc_submitted_at = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { user = PublicUser.From(user) });
    }

    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] ProfileUpdateIn body, [FromHeader(Name = "Authorization")] string? authorization, CancellationToken ct)
    {
        var user = await _auth.ResolveAsync(authorization, ct);
        if (user is null) return Unauthorized(new { detail = "Invalid token" });
        if (!string.IsNullOrEmpty(body.Name)) user.name = body.Name;
        if (body.Bio is not null) user.bio = body.Bio;
        await _db.SaveChangesAsync(ct);
        return Ok(new { user = PublicUser.From(user) });
    }
}
