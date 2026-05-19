using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TravelReview.Api.Services;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserDoc> Users => Set<UserDoc>();
    public DbSet<SessionDoc> Sessions => Set<SessionDoc>();
    public DbSet<CountryDoc> Countries => Set<CountryDoc>();
    public DbSet<CityDoc> Cities => Set<CityDoc>();
    public DbSet<PlaceDoc> Places => Set<PlaceDoc>();
    public DbSet<ReviewDoc> Reviews => Set<ReviewDoc>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Persist List<string> properties as JSON in nvarchar(max).
        var listConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v ?? new(), (JsonSerializerOptions?)null),
            v => string.IsNullOrEmpty(v)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        var listComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
            (a, c) => (a == null && c == null) || (a != null && c != null && a.SequenceEqual(c)),
            v => v == null ? 0 : v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v == null ? new List<string>() : v.ToList());

        b.Entity<UserDoc>(e =>
        {
            e.ToTable("Users");
            e.Property(x => x.countries_visited).HasConversion(listConverter, listComparer).HasColumnType("nvarchar(max)");
            e.Property(x => x.following).HasConversion(listConverter, listComparer).HasColumnType("nvarchar(max)");
        });

        b.Entity<SessionDoc>().ToTable("Sessions");
        b.Entity<CountryDoc>().ToTable("Countries");
        b.Entity<CityDoc>().ToTable("Cities");

        b.Entity<PlaceDoc>(e =>
        {
            e.ToTable("Places");
            e.Property(x => x.photos).HasConversion(listConverter, listComparer).HasColumnType("nvarchar(max)");
        });

        b.Entity<ReviewDoc>(e =>
        {
            e.ToTable("Reviews");
            e.Property(x => x.photos).HasConversion(listConverter, listComparer).HasColumnType("nvarchar(max)");
            e.Property(x => x.helpful_voters).HasConversion(listConverter, listComparer).HasColumnType("nvarchar(max)");
        });
    }
}
