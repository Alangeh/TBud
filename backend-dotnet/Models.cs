using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TravelReview.Api;

// ---------- EF Core entities ----------
// String PKs preserve the `usr_xxx` / `c_xxx` / `p_xxx` id convention from the original API.
// Lists are persisted as JSON in nvarchar(max) columns (configured in AppDbContext).

[Index(nameof(email), IsUnique = true)]
public class UserDoc
{
    [Key, MaxLength(40)] public string user_id { get; set; } = "";
    [MaxLength(320)] public string email { get; set; } = "";
    [MaxLength(200)] public string name { get; set; } = "";
    [MaxLength(200)] public string? password_hash { get; set; }
    [MaxLength(1024)] public string? picture { get; set; }
    [MaxLength(2000)] public string bio { get; set; } = "";
    public bool verified { get; set; }
    public int review_count { get; set; }
    public int follower_count { get; set; }
    public int following_count { get; set; }
    public List<string> countries_visited { get; set; } = new();
    public List<string> following { get; set; } = new();
    [MaxLength(20)] public string auth_provider { get; set; } = "email";
    public DateTime created_at { get; set; } = DateTime.UtcNow;
    [MaxLength(40)] public string? kyc_document_type { get; set; }
    public DateTime? kyc_submitted_at { get; set; }
}

[Index(nameof(session_token), IsUnique = true)]
[Index(nameof(user_id))]
public class SessionDoc
{
    [Key, MaxLength(200)] public string session_token { get; set; } = "";
    [MaxLength(40)] public string user_id { get; set; } = "";
    public DateTime expires_at { get; set; }
    public DateTime created_at { get; set; } = DateTime.UtcNow;
}

public class CountryDoc
{
    [Key, MaxLength(40)] public string country_id { get; set; } = "";
    [MaxLength(200)] public string name { get; set; } = "";
    [MaxLength(8)] public string code { get; set; } = "";
    [MaxLength(1000)] public string description { get; set; } = "";
    [MaxLength(1024)] public string image { get; set; } = "";
}

[Index(nameof(country_id))]
public class CityDoc
{
    [Key, MaxLength(40)] public string city_id { get; set; } = "";
    [MaxLength(40)] public string country_id { get; set; } = "";
    [MaxLength(200)] public string name { get; set; } = "";
    [MaxLength(1000)] public string description { get; set; } = "";
    [MaxLength(1024)] public string image { get; set; } = "";
}

[Index(nameof(city_id))]
[Index(nameof(country_id))]
[Index(nameof(category))]
public class PlaceDoc
{
    [Key, MaxLength(40)] public string place_id { get; set; } = "";
    [MaxLength(40)] public string city_id { get; set; } = "";
    [MaxLength(40)] public string country_id { get; set; } = "";
    [MaxLength(200)] public string name { get; set; } = "";
    [MaxLength(40)] public string category { get; set; } = "";
    [MaxLength(2000)] public string description { get; set; } = "";
    [MaxLength(500)] public string address { get; set; } = "";
    public List<string> photos { get; set; } = new();
    public double rating { get; set; }
    public int review_count { get; set; }
    public bool claimed { get; set; }
    public DateTime created_at { get; set; } = DateTime.UtcNow;
}

[Index(nameof(place_id))]
[Index(nameof(user_id))]
public class ReviewDoc
{
    [Key, MaxLength(40)] public string review_id { get; set; } = "";
    [MaxLength(40)] public string place_id { get; set; } = "";
    [MaxLength(40)] public string city_id { get; set; } = "";
    [MaxLength(40)] public string country_id { get; set; } = "";
    [MaxLength(40)] public string user_id { get; set; } = "";
    public int rating { get; set; }
    [MaxLength(2000)] public string text { get; set; } = "";
    public List<string> photos { get; set; } = new();
    public int helpful_count { get; set; }
    public List<string> helpful_voters { get; set; } = new();
    public DateTime created_at { get; set; } = DateTime.UtcNow;
}

// ---------- Request DTOs ----------
public record RegisterIn([Required, EmailAddress] string Email, [Required, MinLength(6)] string Password, [Required] string Name);
public record LoginIn([Required, EmailAddress] string Email, [Required] string Password);
public record GoogleSessionIn([Required] string Session_token);
public record KycIn([Required] string Document_type, [Required] string Image_base64);
public record ReviewIn([Required] string Place_id, [Range(1, 5)] int Rating, [Required, MinLength(1), MaxLength(2000)] string Text, List<string>? Photos);
public record ProfileUpdateIn(string? Name, string? Bio);

// ---------- Public response shape ----------
public class PublicUser
{
    public string user_id { get; set; } = "";
    public string? email { get; set; }
    public string? name { get; set; }
    public string? picture { get; set; }
    public string bio { get; set; } = "";
    public bool verified { get; set; }
    public int review_count { get; set; }
    public int follower_count { get; set; }
    public int following_count { get; set; }
    public List<string> countries_visited { get; set; } = new();
    public string? created_at { get; set; }

    public static PublicUser From(UserDoc u) => new()
    {
        user_id = u.user_id, email = u.email, name = u.name, picture = u.picture,
        bio = u.bio, verified = u.verified, review_count = u.review_count,
        follower_count = u.follower_count, following_count = u.following_count,
        countries_visited = u.countries_visited.ToList(), created_at = u.created_at.ToString("o"),
    };
}
