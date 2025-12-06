using Microsoft.AspNetCore.Mvc;
using Platform.Engineering.Copilot.Admin.Models;
using Platform.Engineering.Copilot.Admin.Services;

namespace Platform.Engineering.Copilot.Admin.Controllers;

/// <summary>
/// Admin API for managing agent configurations
/// </summary>
[ApiController]
[Route("api/admin/agents")]
[Produces("application/json")]
public class AgentAdminController : ControllerBase
{
    private readonly ILogger<AgentAdminController> _logger;
    private readonly AgentConfigurationService _agentService;

    public AgentAdminController(
        ILogger<AgentAdminController> logger,
        AgentConfigurationService agentService)
    {
        _logger = logger;
        _agentService = agentService;
    }

    /// <summary>
    /// Get all agents grouped by category
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AgentConfigurationListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AgentConfigurationListResponse>> GetAllAgents()
    {
        _logger.LogInformation("Admin API: Fetching all agent configurations");
        var response = await _agentService.GetAllAgentsAsync();
        return Ok(response);
    }

    /// <summary>
    /// Get a single agent by name
    /// </summary>
    [HttpGet("{agentName}")]
    [ProducesResponseType(typeof(AgentConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentConfigurationDto>> GetAgent(string agentName)
    {
        _logger.LogInformation("Admin API: Fetching agent {AgentName}", agentName);
        
        var agent = await _agentService.GetAgentByNameAsync(agentName);
        if (agent == null)
        {
            return NotFound(new { error = $"Agent '{agentName}' not found" });
        }

        return Ok(agent);
    }

    /// <summary>
    /// Update agent enabled/disabled status
    /// </summary>
    [HttpPut("{agentName}/status")]
    [ProducesResponseType(typeof(AgentConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentConfigurationDto>> UpdateAgentStatus(
        string agentName,
        [FromBody] UpdateAgentStatusRequest request)
    {
        _logger.LogInformation(
            "Admin API: Updating agent {AgentName} status to {Status}",
            agentName,
            request.IsEnabled ? "enabled" : "disabled");

        var agent = await _agentService.UpdateAgentStatusAsync(agentName, request);
        if (agent == null)
        {
            return NotFound(new { error = $"Agent '{agentName}' not found" });
        }

        return Ok(agent);
    }

    /// <summary>
    /// Update agent configuration
    /// </summary>
    [HttpPut("{agentName}")]
    [ProducesResponseType(typeof(AgentConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AgentConfigurationDto>> UpdateAgentConfiguration(
        string agentName,
        [FromBody] UpdateAgentConfigurationRequest request)
    {
        _logger.LogInformation("Admin API: Updating agent {AgentName} configuration", agentName);

        // Validate JSON if provided
        if (request.ConfigurationJson != null)
        {
            try
            {
                System.Text.Json.JsonDocument.Parse(request.ConfigurationJson);
            }
            catch (System.Text.Json.JsonException ex)
            {
                return BadRequest(new { error = "Invalid JSON in ConfigurationJson", details = ex.Message });
            }
        }

        var agent = await _agentService.UpdateAgentConfigurationAsync(agentName, request);
        if (agent == null)
        {
            return NotFound(new { error = $"Agent '{agentName}' not found" });
        }

        return Ok(agent);
    }

    /// <summary>
    /// Sync agent configurations from database to in-memory configuration
    /// </summary>
    [HttpPost("sync")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> SyncConfiguration()
    {
        _logger.LogInformation("Admin API: Syncing agent configurations");

        var success = await _agentService.SyncConfigurationAsync();
        if (!success)
        {
            return StatusCode(500, new { error = "Configuration sync failed" });
        }

        return Ok(new { message = "Configuration synced successfully" });
    }

    /// <summary>
    /// Seed agent configurations from appsettings.json (initialization only)
    /// </summary>
    [HttpPost("seed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SeedFromConfiguration()
    {
        _logger.LogInformation("Admin API: Seeding agent configurations from appsettings");

        var seeded = await _agentService.SeedFromConfigurationAsync();
        if (!seeded)
        {
            return Conflict(new { message = "Agent configurations already exist or no config found" });
        }

        return Ok(new { message = "Agent configurations seeded successfully" });
    }
}
