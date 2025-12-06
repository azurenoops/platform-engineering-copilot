using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Platform.Engineering.Copilot.Admin.Models;
using Platform.Engineering.Copilot.Core.Data.Context;
using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Admin.Services;

/// <summary>
/// Service for managing agent configurations
/// </summary>
public class AgentConfigurationService
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgentConfigurationService> _logger;

    public AgentConfigurationService(
        PlatformEngineeringCopilotContext context,
        IConfiguration configuration,
        ILogger<AgentConfigurationService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Get all agents grouped by category
    /// </summary>
    public async Task<AgentConfigurationListResponse> GetAllAgentsAsync()
    {
        var agents = await _context.AgentConfigurations
            .OrderBy(a => a.Category)
            .ThenBy(a => a.DisplayOrder)
            .ThenBy(a => a.DisplayName)
            .ToListAsync();

        var agentDtos = agents.Select(MapToDto).ToList();

        var grouped = agentDtos
            .GroupBy(a => a.Category)
            .Select(g => new AgentCategoryGroup
            {
                Category = g.Key,
                Agents = g.OrderBy(a => a.DisplayOrder).ThenBy(a => a.DisplayName).ToList(),
                EnabledCount = g.Count(a => a.IsEnabled),
                TotalCount = g.Count()
            })
            .OrderBy(g => g.Category)
            .ToList();

        return new AgentConfigurationListResponse
        {
            Categories = grouped,
            TotalAgents = agentDtos.Count,
            EnabledAgents = agentDtos.Count(a => a.IsEnabled)
        };
    }

    /// <summary>
    /// Get a single agent by name
    /// </summary>
    public async Task<AgentConfigurationDto?> GetAgentByNameAsync(string agentName)
    {
        var agent = await _context.AgentConfigurations
            .FirstOrDefaultAsync(a => a.AgentName == agentName);

        return agent != null ? MapToDto(agent) : null;
    }

    /// <summary>
    /// Update agent enabled status
    /// </summary>
    public async Task<AgentConfigurationDto?> UpdateAgentStatusAsync(
        string agentName, 
        UpdateAgentStatusRequest request)
    {
        var agent = await _context.AgentConfigurations
            .FirstOrDefaultAsync(a => a.AgentName == agentName);

        if (agent == null)
        {
            _logger.LogWarning("Agent not found: {AgentName}", agentName);
            return null;
        }

        agent.IsEnabled = request.IsEnabled;
        agent.ModifiedBy = request.ModifiedBy;
        agent.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Agent {AgentName} {Status} by {User}", 
            agentName, 
            request.IsEnabled ? "enabled" : "disabled",
            request.ModifiedBy ?? "system");

        // TODO: Trigger configuration reload in the application
        // This could be done via IOptionsMonitor or a background service

        return MapToDto(agent);
    }

    /// <summary>
    /// Update agent configuration
    /// </summary>
    public async Task<AgentConfigurationDto?> UpdateAgentConfigurationAsync(
        string agentName,
        UpdateAgentConfigurationRequest request)
    {
        var agent = await _context.AgentConfigurations
            .FirstOrDefaultAsync(a => a.AgentName == agentName);

        if (agent == null)
        {
            _logger.LogWarning("Agent not found: {AgentName}", agentName);
            return null;
        }

        // Update only provided fields
        if (request.DisplayName != null)
            agent.DisplayName = request.DisplayName;

        if (request.Description != null)
            agent.Description = request.Description;

        if (request.IsEnabled.HasValue)
            agent.IsEnabled = request.IsEnabled.Value;

        if (request.ConfigurationJson != null)
            agent.ConfigurationJson = request.ConfigurationJson;

        if (request.IconName != null)
            agent.IconName = request.IconName;

        if (request.DisplayOrder.HasValue)
            agent.DisplayOrder = request.DisplayOrder.Value;

        if (request.Dependencies != null)
            agent.Dependencies = request.Dependencies;

        agent.ModifiedBy = request.ModifiedBy;
        agent.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Agent {AgentName} configuration updated by {User}",
            agentName,
            request.ModifiedBy ?? "system");

        return MapToDto(agent);
    }

    /// <summary>
    /// Synchronize database state with in-memory configuration
    /// This allows runtime configuration changes without restart
    /// </summary>
    public async Task<bool> SyncConfigurationAsync()
    {
        try
        {
            var agents = await _context.AgentConfigurations.ToListAsync();

            // TODO: Implement actual configuration sync
            // This would involve updating IConfiguration or triggering a reload
            // For now, just log the current state

            _logger.LogInformation(
                "Configuration sync completed. {EnabledCount}/{TotalCount} agents enabled",
                agents.Count(a => a.IsEnabled),
                agents.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during configuration sync");
            return false;
        }
    }

    /// <summary>
    /// Initialize agent configurations from appsettings.json if database is empty
    /// </summary>
    public async Task<bool> SeedFromConfigurationAsync()
    {
        try
        {
            var existingCount = await _context.AgentConfigurations.CountAsync();
            if (existingCount > 0)
            {
                _logger.LogInformation("Agent configurations already seeded ({Count} agents)", existingCount);
                return false;
            }

            var agentConfig = _configuration.GetSection("AgentConfiguration");
            if (!agentConfig.Exists())
            {
                _logger.LogWarning("No AgentConfiguration section found in appsettings");
                return false;
            }

            var agents = new List<AgentConfiguration>();
            var displayOrder = 0;

            // Infrastructure Agent
            if (agentConfig.GetSection("InfrastructureAgent").Exists())
            {
                agents.Add(CreateAgentFromConfig("InfrastructureAgent", "Infrastructure Agent", 
                    "Manages infrastructure provisioning and IaC generation", "Core", "🏗️", 
                    agentConfig.GetSection("InfrastructureAgent"), displayOrder++));
            }

            // Compliance Agent
            if (agentConfig.GetSection("ComplianceAgent").Exists())
            {
                agents.Add(CreateAgentFromConfig("ComplianceAgent", "Compliance Agent",
                    "Ensures compliance with security frameworks and standards", "Compliance", "✓",
                    agentConfig.GetSection("ComplianceAgent"), displayOrder++));
            }

            // Cost Management Agent
            if (agentConfig.GetSection("CostManagementAgent").Exists())
            {
                agents.Add(CreateAgentFromConfig("CostManagementAgent", "Cost Management Agent",
                    "Monitors and optimizes cloud resource costs", "Cost", "💰",
                    agentConfig.GetSection("CostManagementAgent"), displayOrder++));
            }

            // Discovery Agent
            if (agentConfig.GetSection("DiscoveryAgent").Exists())
            {
                agents.Add(CreateAgentFromConfig("DiscoveryAgent", "Discovery Agent",
                    "Discovers and catalogs Azure resources", "Discovery", "🔍",
                    agentConfig.GetSection("DiscoveryAgent"), displayOrder++));
            }

            // Environment Agent
            if (agentConfig.GetSection("EnvironmentAgent").Exists())
            {
                agents.Add(CreateAgentFromConfig("EnvironmentAgent", "Environment Agent",
                    "Manages environment lifecycle and operations", "Core", "🌍",
                    agentConfig.GetSection("EnvironmentAgent"), displayOrder++));
            }

            // Security Agent
            if (agentConfig.GetSection("SecurityAgent").Exists())
            {
                agents.Add(CreateAgentFromConfig("SecurityAgent", "Security Agent",
                    "Provides security scanning and threat detection", "Security", "🔒",
                    agentConfig.GetSection("SecurityAgent"), displayOrder++));
            }

            // Knowledge Base Agent
            if (agentConfig.GetSection("KnowledgeBaseAgent").Exists())
            {
                agents.Add(CreateAgentFromConfig("KnowledgeBaseAgent", "Knowledge Base Agent",
                    "Provides intelligent search and documentation", "Core", "📚",
                    agentConfig.GetSection("KnowledgeBaseAgent"), displayOrder++));
            }

            // Service Creation Agent
            if (agentConfig.GetSection("ServiceCreationAgent").Exists())
            {
                agents.Add(CreateAgentFromConfig("ServiceCreationAgent", "Service Creation Agent",
                    "Automates service and application creation", "Core", "⚙️",
                    agentConfig.GetSection("ServiceCreationAgent"), displayOrder++));
            }

            // Document Agent
            if (agentConfig.GetSection("DocumentAgent").Exists())
            {
                agents.Add(CreateAgentFromConfig("DocumentAgent", "Document Agent",
                    "Generates and manages technical documentation", "Documentation", "📋",
                    agentConfig.GetSection("DocumentAgent"), displayOrder++));
            }

            if (agents.Any())
            {
                await _context.AgentConfigurations.AddRangeAsync(agents);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Seeded {Count} agent configurations from appsettings", agents.Count);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding agent configurations");
            return false;
        }
    }

    private AgentConfiguration CreateAgentFromConfig(
        string agentName,
        string displayName,
        string description,
        string category,
        string iconName,
        IConfigurationSection configSection,
        int displayOrder)
    {
        var isEnabled = configSection.GetValue<bool>("Enabled");
        
        // Serialize the entire configuration section as JSON
        var configJson = System.Text.Json.JsonSerializer.Serialize(
            configSection.Get<Dictionary<string, object>>());

        return new AgentConfiguration
        {
            AgentName = agentName,
            DisplayName = displayName,
            Description = description,
            IsEnabled = isEnabled,
            Category = category,
            IconName = iconName,
            ConfigurationJson = configJson,
            DisplayOrder = displayOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ModifiedBy = "system-seed",
            HealthStatus = isEnabled ? "Unknown" : "Disabled"
        };
    }

    private static AgentConfigurationDto MapToDto(AgentConfiguration agent)
    {
        return new AgentConfigurationDto
        {
            AgentConfigurationId = agent.AgentConfigurationId,
            AgentName = agent.AgentName,
            DisplayName = agent.DisplayName,
            Description = agent.Description,
            IsEnabled = agent.IsEnabled,
            Category = agent.Category,
            IconName = agent.IconName,
            ConfigurationJson = agent.ConfigurationJson,
            DisplayOrder = agent.DisplayOrder,
            CreatedAt = agent.CreatedAt,
            UpdatedAt = agent.UpdatedAt,
            ModifiedBy = agent.ModifiedBy,
            Dependencies = agent.Dependencies,
            LastExecutedAt = agent.LastExecutedAt,
            HealthStatus = agent.HealthStatus
        };
    }
}
