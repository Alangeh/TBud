using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TravelReview.Api.Services;

namespace TravelReview.Api.Tests;

/// <summary>
/// Boots the real API in-process against an isolated SQL Server database
/// (`TravelReview_Test_&lt;guid&gt;`) so tests never touch dev data.
/// Connection string source: env var TEST_CONNECTION_STRING > CONNECTION_STRING >
/// default localhost SQL Server. The DB name is replaced with a unique one per run.
/// Database is dropped at the end of the test run.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string DbName { get; } = $"TravelReview_Test_{Guid.NewGuid():N}";
    public string ConnectionString { get; private set; } = "";

    private static string BaseConnectionString()
        => Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
           ?? Environment.GetEnvironmentVariable("CONNECTION_STRING")
           ?? "Server=localhost,1433;Database=TravelReview;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var csb = new SqlConnectionStringBuilder(BaseConnectionString()) { InitialCatalog = DbName };
        ConnectionString = csb.ConnectionString;

        Environment.SetEnvironmentVariable("CONNECTION_STRING", ConnectionString);
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-must-be-at-least-32-characters-xxx");
        builder.UseEnvironment("Testing");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
        }
        catch { /* best-effort cleanup */ }
    }
}
