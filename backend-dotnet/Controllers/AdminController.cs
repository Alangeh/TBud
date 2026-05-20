using Microsoft.AspNetCore.Mvc;
using TravelReview.Api.Services;

namespace TravelReview.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly HydrationService _hydration;
    private readonly IConfiguration _cfg;
    public AdminController(HydrationService hydration, IConfiguration cfg) { _hydration = hydration; _cfg = cfg; }

    /// <summary>Public-readable lightweight status of the dynamic data hydration job.</summary>
    [HttpGet("hydration-status")]
    public async Task<IActionResult> Status(CancellationToken ct)
        => Ok(await _hydration.GetStatusAsync(ct));

    /// <summary>Force a re-run of dynamic data hydration. Idempotent — only adds missing data.</summary>
    [HttpPost("refresh-data")]
    public async Task<IActionResult> Refresh(
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken ct)
    {
        var admin = (Environment.GetEnvironmentVariable("ADMIN_TOKEN") ?? _cfg["Admin:Token"] ?? "").Trim();
        if (string.IsNullOrEmpty(admin))
            return StatusCode(503, new { detail = "ADMIN_TOKEN not configured" });
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Unauthorized(new { detail = "Missing admin bearer token" });
        if (authorization["Bearer ".Length..].Trim() != admin)
            return StatusCode(403, new { detail = "Invalid admin token" });

        var result = await _hydration.RunAsync(force: true, ct);
        return Ok(result);
    }
}
