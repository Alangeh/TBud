using DotNetEnv;
using TravelReview.Api.Services;

// Load .env if present (mirrors python-dotenv behavior).
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o => {
    o.JsonSerializerOptions.PropertyNamingPolicy = null; // keep snake_case as defined
});
builder.Services.AddHttpClient();
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<SeedService>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseCors();
app.MapControllers();

// Run indexes + seed on startup (fire and forget — fail fast in dev).
using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<MongoContext>();
    var seed = scope.ServiceProvider.GetRequiredService<SeedService>();
    await ctx.EnsureIndexesAsync();
    await seed.RunAsync();
}

app.Run();

// Expose Program for WebApplicationFactory<Program> in the test project.
public partial class Program { }
