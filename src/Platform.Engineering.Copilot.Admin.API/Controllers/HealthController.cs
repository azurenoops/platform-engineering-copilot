using Microsoft.AspNetCore.Mvc;

namespace Platform.Engineering.Copilot.Admin.API.Controllers;

/// <summary>
/// Health check endpoint — unauthenticated, returns simple status and timestamp.
/// Note: The main health endpoint is also mapped via MapHealthChecks("/health") in Program.cs.
/// This controller provides a richer JSON response at /api/health.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetHealth()
    {
        _logger.LogInformation("Health check requested");

        return Ok(new
        {
            status = "Healthy",
            timestamp = DateTimeOffset.UtcNow
        });
    }
}
