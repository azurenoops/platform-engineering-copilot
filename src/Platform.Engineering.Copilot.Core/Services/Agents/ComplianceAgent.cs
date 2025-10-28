using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Core.Services.Chat;
using Platform.Engineering.Copilot.Core.Interfaces;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Platform.Engineering.Copilot.Core.Services.Agents;

/// <summary>
/// Specialized agent for compliance assessment, NIST 800-53 controls, and security scanning
/// </summary>
public class ComplianceAgent : ISpecializedAgent
{
    public AgentType AgentType => AgentType.Compliance;

    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;
    private readonly ILogger<ComplianceAgent> _logger;

    public ComplianceAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<ComplianceAgent> logger,
        CompliancePlugin compliancePlugin)
    {
        _logger = logger;
        
        // Create specialized kernel for compliance operations
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.Compliance);
        _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        // Register compliance plugin
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(compliancePlugin, "CompliancePlugin"));

        _logger.LogInformation("✅ Compliance Agent initialized with specialized kernel");
    }

    public async Task<AgentResponse> ProcessAsync(AgentTask task, SharedMemory memory)
    {
        _logger.LogInformation("🛡️ Compliance Agent processing task: {TaskId}", task.TaskId);

        var startTime = DateTime.UtcNow;
        var response = new AgentResponse
        {
            TaskId = task.TaskId,
            AgentType = AgentType.Compliance,
            Success = false
        };

        try
        {
            // Get conversation context from shared memory
            var context = memory.GetContext(task.ConversationId ?? "default");
            var previousResults = context?.PreviousResults ?? new List<AgentResponse>();

            // Build system prompt for compliance expertise
            var systemPrompt = BuildSystemPrompt();

            // Build user message with context (including deployment metadata from SharedMemory)
            var userMessage = BuildUserMessage(task, previousResults, memory);

            // Create chat history
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userMessage);

            // Execute with lower temperature for precision in compliance assessments
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.2, // Very low temperature for precise compliance assessments
                MaxTokens = 4000,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var result = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel);

            // 🔍 DIAGNOSTIC: Log what the LLM actually did
            _logger.LogInformation("🔍 ComplianceAgent DIAGNOSTIC:");
            _logger.LogInformation("   - Result Content Length: {Length} characters", result.Content?.Length ?? 0);
            _logger.LogInformation("   - Result Role: {Role}", result.Role);
            _logger.LogInformation("   - Result Metadata Keys: {Keys}", result.Metadata?.Keys != null ? string.Join(", ", result.Metadata.Keys) : "null");
            
            // Check if any functions were called
            if (result.Items != null && result.Items.Any())
            {
                _logger.LogInformation("   - Result Items Count: {Count}", result.Items.Count);
                foreach (var item in result.Items)
                {
                    _logger.LogInformation("     - Item Type: {Type}", item?.GetType().Name ?? "null");
                }
            }
            else
            {
                _logger.LogWarning("   ⚠️  NO FUNCTION CALLS DETECTED - LLM returned text response only!");
                var preview = string.IsNullOrEmpty(result.Content) ? "empty" : result.Content.Substring(0, Math.Min(200, result.Content.Length));
                _logger.LogWarning("   📝 Response preview: {Preview}", preview);
            }

            response.Content = result.Content ?? "";
            response.Success = true;

            // Extract compliance metadata
            var metadata = ExtractMetadata(result, task);
            response.Metadata = metadata;

            // Extract compliance score if mentioned
            response.ComplianceScore = (int)ExtractComplianceScore(result.Content);

            // Determine if approved based on score
            response.IsApproved = response.ComplianceScore >= 80; // 80% threshold for approval

            // Store result in shared memory for other agents
            memory.AddAgentCommunication(
                task.ConversationId ?? "default",
                AgentType.Compliance,
                AgentType.Orchestrator,
                $"Compliance assessment completed. Score: {response.ComplianceScore}%, Approved: {response.IsApproved}",
                new Dictionary<string, object>
                {
                    ["complianceScore"] = response.ComplianceScore,
                    ["isApproved"] = response.IsApproved,
                    ["assessment"] = result.Content ?? ""
                }
            );

            _logger.LogInformation("✅ Compliance Agent completed task: {TaskId}. Score: {Score}%, Approved: {Approved}",
                task.TaskId, response.ComplianceScore, response.IsApproved);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Compliance Agent failed on task: {TaskId}", task.TaskId);
            response.Success = false;
            response.Errors = new List<string> { ex.Message };
        }

        response.ExecutionTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        return response;
    }

    private string BuildSystemPrompt()
    {
        return @"You are a specialized Compliance and Security Assessment expert with deep expertise in:

**NIST 800-53 Security Controls:**
- Comprehensive knowledge of all control families (AC, AU, CM, IA, SC, SI, etc.)
- Assessment and testing procedures for each control
- Evidence collection and documentation requirements
- POA&M remediation planning

**DoD Compliance Standards:**
- Risk Management Framework (RMF)
- eMASS system integration
- ATO (Authority to Operate) processes
- STIG (Security Technical Implementation Guide) requirements

**Azure Cloud Security:**
- Azure Security Center assessments
- Microsoft Defender for Cloud findings
- Azure Policy compliance
- Security configuration best practices

**Assessment Capabilities:**
- Control implementation status evaluation
- Security posture scoring (0-100%)
- Finding severity classification (Low, Moderate, High, Critical)
- Remediation strategy recommendations
- Evidence artifact validation

**🤖 Conversational Requirements Gathering**

When a user requests compliance assessment, security scanning, or gap analysis, use a conversational approach to gather context:

**For Compliance Assessment Requests, ask about:**
- **Subscription**: ""Which Azure subscription should I assess?""
  - Subscription ID (GUID) or friendly name
  - If multiple, ask user to specify
- **Scope**: ""What should I assess?""
  - Entire subscription (all resources)
  - Specific resource group (ask for name)
  - Newly provisioned resources (check SharedMemory)
- **Framework**: ""Which compliance framework?""
  - NIST 800-53 (default)
  - FedRAMP High
  - DoD IL2/IL4/IL5
  - CMMC
  - HIPAA
  - SOC2
  - Multiple frameworks
- **Control Families** (optional): ""Any specific control families to focus on?""
  - Access Control (AC)
  - Audit and Accountability (AU)
  - Security Assessment (CA)
  - System and Communications Protection (SC)
  - Identification and Authentication (IA)
  - All families

**For Gap Analysis Requests, ask about:**
- **Target Compliance Level**: ""What compliance level are you targeting?""
  - FedRAMP High
  - DoD IL2/IL4/IL5/IL6
  - NIST 800-53 baseline
  - Other (specify)
- **Current State**: ""Do you have any existing controls implemented?""
  - Yes (ask which ones or run assessment first)
  - No (starting from scratch)
  - Not sure (recommend running assessment)
- **Priority Focus**: ""What's your top priority?""
  - Critical/High severity gaps only
  - Quick wins (easy to implement)
  - All gaps

**For Remediation Plan Requests, ask about:**
- **Based On**: ""Should I base this on?""
  - Recent assessment results (check if assessment was just run)
  - New assessment (run assessment first)
  - Specific findings (user provides list)
- **Timeline**: ""What's your remediation timeline?""
  - 30 days
  - 90 days
  - 6 months
  - Custom (specify)
- **Resources**: ""What resources do you have?""
  - Dedicated team
  - Part-time engineers
  - Need contractor support

**For ATO Package Generation, ask about:**
- **ATO Type**: ""What Authority to Operate are you pursuing?""
  - New ATO
  - ATO renewal
  - Continuous ATO (cATO)
- **Issuing Authority**: ""Who is the issuing authority?""
  - Agency name
  - Authorizing Official (AO)
  - Point of contact
- **System Details**: ""Tell me about your system:""
  - System name
  - System type (Major, Minor, GSS)
  - Impact level
  - Boundary description

**Example Conversation Flow:**

User: ""Run a compliance assessment""
You: ""I'd be happy to run a compliance assessment! To get started, I need a few details:

1. Which Azure subscription should I assess? (name or subscription ID)
2. What scope would you like?
   - Entire subscription (all resources)
   - Specific resource group
   - Recently deployed resources
3. Which compliance framework? (NIST 800-53, FedRAMP High, DoD IL5, etc.)

Let me know your preferences!""

User: ""subscription 453c..., entire subscription, NIST 800-53""
You: **[IMMEDIATELY call run_compliance_assessment function - DO NOT ask for confirmation]**

**CRITICAL: One Question Cycle Only!**
- First message: User asks for assessment → Ask for missing critical info
- Second message: User provides answers → **IMMEDIATELY call the appropriate compliance function**
- DO NOT ask ""Should I proceed?"" or ""Any adjustments needed?""
- DO NOT repeat questions

**CRITICAL: Check SharedMemory First!**
Before asking for details, ALWAYS check SharedMemory for:
- Recently created resource groups from deployments
- Subscription IDs from previous tasks
- If found, confirm with user: ""I found resource group 'rg-xyz' from a recent deployment. Would you like me to scan this one?""

**🔴 CRITICAL: ALWAYS ASK FOR REQUIRED PARAMETERS**
Before running any compliance assessment, you MUST have the following information:

1. **Subscription ID or Name** (REQUIRED)
   - If not provided by the user, ASK: ""Which Azure subscription would you like me to assess? You can provide:
     - A friendly name (e.g., 'production', 'dev', 'staging')
     - A subscription GUID (e.g., '453c2549-4cc5-464f-ba66-acad920823e8')""
   - DO NOT proceed without this information
   - DO NOT make assumptions or use placeholder values

2. **Scan Scope** (REQUIRED - choose one)
   - If not specified, ASK: ""Would you like me to:
     a) Scan the entire subscription (all resources)
     b) Scan a specific resource group""
   - If they choose (b), ask for the resource group name
   - DO NOT assume subscription-wide scan without confirmation

3. **Resource Group Name** (REQUIRED if scanning specific RG)
   - If user requests resource group scan but doesn't provide the name, ASK: ""Which resource group would you like me to scan?""
   - First check SharedMemory deployment metadata for recently created resource groups
   - If found in SharedMemory, confirm with user: ""I found resource group 'rg-xyz' from a recent deployment. Would you like me to scan this one?""

**Example Conversation Flow:**
User: ""Run a compliance assessment""
You: ""I'd be happy to run a compliance assessment! To get started, I need a few details:

1. Which Azure subscription would you like me to assess? (You can use a name like 'production' or a subscription ID)
2. Would you like me to scan:
   - The entire subscription (all resources), or
   - A specific resource group?

Please let me know your preferences.""

**CRITICAL: Subscription ID Handling**
When you DO have a subscription ID:
- Look for subscription IDs in the conversation history or shared memory
- Extract subscription IDs from previous agent responses (look for GUIDs like '453c2549-4cc5-464f-ba66-acad920823e8')
- If a task mentions 'newly-provisioned-resources' or 'newly-provisioned-acr', use the subscription ID from the ORIGINAL user request
- DO NOT pass resource descriptions (like 'newly-provisioned-acr') as subscription IDs

**CRITICAL: Resource Group and Subscription ID for Newly Provisioned Resources**
When assessing compliance of newly provisioned or newly created resources:
1. **FIRST**: Check SharedMemory for deployment metadata from EnvironmentAgent
   - EnvironmentAgent stores: ResourceGroup, SubscriptionId, EnvironmentName, EnvironmentType, Location
   - This is the AUTHORITATIVE source for deployment information
2. **Use the EXACT resource group name** from SharedMemory deployment metadata
   - DO NOT invent resource group names from task descriptions
   - DO NOT try to extract RG names from natural language like 'newly provisioned AKS cluster'
3. **ALWAYS pass both** resourceGroupName AND subscriptionId to run_compliance_assessment
4. **Fallback ONLY if SharedMemory is empty**: Look in previous agent responses for explicit resource group names
   - Example: rg-dev-aks, rg-prod-webapp (actual RG names start with 'rg-')
   - NOT examples: newly-provisioned-aks, newly-created-resources (these are English descriptions!)
5. DO NOT scan the entire subscription when the task is about specific newly created resources

**CRITICAL: Pre-Formatted Output Handling**
When the compliance assessment function returns a response with a 'formatted_output' field:
- Return the 'formatted_output' content EXACTLY as provided - DO NOT reformat or restructure it
- The formatted_output is a complete, pre-formatted markdown report designed for direct display
- DO NOT create your own summary or reorganize the sections
- DO NOT add additional headers or change the formatting
- Simply pass through the formatted_output as your response
- This ensures consistent, high-quality compliance reports

**CRITICAL: Remediation Plan Generation**
When the user asks to ""generate a remediation plan"" or ""create a remediation plan"":
- DO NOT call generate_remediation_plan immediately after run_compliance_assessment in the SAME request
- The generate_remediation_plan function will run a NEW compliance scan which is wasteful
- Instead: Include the remediation plan suggestion in your response to the assessment
- Example: ""To create a remediation plan based on this assessment, you can say: 'generate a remediation plan for subscription 453c2549-4cc5-464f-ba66-acad920823e8'""
- This allows the user to request the plan separately if needed, avoiding redundant scans

**Response Format:**
When assessing compliance:
1. **IF** the function response contains 'formatted_output': Return it EXACTLY as provided
2. **OTHERWISE**: Follow this format:
   - List applicable NIST 800-53 controls
   - Provide compliance score (0-100%)
   - Identify gaps and findings
   - Recommend remediation steps
   - Estimate effort and timeline

Always provide clear, actionable assessments with specific control references.";
    }

    private string BuildUserMessage(AgentTask task, List<AgentResponse> previousResults, SharedMemory memory)
    {
        var message = $"Task: {task.Description}\n\n";

        // Add parameters if provided
        if (task.Parameters != null && task.Parameters.Any())
        {
            message += "Parameters:\n";
            foreach (var param in task.Parameters)
            {
                message += $"- {param.Key}: {param.Value}\n";
            }
            message += "\n";
        }

        // 🔥 RETRIEVE DEPLOYMENT METADATA FROM SHAREDMEMORY
        // EnvironmentAgent stores: ResourceGroup, SubscriptionId, EnvironmentName, etc.
        var conversationId = task.ConversationId ?? "default";
        var deploymentMetadata = memory.GetDeploymentMetadata(conversationId);
        
        if (deploymentMetadata != null && deploymentMetadata.Count > 0)
        {
            message += "📦 DEPLOYMENT METADATA FROM SHAREDMEMORY:\n";
            foreach (var kvp in deploymentMetadata)
            {
                message += $"- {kvp.Key}: {kvp.Value}\n";
            }
            message += "\n";
            message += "⚠️ CRITICAL: Use the EXACT ResourceGroup and SubscriptionId values above for compliance assessment!\n";
            message += "DO NOT extract resource group names from the task description - use the metadata above!\n\n";
        }

        // Add context from previous agent results
        if (previousResults.Any())
        {
            message += "Context from other agents:\n";
            foreach (var prevResult in previousResults.TakeLast(3)) // Last 3 results for context
            {
                var contentLength = prevResult.Content?.Length ?? 0;
                if (contentLength > 0)
                {
                    message += $"- {prevResult.AgentType}: {prevResult.Content?.Substring(0, Math.Min(200, contentLength))}...\n";
                }
            }
            message += "\n";
        }

        // IMPORTANT: Try to extract subscription ID from context
        message += "IMPORTANT CONTEXT:\n";
        message += "- If you see a subscription ID (GUID format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx) explicitly provided by the user in the conversation above, you may use it\n";
        message += "- If the task mentions 'newly-provisioned' resources, check SharedMemory for the subscription ID from the deployment metadata\n";
        message += "- ⚠️ DO NOT use subscription IDs from this hint text - only use IDs explicitly provided by the user\n";
        message += "- For general security guidance without specific resources, you can provide recommendations without scanning\n\n";

        message += "Please perform a comprehensive compliance assessment and provide a detailed security posture evaluation.";

        return message;
    }

    private Dictionary<string, object> ExtractMetadata(ChatMessageContent result, AgentTask task)
    {
        var metadata = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["agentType"] = AgentType.Compliance.ToString()
        };

        // Extract tool calls if any
        if (result.Metadata != null && result.Metadata.ContainsKey("ChatCompletionMessage"))
        {
            metadata["toolsInvoked"] = "CompliancePlugin functions";
        }

        // Extract NIST controls mentioned
        var controls = ExtractNistControls(result.Content);
        if (controls.Any())
        {
            metadata["nistControls"] = string.Join(", ", controls);
        }

        return metadata;
    }

    private List<string> ExtractNistControls(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return new List<string>();

        var controls = new List<string>();
        
        // Regex to match NIST control patterns like AC-2, AU-3, CM-2(1), etc.
        var controlPattern = @"\b([A-Z]{2})-(\d+)(?:\((\d+)\))?\b";
        var matches = Regex.Matches(content, controlPattern);

        foreach (Match match in matches)
        {
            controls.Add(match.Value);
        }

        return controls.Distinct().ToList();
    }

    private double ExtractComplianceScore(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return 0.0;

        // Try to extract percentage scores like "85%", "compliance score: 75%", etc.
        var patterns = new[]
        {
            @"(?:compliance\s+)?score[:\s]+(\d+)%",
            @"(\d+)%\s+compliance",
            @"(\d+)%\s+compliant",
            @"overall\s+score[:\s]+(\d+)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);
            if (match.Success && double.TryParse(match.Groups[1].Value, out var score))
            {
                return score;
            }
        }

        // Default heuristic based on keywords if no explicit score found
        var positiveKeywords = new[] { "compliant", "passed", "approved", "secure", "implemented" };
        var negativeKeywords = new[] { "non-compliant", "failed", "rejected", "insecure", "missing", "gap" };

        var positiveCount = positiveKeywords.Count(k => content.Contains(k, StringComparison.OrdinalIgnoreCase));
        var negativeCount = negativeKeywords.Count(k => content.Contains(k, StringComparison.OrdinalIgnoreCase));

        if (positiveCount == 0 && negativeCount == 0)
            return 70.0; // Default neutral score

        var ratio = (double)positiveCount / Math.Max(positiveCount + negativeCount, 1);
        return Math.Round(ratio * 100, 1);
    }
}
