using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace TravelReview.Api.Services;

public class MongoContext
{
    public IMongoDatabase Db { get; }
    public IMongoCollection<UserDoc> Users => Db.GetCollection<UserDoc>("users");
    public IMongoCollection<SessionDoc> Sessions => Db.GetCollection<SessionDoc>("user_sessions");
    public IMongoCollection<CountryDoc> Countries => Db.GetCollection<CountryDoc>("countries");
    public IMongoCollection<CityDoc> Cities => Db.GetCollection<CityDoc>("cities");
    public IMongoCollection<PlaceDoc> Places => Db.GetCollection<PlaceDoc>("places");
    public IMongoCollection<ReviewDoc> Reviews => Db.GetCollection<ReviewDoc>("reviews");

    static MongoContext()
    {
        // Register the IgnoreExtraElements convention once per process.
        var pack = new ConventionPack { new IgnoreExtraElementsConvention(true) };
        ConventionRegistry.Register("IgnoreExtras", pack, _ => true);
    }

    public MongoContext(IConfiguration cfg)
    {
        var url = Environment.GetEnvironmentVariable("MONGO_URL") ?? cfg["Mongo:Url"]!;
        var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? cfg["Mongo:Database"]!;
        Db = new MongoClient(url).GetDatabase(dbName);
    }

    public async Task EnsureIndexesAsync()
    {
        await Users.Indexes.CreateManyAsync(new[] {
            new CreateIndexModel<UserDoc>(Builders<UserDoc>.IndexKeys.Ascending(u => u.email), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<UserDoc>(Builders<UserDoc>.IndexKeys.Ascending(u => u.user_id), new CreateIndexOptions { Unique = true }),
        });
        await Sessions.Indexes.CreateManyAsync(new[] {
            new CreateIndexModel<SessionDoc>(Builders<SessionDoc>.IndexKeys.Ascending(s => s.session_token), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<SessionDoc>(Builders<SessionDoc>.IndexKeys.Ascending(s => s.expires_at), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }),
        });
        await Countries.Indexes.CreateOneAsync(new CreateIndexModel<CountryDoc>(Builders<CountryDoc>.IndexKeys.Ascending(c => c.country_id), new CreateIndexOptions { Unique = true }));
        await Cities.Indexes.CreateManyAsync(new[] {
            new CreateIndexModel<CityDoc>(Builders<CityDoc>.IndexKeys.Ascending(c => c.city_id), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<CityDoc>(Builders<CityDoc>.IndexKeys.Ascending(c => c.country_id)),
        });
        await Places.Indexes.CreateManyAsync(new[] {
            new CreateIndexModel<PlaceDoc>(Builders<PlaceDoc>.IndexKeys.Ascending(p => p.place_id), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<PlaceDoc>(Builders<PlaceDoc>.IndexKeys.Ascending(p => p.city_id)),
        });
        await Reviews.Indexes.CreateManyAsync(new[] {
            new CreateIndexModel<ReviewDoc>(Builders<ReviewDoc>.IndexKeys.Ascending(r => r.review_id), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<ReviewDoc>(Builders<ReviewDoc>.IndexKeys.Ascending(r => r.place_id)),
            new CreateIndexModel<ReviewDoc>(Builders<ReviewDoc>.IndexKeys.Ascending(r => r.user_id)),
        });
    }
}
