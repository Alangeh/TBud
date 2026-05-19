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
    var db = sp.GetRequiredService<AppDbContext>();
    if (cfg.GetValue<bool>("Migrations:ApplyOnStartup")) await db.Database.MigrateAsync();
    if (cfg.GetValue<bool>("Migrations:SeedOnStartup")) await sp.GetRequiredService<SeedService>().RunAsync();
}

app.Run();

// Expose Program for WebApplicationFactory<Program> in the test project.
public partial class Program { }
