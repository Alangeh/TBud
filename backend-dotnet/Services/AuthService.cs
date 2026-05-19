using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace TravelReview.Api.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly IHttpClientFactory _http;
    public AuthService(AppDbContext db, IConfiguration cfg, IHttpClientFactory http) { _db = db; _cfg = cfg; _http = http; }

    private string JwtSecret => Environment.GetEnvironmentVariable("JWT_SECRET") ?? _cfg["Jwt:Secret"]!;
    private int JwtDays => int.Parse(_cfg["Jwt:ExpiresDays"] ?? "7");

    public static string Hash(string pw) => BCrypt.Net.BCrypt.HashPassword(pw);
    public static bool Check(string pw, string? hash) => !string.IsNullOrEmpty(hash) && BCrypt.Net.BCrypt.Verify(pw, hash);
    public static string MakeId(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..(prefix.Length + 13)];

    public string IssueJwt(string userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var token = new JwtSecurityToken(
            claims: new[] { new Claim("user_id", userId) },
            expires: DateTime.UtcNow.AddDays(JwtDays),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<UserDoc?> ResolveAsync(string? authorization, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;
        var token = authorization["Bearer ".Length..].Trim();

        // JWT path
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
            handler.ValidateToken(token, new TokenValidationParameters {
                ValidateIssuer = false, ValidateAudience = false,
                ValidateIssuerSigningKey = true, IssuerSigningKey = key,
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
            }, out var validated);
            var jwt = (JwtSecurityToken)validated;
            var uid = jwt.Claims.First(c => c.Type == "user_id").Value;
            return await _db.Users.FirstOrDefaultAsync(u => u.user_id == uid, ct);
        }
        catch { /* fall through */ }

        // Emergent session_token path
        var sess = await _db.Sessions.FirstOrDefaultAsync(s => s.session_token == token, ct);
        if (sess is null || sess.expires_at < DateTime.UtcNow) return null;
        return await _db.Users.FirstOrDefaultAsync(u => u.user_id == sess.user_id, ct);
    }

    public async Task<(string sessionToken, UserDoc user)?> VerifyGoogleAsync(string sessionId, CancellationToken ct)
    {
        var url = _cfg["Emergent:SessionDataUrl"]!;
        var client = _http.CreateClient();
        client.DefaultRequestHeaders.Add("X-Session-ID", sessionId);
        var res = await client.GetAsync(url, ct);
        if (!res.IsSuccessStatusCode) return null;
        var data = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken: ct);
        if (data is null) return null;
        var email = data.GetValueOrDefault("email")?.ToString()?.ToLowerInvariant();
        var name = data.GetValueOrDefault("name")?.ToString();
        var picture = data.GetValueOrDefault("picture")?.ToString();
        var sessionToken = data.GetValueOrDefault("session_token")?.ToString();
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(sessionToken)) return null;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.email == email, ct);
        if (user is null)
        {
            user = new UserDoc {
                user_id = MakeId("usr"), email = email, name = name ?? email.Split('@')[0],
                picture = picture, password_hash = null, auth_provider = "google",
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
        }

        var existing = await _db.Sessions.FirstOrDefaultAsync(s => s.session_token == sessionToken, ct);
        if (existing is null)
        {
            _db.Sessions.Add(new SessionDoc {
                session_token = sessionToken, user_id = user.user_id,
                expires_at = DateTime.UtcNow.AddDays(JwtDays), created_at = DateTime.UtcNow,
            });
        }
        else
        {
            existing.user_id = user.user_id;
            existing.expires_at = DateTime.UtcNow.AddDays(JwtDays);
        }
        await _db.SaveChangesAsync(ct);
        return (sessionToken, user);
    }
}
