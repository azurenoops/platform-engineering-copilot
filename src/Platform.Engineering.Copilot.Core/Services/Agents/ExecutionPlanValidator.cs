using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models.Agents;

namespace Platform.Engineering.Copilot.Core.Services.Agents;

/// <summary>
/// Validates and corrects execution plans to ensure they match user intent
/// </summary>
public class ExecutionPlanValidator
{
    private readonly ILogger<ExecutionPlanValidator> _logger;

    public ExecutionPlanValidator(ILogger<ExecutionPlanValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validate and potentially correct an execution plan based on user message
    /// </summary>
    public ExecutionPlan ValidateAndCorrect(ExecutionPlan plan, string userMessage, string conversationId)
    {
        _logger.LogInformation("🔍 VALIDATOR ENTRY: message='{Message}', planTasks={TaskCount}, agents={Agents}", 
            userMessage, plan.Tasks.Count, string.Join(",", plan.Tasks.Select(t => t.AgentType)));
        
        // CRITICAL: Check for compliance scanning requests FIRST - this takes precedence over all other validations
        if (IsComplianceScanningRequest(userMessage))
        {
            // If plan correctly has Compliance agent, return as-is
            if (plan.Tasks.Any(t => t.AgentType == AgentType.Compliance))
            {
                _logger.LogInformation("✅ Plan validation: Compliance scanning request correctly routed to ComplianceAgent");
                return plan; // STOP HERE - compliance scan is correctly routed
            }
            
            // If plan is wrong (e.g., Infrastructure), correct it
            _logger.LogWarning("⚠️  Plan validation: Compliance scanning request incorrectly routed, correcting to ComplianceAgent");
            return CreateComplianceScanningPlan(userMessage, conversationId); // STOP HERE - corrected to compliance
        }

        // CRITICAL: Check for actual provisioning requests SECOND - user explicitly wants to deploy
        if (IsActualProvisioningRequest(userMessage))
        {
            // If plan has all 5 agents, it's correct
            if (HasAllProvisioningAgents(plan))
            {
                _logger.LogInformation("✅ Plan validation: Actual provisioning request correctly routed to all 5 agents");
                return plan;
            }
            
            // If plan is incomplete (missing agents), correct it
            _logger.LogWarning("⚠️  Plan validation: Actual provisioning request missing agents, correcting to full workflow");
            return CreateActualProvisioningPlan(userMessage, conversationId);
        }

        // ONLY check template generation if NOT a compliance scan or actual provisioning
        // CRITICAL: Also skip if this is a Discovery-only request (resource discovery, tag search, etc.)
        if (IsDiscoveryOnlyRequest(userMessage, plan))
        {
            _logger.LogInformation("✅ Plan validation: Discovery-only request correctly routed");
            return plan; // Discovery requests should not be modified
        }
        
        // Check if this is a template generation request being treated as provisioning
        if (IsTemplateGenerationRequest(userMessage) && !IsActualProvisioningRequest(userMessage) && HasProvisioningAgents(plan))
        {
            _logger.LogWarning("⚠️  Plan validation: Template generation request incorrectly includes provisioning agents");
            return CreateTemplateGenerationPlan(userMessage, conversationId);
        }

        return plan;
    }
    
    private bool IsDiscoveryOnlyRequest(string userMessage, ExecutionPlan plan)
    {
        var lowerMessage = userMessage.ToLowerInvariant();
        
        // Discovery-specific keywords that indicate resource querying (not provisioning)
        var discoveryIndicators = new[]
        {
            "list resources", "find resources", "show resources", "discover resources",
            "with tag", "tagged with", "search by tag", "resources with tag",
            "tag createdby", "tag environment", "find all resources with",
            "inventory", "what resources", "resource group", "/subscriptions/",
            "health status", "resource health", "dependencies"
        };
        
        var hasDiscoveryIndicator = discoveryIndicators.Any(i => lowerMessage.Contains(i));
        
        // Check if plan only has Discovery agent (and maybe KnowledgeBase for help)
        var onlyHasDiscovery = plan.Tasks.All(t => 
            t.AgentType == AgentType.Discovery || 
            t.AgentType == AgentType.KnowledgeBase);
        
        if (hasDiscoveryIndicator || onlyHasDiscovery)
        {
            _logger.LogInformation("✅ IsDiscoveryOnlyRequest: TRUE - hasIndicator={HasIndicator}, onlyDiscovery={OnlyDiscovery}", 
                hasDiscoveryIndicator, onlyHasDiscovery);
            return true;
        }
        
        _logger.LogInformation("❌ IsDiscoveryOnlyRequest: FALSE - no discovery indicators found");
        return false;
    }

    private bool IsActualProvisioningRequest(string userMessage)
    {
        var lowerMessage = userMessage.ToLowerInvariant();

        // Strong indicators of actual provisioning - user explicitly wants to deploy
        var provisioningIndicators = new[]
        {
            "actually provision", "make it live", "make this live",
            "execute deployment", "create the resources now", "deploy the template",
            "provision for real", "provision this", "deploy this now",
            "create resources now", "execute this", "provision now"
        };

        // Check for strong multi-word indicators (explicit provisioning intent)
        if (provisioningIndicators.Any(i => lowerMessage.Contains(i)))
        {
            _logger.LogInformation("🚀 DETECTED ACTUAL PROVISIONING REQUEST: '{UserMessage}'", userMessage);
            return true;
        }

        // Check for combination of urgency + provisioning keywords
        var urgencyKeywords = new[] { "actually", "now", "immediately", "right now", "execute", "live" };
        var provisioningKeywords = new[] { "provision", "deploy", "create resources", "make it", "deployment" };

        var hasUrgency = urgencyKeywords.Any(v => lowerMessage.Contains(v));
        var hasProvisioning = provisioningKeywords.Any(k => lowerMessage.Contains(k));

        if (hasUrgency && hasProvisioning)
        {
            _logger.LogInformation("🚀 DETECTED ACTUAL PROVISIONING REQUEST (urgency + provisioning): '{UserMessage}'", userMessage);
            return true;
        }

        return false;
    }

    private bool HasAllProvisioningAgents(ExecutionPlan plan)
    {
        // Check if plan has all 5 agents needed for actual provisioning
        // Correct execution order: Infrastructure (1) → Environment (2) → Discovery (3) → Compliance (4) → Cost (5)
        var requiredAgents = new[]
        {
            AgentType.Infrastructure,  // Priority 1: Generate/validate template
            AgentType.Environment,     // Priority 2: Deploy resources
            AgentType.Discovery,       // Priority 3: Verify created resources in new RG
            AgentType.Compliance,      // Priority 4: Scan new RG only (not entire subscription)
            AgentType.CostManagement   // Priority 5: Estimate costs
        };

        var hasAllAgents = requiredAgents.All(agentType => 
            plan.Tasks.Any(t => t.AgentType == agentType));

        if (hasAllAgents)
        {
            _logger.LogInformation("✅ Plan has all 5 provisioning agents in correct order: Infrastructure (1) → Environment (2) → Discovery (3) → Compliance (4) → CostManagement (5)");
        }
        else
        {
            var presentAgents = plan.Tasks.Select(t => t.AgentType).Distinct().ToList();
            var missingAgents = requiredAgents.Except(presentAgents).ToList();
            _logger.LogWarning("⚠️  Plan missing provisioning agents: {MissingAgents}", string.Join(", ", missingAgents));
        }

        return hasAllAgents;
    }

    private ExecutionPlan CreateActualProvisioningPlan(string userMessage, string conversationId)
    {
        _logger.LogInformation("✅ Creating corrected plan for ACTUAL PROVISIONING with all 5 agents");

        return new ExecutionPlan
        {
            PrimaryIntent = "provisioning",
            ExecutionPattern = ExecutionPattern.Sequential, // Sequential: Infrastructure → Environment → Discovery → Compliance → Cost
            EstimatedTimeSeconds = 120, // 60-180 seconds for actual provisioning
            Tasks = new List<AgentTask>
            {
                // Step 1: Infrastructure - Generate/validate template FIRST
                new AgentTask
                {
                    AgentType = AgentType.Infrastructure,
                    Description = userMessage,
                    Priority = 1, // FIRST: Generate template before deployment
                    IsCritical = true,
                    ConversationId = conversationId
                },
                // Step 2: Environment - Deploy resources SECOND
                new AgentTask
                {
                    AgentType = AgentType.Environment,
                    Description = $"Deploy the infrastructure template: {userMessage}",
                    Priority = 2, // SECOND: Deploy after template is ready
                    IsCritical = true,
                    ConversationId = conversationId
                },
                // Step 3: Discovery - Verify created resources THIRD
                new AgentTask
                {
                    AgentType = AgentType.Discovery,
                    Description = "Discover and verify newly created resources in the deployed resource group",
                    Priority = 3, // THIRD: Discover after deployment completes
                    IsCritical = false,
                    ConversationId = conversationId
                },
                // Step 4: Compliance - Scan ONLY the new resource group FOURTH
                new AgentTask
                {
                    AgentType = AgentType.Compliance,
                    Description = "Perform compliance scan on newly created resource group only (not entire subscription)",
                    Priority = 4, // FOURTH: Scan after resources exist
                    IsCritical = false,
                    ConversationId = conversationId
                },
                // Step 5: CostManagement - Estimate costs LAST
                new AgentTask
                {
                    AgentType = AgentType.CostManagement,
                    Description = "Estimate monthly costs for deployed resources",
                    Priority = 5, // FIFTH: Estimate costs after everything is deployed
                    IsCritical = false,
                    ConversationId = conversationId
                }
            }
        };
    }

    private ExecutionPlan CreateInfrastructurePlan(string userMessage, string conversationId)
    {
        _logger.LogInformation("✅ Correcting plan to infrastructure-only (without provisioning agents)");

        return new ExecutionPlan
        {
            PrimaryIntent = "infrastructure",
            ExecutionPattern = ExecutionPattern.Sequential,
            EstimatedTimeSeconds = 30,
            Tasks = new List<AgentTask>
            {
                new AgentTask
                {
                    AgentType = AgentType.Infrastructure,
                    Description = userMessage,
                    Priority = 1,
                    IsCritical = true,
                    ConversationId = conversationId
                }
            }
        };
    }

    private bool IsComplianceScanningRequest(string message)
    {
        var lowerMessage = message.ToLowerInvariant();

        // Strong indicators of compliance scanning (checking existing resources)
        var scanningIndicators = new[]
        {
            "check compliance", "run a compliance assessment", "scan my subscription",
            "compliance status", "security assessment", "assess compliance",
            "validate compliance", "audit", "compliance scan"
        };

        // Check for strong multi-word indicators first
        if (scanningIndicators.Any(i => lowerMessage.Contains(i)))
        {
            return true;
        }

        // Check for combination of action + compliance keywords
        var actionVerbs = new[] { "check", "scan", "assess", "validate", "audit", "evaluate", "review" };
        var complianceKeywords = new[] { "compliance", "compliant", "nist", "fedramp", "security" };

        var hasActionVerb = actionVerbs.Any(v => lowerMessage.Contains(v));
        var hasComplianceKeyword = complianceKeywords.Any(k => lowerMessage.Contains(k));

        return hasActionVerb && hasComplianceKeyword;
    }

    private ExecutionPlan CreateComplianceScanningPlan(string userMessage, string conversationId)
    {
        _logger.LogInformation("✅ Creating corrected plan for compliance scanning");

        return new ExecutionPlan
        {
            PrimaryIntent = "compliance",
            ExecutionPattern = ExecutionPattern.Sequential,
            EstimatedTimeSeconds = 60,
            Tasks = new List<AgentTask>
            {
                new AgentTask
                {
                    AgentType = AgentType.Compliance,
                    Description = userMessage,
                    Priority = 1,
                    IsCritical = true,
                    ConversationId = conversationId
                }
            }
        };
    }

    private bool IsTemplateGenerationRequest(string message)
    {
        var lowerMessage = message.ToLowerInvariant();

        // CRITICAL: Check if this is actually a provisioning request FIRST
        // If user says "actually provision", they want provisioning, NOT template generation
        if (IsActualProvisioningRequest(message))
        {
            _logger.LogInformation("🔍 IsTemplateGenerationRequest: FALSE - IsActualProvisioningRequest=true");
            return false; // NOT a template generation request - it's provisioning!
        }
        
        // CRITICAL: Check if this is a Discovery request (resource querying, tag search, inventory)
        // Discovery requests should NOT be treated as template generation
        var isDiscoveryQuery = lowerMessage.Contains("find resources") || 
                               lowerMessage.Contains("list resources") ||
                               lowerMessage.Contains("show resources") ||
                               lowerMessage.Contains("with tag") ||
                               lowerMessage.Contains("tagged with") ||
                               lowerMessage.Contains("tag createdby") ||
                               lowerMessage.Contains("tag environment") ||
                               lowerMessage.Contains("search by tag") ||
                               lowerMessage.Contains("inventory") ||
                               lowerMessage.Contains("what resources") ||
                               lowerMessage.Contains("discover resources");
        
        _logger.LogInformation("🔍 IsTemplateGenerationRequest discovery check: isDiscoveryQuery={IsDiscoveryQuery}, message={Message}", isDiscoveryQuery, lowerMessage);
        
        if (isDiscoveryQuery)
        {
            _logger.LogInformation("🔍 IsTemplateGenerationRequest: FALSE - Discovery query detected");
            return false; // NOT template generation - it's discovery!
        }

        // Indicators of template generation (when NOT provisioning)
        var templateIndicators = new[]
        {
            "template", "bicep", "arm", "iac", "blueprint", "terraform",
            "generate code", "show me the code", "infrastructure code"
        };

        var hasTemplateIndicator = templateIndicators.Any(i => lowerMessage.Contains(i));

        // If they explicitly ask for a template or code, it's template generation
        if (hasTemplateIndicator)
        {
            return true;
        }

        // Generic deployment words like "deploy", "create", "set up" are template generation
        // UNLESS they have urgency/provisioning keywords OR it's a discovery query
        var deploymentWords = new[] { "deploy", "create", "set up", "i need", "provision" };
        var hasDeploymentWord = deploymentWords.Any(w => lowerMessage.Contains(w));

        // Only consider it template generation if it has deployment words
        // AND doesn't have actual provisioning intent
        return hasDeploymentWord && !IsActualProvisioningRequest(message);
    }

    private bool HasProvisioningAgents(ExecutionPlan plan)
    {
        var provisioningAgents = new[]
        {
            AgentType.Compliance,
            AgentType.Infrastructure,
            AgentType.Discovery,
            AgentType.Environment,
            AgentType.CostManagement
        };

        return plan.Tasks.Any(t => provisioningAgents.Contains(t.AgentType));
    }

    private ExecutionPlan CreateTemplateGenerationPlan(string userMessage, string conversationId)
    {
        _logger.LogInformation("✅ Correcting plan to infrastructure-only template generation");

        return new ExecutionPlan
        {
            PrimaryIntent = "template_generation",
            ExecutionPattern = ExecutionPattern.Sequential,
            EstimatedTimeSeconds = 30,
            Tasks = new List<AgentTask>
            {
                new AgentTask
                {
                    AgentType = AgentType.Infrastructure,
                    Description = userMessage,
                    Priority = 1,
                    IsCritical = true,
                    ConversationId = conversationId
                }
            }
        };
    }
}
