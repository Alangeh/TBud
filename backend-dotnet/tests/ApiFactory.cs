using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MongoDB.Driver;

namespace TravelReview.Api.Tests;

/// <summary>
/// Boots the real API in-process against an isolated Mongo database (`travelreview_test_<guid>`)
/// so tests never touch dev data. Database is dropped at the end of the test run.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string DbName { get; } = $"travelreview_test_{Guid.NewGuid():N}";
    public string MongoUrl => Environment.GetEnvironmentVariable("MONGO_URL") ?? "mongodb://localhost:27017";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("MONGO_URL", MongoUrl);
        Environment.SetEnvironmentVariable("DB_NAME", DbName);
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-must-be-at-least-32-characters-xxx");
        builder.UseEnvironment("Testing");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try { await new MongoClient(MongoUrl).DropDatabaseAsync(DbName); } catch { /* ignore */ }
    }
}
