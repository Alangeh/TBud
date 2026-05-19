using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

namespace TravelReview.Api.Services;

public class AuthService
{
    private readonly MongoContext _db;
    private readonly IConfiguration _cfg;
    private readonly IHttpClientFactory _http;
    public AuthService(MongoContext db, IConfiguration cfg, IHttpClientFactory http) { _db = db; _cfg = cfg; _http = http; }

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

        // Try JWT
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
            return await _db.Users.Find(u => u.user_id == uid).FirstOrDefaultAsync(ct);
        }
        catch { /* fall through */ }

        // Try Emergent session token stored in user_sessions
        var sess = await _db.Sessions.Find(s => s.session_token == token).FirstOrDefaultAsync(ct);
        if (sess is null) return null;
        if (sess.expires_at < DateTime.UtcNow) return null;
        return await _db.Users.Find(u => u.user_id == sess.user_id).FirstOrDefaultAsync(ct);
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

        var user = await _db.Users.Find(u => u.email == email).FirstOrDefaultAsync(ct);
        if (user is null)
        {
            user = new UserDoc {
                user_id = MakeId("usr"), email = email, name = name ?? email.Split('@')[0],
                picture = picture, password_hash = null, auth_provider = "google",
            };
            await _db.Users.InsertOneAsync(user, cancellationToken: ct);
        }

        var filter = Builders<SessionDoc>.Filter.Eq(s => s.session_token, sessionToken);
        var update = Builders<SessionDoc>.Update
            .Set(s => s.session_token, sessionToken)
            .Set(s => s.user_id, user.user_id)
            .Set(s => s.expires_at, DateTime.UtcNow.AddDays(JwtDays))
            .Set(s => s.created_at, DateTime.UtcNow);
        await _db.Sessions.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
        return (sessionToken, user);
    }
}
