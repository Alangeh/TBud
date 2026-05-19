using System.ComponentModel.DataAnnotations;
using MongoDB.Bson.Serialization.Attributes;

namespace TravelReview.Api;

// ---------- Mongo entities (BSON ignores extra fields) ----------
public class UserDoc
{
    [BsonIgnoreIfNull] public string? _id { get; set; }
    public string user_id { get; set; } = "";
    public string email { get; set; } = "";
    public string name { get; set; } = "";
    public string? password_hash { get; set; }
    public string? picture { get; set; }
    public string bio { get; set; } = "";
    public bool verified { get; set; }
    public int review_count { get; set; }
    public int follower_count { get; set; }
    public int following_count { get; set; }
    public List<string> countries_visited { get; set; } = new();
    public List<string> following { get; set; } = new();
    public string auth_provider { get; set; } = "email";
    public DateTime created_at { get; set; } = DateTime.UtcNow;
    [BsonIgnoreIfNull] public string? kyc_document_type { get; set; }
    [BsonIgnoreIfNull] public DateTime? kyc_submitted_at { get; set; }
}

public class SessionDoc
{
    [BsonIgnoreIfNull] public string? _id { get; set; }
    public string session_token { get; set; } = "";
    public string user_id { get; set; } = "";
    public DateTime expires_at { get; set; }
    public DateTime created_at { get; set; } = DateTime.UtcNow;
}

public class CountryDoc
{
    [BsonIgnoreIfNull] public string? _id { get; set; }
    public string country_id { get; set; } = "";
    public string name { get; set; } = "";
    public string code { get; set; } = "";
    public string description { get; set; } = "";
    public string image { get; set; } = "";
}

public class CityDoc
{
    [BsonIgnoreIfNull] public string? _id { get; set; }
    public string city_id { get; set; } = "";
    public string country_id { get; set; } = "";
    public string name { get; set; } = "";
    public string description { get; set; } = "";
    public string image { get; set; } = "";
}

public class PlaceDoc
{
    [BsonIgnoreIfNull] public string? _id { get; set; }
    public string place_id { get; set; } = "";
    public string city_id { get; set; } = "";
    public string country_id { get; set; } = "";
    public string name { get; set; } = "";
    public string category { get; set; } = "";
    public string description { get; set; } = "";
    public string address { get; set; } = "";
    public List<string> photos { get; set; } = new();
    public double rating { get; set; }
    public int review_count { get; set; }
    public bool claimed { get; set; }
    public DateTime created_at { get; set; } = DateTime.UtcNow;
}

public class ReviewDoc
{
    [BsonIgnoreIfNull] public string? _id { get; set; }
    public string review_id { get; set; } = "";
    public string place_id { get; set; } = "";
    public string city_id { get; set; } = "";
    public string country_id { get; set; } = "";
    public string user_id { get; set; } = "";
    public int rating { get; set; }
    public string text { get; set; } = "";
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
        countries_visited = u.countries_visited, created_at = u.created_at.ToString("o"),
    };
}
