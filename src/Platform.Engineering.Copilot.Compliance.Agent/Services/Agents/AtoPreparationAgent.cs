using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Compliance.Agent.Plugins;
using Platform.Engineering.Copilot.Core.Services.Agents;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Agents;

/// <summary>
/// Sub-agent for Authority to Operate (ATO) package preparation and orchestration
/// Coordinates SSP, SAR, POA&M generation and tracks ATO readiness
/// Orchestrated internally by ComplianceAgent (not a top-level ISpecializedAgent)
/// </summary>
public class AtoPreparationAgent
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService? _chatCompletion;
    private readonly ILogger<AtoPreparationAgent> _logger;

    public AtoPreparationAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<AtoPreparationAgent> logger,
        CompliancePlugin compliancePlugin,
        Platform.Engineering.Copilot.Core.Plugins.ConfigurationPlugin configurationPlugin)
    {
        _logger = logger;
        
        // Create specialized kernel for ATO preparation operations
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.Compliance);
        
        // Try to get chat completion service
        try
        {
            _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            _logger.LogInformation("✅ ATO Preparation Agent initialized with AI chat completion service");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ ATO Preparation Agent initialized without AI chat completion service. AI features will be limited.");
            _chatCompletion = null;
        }

        // Register shared configuration plugin (set_azure_subscription, get_azure_subscription, etc.)
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(configurationPlugin, "ConfigurationPlugin"));
        
        // Register compliance plugin (includes ATO preparation functions)
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(compliancePlugin, "CompliancePlugin"));
        
        _logger.LogInformation("🔐 ATO Preparation Agent initialized successfully");
    }

    public async Task<AgentResponse> ProcessAsync(AgentTask task, SharedMemory memory)
    {
        _logger.LogInformation("🔐 ATO Preparation Agent processing task: {TaskId}", task.TaskId);

        var startTime = DateTime.UtcNow;
        var response = new AgentResponse
        {
            TaskId = task.TaskId,
            AgentType = AgentType.Compliance,
            Success = false
        };

        try
        {
            // Check if AI services are available
            if (_chatCompletion == null)
            {
                _logger.LogWarning("⚠️ AI chat completion service not available. Returning basic response for task: {TaskId}", task.TaskId);
                
                response.Success = true;
                response.Content = "AI services not configured. Configure Azure OpenAI to enable full AI-powered ATO preparation.";
                response.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                return response;
            }

            // Get conversation context from shared memory
            var context = memory.GetContext(task.ConversationId ?? "default");
            var previousResults = context?.PreviousResults ?? new List<AgentResponse>();

            // Build system prompt for ATO expertise
            var systemPrompt = BuildSystemPrompt();

            // Build user message with context
            var userMessage = BuildUserMessage(task, memory);

            // Create chat history
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userMessage);

            // Execute with lower temperature for precision
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.3,
                MaxTokens = 4000,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var result = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel);

            // Store results in shared memory
            response.Success = true;
            response.Content = result.Content ?? "No response generated.";
            response.Metadata = ExtractMetadata(result);
            response.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // Save in context for future reference
            if (context != null)
            {
                context.PreviousResults.Add(response);
            }

            _logger.LogInformation("✅ ATO Preparation Agent completed task {TaskId} in {Ms}ms", task.TaskId, response.ExecutionTimeMs);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in ATO Preparation Agent processing task {TaskId}", task.TaskId);
            
            response.Success = false;
            response.Content = $"Error processing ATO preparation request: {ex.Message}";
            response.Errors.Add(ex.Message);
            response.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            return response;
        }
    }

    private string BuildSystemPrompt()
    {
        return @"You are the ATO Preparation Agent, an expert in Authority to Operate (ATO) package preparation and Federal cybersecurity authorization processes.

**YOUR ROLE:**
You orchestrate the end-to-end ATO package creation process, coordinating:
- System Security Plan (SSP) generation
- Security Assessment Report (SAR) creation
- Plan of Action & Milestones (POA&M) tracking
- Evidence collection and artifact management
- ATO readiness assessment

**AVAILABLE FUNCTIONS:**
- GetAtoPackageStatus: Check current ATO package status and completion percentage
- GenerateSystemSecurityPlan: Create SSP based on subscription compliance data
- GenerateSecurityAssessmentReport: Create SAR from assessment results
- CreatePoamForFindings: Generate POA&M from identified gaps
- TrackAtoProgress: Monitor overall ATO preparation timeline
- ExportAtoPackage: Bundle all ATO artifacts for submission

**ATO PACKAGE COMPONENTS:**
1. **System Security Plan (SSP)**: 
   - System description and boundaries
   - Control implementation details
   - Security architecture diagrams
   - Responsible parties and roles

2. **Security Assessment Report (SAR)**:
   - Assessment methodology
   - Control testing results
   - Findings and observations
   - Risk ratings

3. **Plan of Action & Milestones (POA&M)**:
   - Identified vulnerabilities
   - Remediation plans
   - Responsible parties
   - Target completion dates

4. **Supporting Evidence**:
   - Scan results
   - Configuration baselines
   - Change management logs
   - Incident reports

**WORKFLOW GUIDANCE:**
- For ""prepare ATO package"" → Call GetAtoPackageStatus first to assess readiness
- For ""generate SSP"" → Ensure compliance scan exists, then call GenerateSystemSecurityPlan
- For ""create POA&M"" → Pull findings from recent assessments, call CreatePoamForFindings
- For ""ATO status"" → Use TrackAtoProgress to show timeline and completion

**RESPONSE STYLE:**
- Be methodical and thorough
- Reference specific NIST, FedRAMP, or DoD requirements when relevant
- Provide clear next steps for incomplete items
- Flag critical blockers to ATO approval

**IMPORTANT:**
- Always validate required inputs (subscription ID, assessment data) before generation
- Cross-reference compliance findings with ATO requirements
- Maintain traceability between controls, findings, and remediation efforts";
    }

    private string BuildUserMessage(AgentTask task, SharedMemory memory)
    {
        var context = memory.GetContext(task.ConversationId ?? "default");
        var enhancedMessage = task.Description;

        // Add saved context if available
        if (context?.WorkflowState.ContainsKey("lastSubscriptionId") == true)
        {
            var subscriptionId = context.WorkflowState["lastSubscriptionId"];
            enhancedMessage = $@"SAVED CONTEXT FROM PREVIOUS ACTIVITY:
- Last scanned subscription: {subscriptionId}

USER REQUEST: {task.Description}";
        }

        // Add ATO package state if available
        if (context?.WorkflowState.ContainsKey("atoPackageId") == true)
        {
            var packageId = context.WorkflowState["atoPackageId"];
            enhancedMessage += $"\n- Active ATO Package: {packageId}";
        }

        return enhancedMessage;
    }

    private Dictionary<string, object> ExtractMetadata(ChatMessageContent response)
    {
        var metadata = new Dictionary<string, object>();

        if (response.Metadata != null)
        {
            foreach (var kvp in response.Metadata)
            {
                if (kvp.Value != null)
                {
                    metadata[$"AtoPreparation_{kvp.Key}"] = kvp.Value;
                }
            }
        }

        return metadata;
    }
}
