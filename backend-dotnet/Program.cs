using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using TravelReview.Api.Services;

// Load .env (overrides appsettings, both still under-overridden by real env vars).
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Connection string priority: env var CONNECTION_STRING > ConnectionStrings:Default in appsettings.
var connectionString =
    Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("No SQL Server connection string configured. Set CONNECTION_STRING env var or ConnectionStrings:Default in appsettings.json.");

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString,
    sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

builder.Services.AddControllers().AddJsonOptions(o => {
    o.JsonSerializerOptions.PropertyNamingPolicy = null; // snake_case stays as-is
});
builder.Services.AddHttpClient();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SeedService>();
builder.Services.AddScoped<HydrationService>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "*" };
builder.Services.AddCors(o => o.AddDefaultPolicy(p => {
    if (allowedOrigins.Length == 1 && allowedOrigins[0] == "*")
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    else
        p.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
}));

var app = builder.Build();
app.UseCors();
app.MapControllers();

// Run migrations + seed on startup.
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var cfg = sp.GetRequiredService<IConfiguration>();
    var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var db = sp.GetRequiredService<AppDbContext>();

    if (cfg.GetValue<bool>("Migrations:ApplyOnStartup"))
    {
        // Prefer EF Core Migrations when the project has them scaffolded
        // (`dotnet ef migrations add Initial`). Otherwise, fall back to
        // EnsureCreatedAsync so first-time `docker compose up` users get a
        // working schema without needing the .NET SDK installed locally.
        var hasMigrations = db.Database.GetMigrations().Any();
        if (hasMigrations)
        {
            log.LogInformation("Applying EF Core migrations...");
            await db.Database.MigrateAsync();
        }
        else if (cfg.GetValue<bool>("Migrations:UseEnsureCreatedFallback", true))
        {
            log.LogWarning("No EF Core migrations found — falling back to EnsureCreatedAsync(). Run `dotnet ef migrations add Initial` for production-grade versioned schema.");
            await db.Database.EnsureCreatedAsync();
        }
    }

    if (cfg.GetValue<bool>("Migrations:SeedOnStartup"))
        await sp.GetRequiredService<SeedService>().RunAsync();
}

// Kick off dynamic data hydration in the background so the server is responsive
// immediately. Idempotent — only runs the first time (state tracked in DB).
if (app.Configuration.GetValue<bool>("Hydration:RunOnStartup", true))
{
    _ = Task.Run(async () =>
    {
        try
        {
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<HydrationService>().RunAsync();
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Background hydration failed");
        }
    });
}

app.Run();

// Expose Program for WebApplicationFactory<Program> in the test project.
public partial class Program { }
