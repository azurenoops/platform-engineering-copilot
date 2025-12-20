using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.TokenManagement;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.TokenManagement;
using Platform.Engineering.Copilot.Core.Services;
using System.Text.RegularExpressions;
using Platform.Engineering.Copilot.Compliance.Agent.Plugins;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using Platform.Engineering.Copilot.Core.Services.Agents;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Agents;

/// <summary>
/// Specialized agent for compliance assessment, NIST 800-53 controls, and security scanning
/// </summary>
public class ComplianceAgent : ISpecializedAgent
{
    public AgentType AgentType => AgentType.Compliance;

    private readonly Kernel _kernel;
    private readonly IChatCompletionService? _chatCompletion;
    private readonly ILogger<ComplianceAgent> _logger;
    private readonly ComplianceAgentOptions _options;
    private readonly ConfigService _configService;
    private readonly ITokenCounter _tokenCounter;
    private readonly IPromptOptimizer _promptOptimizer;
    private readonly IRagContextOptimizer _ragContextOptimizer;
    private readonly IConversationHistoryOptimizer _conversationHistoryOptimizer;
    private readonly DocumentAgent _documentAgent;

    public ComplianceAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<ComplianceAgent> logger,
        IOptions<ComplianceAgentOptions> options,
        CompliancePlugin compliancePlugin,
        Platform.Engineering.Copilot.Core.Plugins.ConfigurationPlugin configurationPlugin,
        ConfigService configService,
        ITokenCounter tokenCounter,
        IPromptOptimizer promptOptimizer,
        IRagContextOptimizer ragContextOptimizer,
        IConversationHistoryOptimizer conversationHistoryOptimizer,
        DocumentAgent documentAgent)
    {
        _logger = logger;
        _options = options.Value;
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _promptOptimizer = promptOptimizer ?? throw new ArgumentNullException(nameof(promptOptimizer));
        _ragContextOptimizer = ragContextOptimizer ?? throw new ArgumentNullException(nameof(ragContextOptimizer));
        _conversationHistoryOptimizer = conversationHistoryOptimizer ?? throw new ArgumentNullException(nameof(conversationHistoryOptimizer));
        _documentAgent = documentAgent ?? throw new ArgumentNullException(nameof(documentAgent));
        
        // Create specialized kernel for compliance operations
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.Compliance);
        
        // Try to get chat completion service - make it optional for basic functionality
        try
        {
            _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            _logger.LogInformation("✅ Compliance Agent initialized with AI chat completion service");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Compliance Agent initialized without AI chat completion service. AI features will be limited.");
            _chatCompletion = null;
        }

        // Register shared configuration plugin (set_azure_subscription, get_azure_subscription, etc.)
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(configurationPlugin, "ConfigurationPlugin"));
        
        // Register compliance plugin (includes ATO preparation and document generation utilities)
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(compliancePlugin, "CompliancePlugin"));

        _logger.LogInformation("✅ Compliance Agent initialized with specialized kernel (compliance + ATO + document utilities)");
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
            // ====================================================================
            // PHASE 1: Check if this is a document generation request
            // Route to DocumentAgent sub-agent for SSP, SAR, POA&M, verification
            // ====================================================================
            if (IsSSPGenerationRequest(task.Description))
            {
                _logger.LogInformation("📄 Routing SSP generation to DocumentAgent sub-agent");
                
                // Ensure assessment data is available in SharedMemory for DocumentAgent
                var docContext = memory.GetContext(task.ConversationId ?? "default");
                if (docContext?.PreviousResults.Count > 0)
                {
                    var recentAssessments = docContext.PreviousResults
                        .Where(r => r.AgentType == AgentType.Compliance)
                        .TakeLast(1)
                        .Select(r => r.Content)
                        .FirstOrDefault();
                    
                    if (!string.IsNullOrEmpty(recentAssessments))
                    {
                        docContext.WorkflowState["recentAssessment"] = recentAssessments;
                        _logger.LogInformation("📊 Injected recent assessment into WorkflowState for DocumentAgent");
                    }
                }
                
                var documentContent = await _documentAgent.GenerateSSPAsync(task, memory);
                response.Success = true;
                response.Content = documentContent;
                response.Metadata = new Dictionary<string, object>
                {
                    ["documentType"] = "SSP",
                    ["sourceAgent"] = "DocumentAgent",
                    ["generationMethod"] = "AI-Enhanced",
                    ["timestamp"] = DateTime.UtcNow
                };
                response.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                return response;
            }
            else if (IsSARGenerationRequest(task.Description))
            {
                _logger.LogInformation("📄 Routing SAR generation to DocumentAgent sub-agent");
                
                // Ensure assessment data is available in SharedMemory for DocumentAgent
                var docContext = memory.GetContext(task.ConversationId ?? "default");
                if (docContext?.PreviousResults.Count > 0)
                {
                    var recentAssessments = docContext.PreviousResults
                        .Where(r => r.AgentType == AgentType.Compliance)
                        .TakeLast(1)
                        .Select(r => r.Content)
                        .FirstOrDefault();
                    
                    if (!string.IsNullOrEmpty(recentAssessments))
                    {
                        docContext.WorkflowState["recentAssessment"] = recentAssessments;
                        _logger.LogInformation("📊 Injected recent assessment into WorkflowState for DocumentAgent");
                    }
                }
                
                var documentContent = await _documentAgent.GenerateSARAsync(task, memory);
                response.Success = true;
                response.Content = documentContent;
                response.Metadata = new Dictionary<string, object>
                {
                    ["documentType"] = "SAR",
                    ["sourceAgent"] = "DocumentAgent",
                    ["generationMethod"] = "AI-Enhanced",
                    ["timestamp"] = DateTime.UtcNow
                };
                response.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                return response;
            }
            else if (IsPoamGenerationRequest(task.Description))
            {
                _logger.LogInformation("📄 Routing POA&M generation to DocumentAgent sub-agent");
                
                // Ensure assessment data is available in SharedMemory for DocumentAgent
                var docContext = memory.GetContext(task.ConversationId ?? "default");
                if (docContext?.PreviousResults.Count > 0)
                {
                    var recentAssessments = docContext.PreviousResults
                        .Where(r => r.AgentType == AgentType.Compliance)
                        .TakeLast(1)
                        .Select(r => r.Content)
                        .FirstOrDefault();
                    
                    if (!string.IsNullOrEmpty(recentAssessments))
                    {
                        docContext.WorkflowState["recentAssessment"] = recentAssessments;
                        docContext.WorkflowState["recentFindings"] = recentAssessments;
                        _logger.LogInformation("📊 Injected recent assessment into WorkflowState for DocumentAgent");
                    }
                }
                
                var documentContent = await _documentAgent.GeneratePOAMAsync(task, memory);
                response.Success = true;
                response.Content = documentContent;
                response.Metadata = new Dictionary<string, object>
                {
                    ["documentType"] = "POA&M",
                    ["sourceAgent"] = "DocumentAgent",
                    ["generationMethod"] = "AI-Enhanced",
                    ["timestamp"] = DateTime.UtcNow
                };
                response.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                return response;
            }
            else if (IsDocumentVerificationRequest(task.Description))
            {
                _logger.LogInformation("📄 Routing document verification to DocumentAgent sub-agent");
                var verificationResult = await _documentAgent.VerifyDocumentAsync(task, memory);
                response.Success = true;
                response.Content = verificationResult;
                response.Metadata = new Dictionary<string, object>
                {
                    ["validationType"] = "DocumentCompliance",
                    ["sourceAgent"] = "DocumentAgent",
                    ["generationMethod"] = "AI-Validation",
                    ["timestamp"] = DateTime.UtcNow
                };
                response.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                return response;
            }
            
            // ====================================================================
            // PHASE 2: Check if AI services are available for compliance assessment
            // ====================================================================
            if (_chatCompletion == null)
            {
                _logger.LogWarning("⚠️ AI chat completion service not available. Returning basic response for task: {TaskId}", task.TaskId);
                
                response.Success = true;
                response.Content = "AI services not configured. Basic compliance functionality available through database operations only. " +
                                 "Configure Azure OpenAI to enable full AI-powered compliance assessments.";
                response.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                return response;
            }

            // Get conversation context from shared memory
            var context = memory.GetContext(task.ConversationId ?? "default");
            var previousResults = context?.PreviousResults ?? new List<AgentResponse>();

            // Phase 5: Evaluate conversation health and optimize if needed
            if (context.MessageHistory != null && context.MessageHistory.Any())
            {
                var conversationMessages = context.MessageHistory
                    .Select(m => new ConversationMessage
                    {
                        Role = m.Role,
                        Content = m.Content,
                        Timestamp = m.Timestamp
                    })
                    .ToList();

                // Evaluate conversation health
                var health = await EvaluateConversationHealthAsync(
                    conversationMessages, 
                    _conversationHistoryOptimizer, 
                    conversationMessages.Sum(m => _tokenCounter.CountTokens(m.Content)), 
                    8000);

                if (health.NeedsOptimization)
                {
                    _logger.LogInformation(
                        "Compliance Agent - Conversation optimization needed: {HealthStatus}", 
                        health.GetHealthSummary());

                    // Manage context window by getting focused message range
                    var managedMessages = await ManageContextWindowAsync(
                        conversationMessages, 
                        _conversationHistoryOptimizer, 
                        conversationMessages.Count - 1,
                        3500);
                    
                    conversationMessages = managedMessages;
                }
            }

            // Get default subscription from config service
            var defaultSubscriptionId = _configService.GetDefaultSubscription();
            
            // Build subscription info if available
            var subscriptionInfo = !string.IsNullOrEmpty(defaultSubscriptionId)
                ? $@"

**🔧 DEFAULT CONFIGURATION:**
- Default Subscription ID: {defaultSubscriptionId}
- When users don't specify a subscription, automatically use the default subscription ID above
- ALWAYS use the default subscription when available unless user explicitly specifies a different one
"
                : "";

            // Build system prompt for compliance expertise
            var systemPrompt = BuildSystemPrompt(subscriptionInfo);

            // Build user message with context (including deployment metadata from SharedMemory)
            var userMessage = BuildUserMessage(task, previousResults, memory);

            // Create chat history
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userMessage);

            // Phase 5: Optimize conversation history before using it
            if (memory.HasContext(task.ConversationId ?? "default"))
            {
                var conversationContext = memory.GetContext(task.ConversationId ?? "default");
                
                if (conversationContext.MessageHistory != null && conversationContext.MessageHistory.Any())
                {
                    var conversationMessages = conversationContext.MessageHistory
                        .Select(m => new ConversationMessage
                        {
                            Role = m.Role,
                            Content = m.Content,
                            Timestamp = m.Timestamp
                        })
                        .ToList();

                    var optimizedHistory = await OptimizeConversationHistoryAsync(conversationMessages, _conversationHistoryOptimizer, 3500);
                    
                    // Use optimized messages for context
                    var recentHistory = optimizedHistory.Messages
                        .OrderBy(m => m.Timestamp)
                        .TakeLast(Math.Min(5, optimizedHistory.Messages.Count))
                        .ToList();

                    if (recentHistory.Any())
                    {
                        var historyText = string.Join("\n", recentHistory.Select(h => 
                            $"{h.Role}: {h.Content}"));
                        
                        chatHistory.AddUserMessage($@"
**IMPORTANT: Previous conversation context (optimized for this compliance assessment):**
{historyText}

**The current message is a continuation of this conversation. User has ALREADY provided compliance context.**

**DO NOT ask for information the user already provided above. Instead, USE the information for your compliance assessment!**
");
                    }
                }
            }

            // Execute with lower temperature for precision in compliance assessments
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = _options.Temperature,
                MaxTokens = _options.MaxTokens,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            // Phase 5: Optimize prompt to fit token budget before sending to LLM
            var systemPromptText = chatHistory.FirstOrDefault(m => m.Role == Microsoft.SemanticKernel.ChatCompletion.AuthorRole.System)?.Content ?? "";
            var userMessageText = task.Description;
            if (!string.IsNullOrEmpty(systemPromptText) && !string.IsNullOrEmpty(userMessageText))
            {
                var optimizedPrompt = FitPromptInTokenBudget(systemPromptText, userMessageText);
                if (optimizedPrompt.WasOptimized)
                {
                    _logger.LogInformation(
                        "Compliance Agent - Prompt optimized before LLM call: {Strategy}, Tokens saved: {Saved}",
                        optimizedPrompt.OptimizationStrategy, optimizedPrompt.TokensSaved);
                }
            }

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

    private string BuildSystemPrompt(string subscriptionInfo = "")
    {
        return $@"🚨 CRITICAL FUNCTION SELECTION RULES - READ FIRST:

**🔧 AZURE CONTEXT CONFIGURATION:**
- **Azure context configuration (subscription, tenant, authentication settings)**
{subscriptionInfo}

**CONFIGURATION vs ASSESSMENT:**
- If users say ""Use subscription X"", ""Set tenant Y"", ""Set authentication Z"" → **IMMEDIATELY CALL** `set_azure_subscription`, `set_azure_tenant`, or `set_authentication_method` functions (CONFIGURATION)
  - DO NOT just acknowledge - you MUST call the function to actually configure the Azure context
  - Extract the subscription ID/tenant ID from the user's message and pass it to the function
  - Example: ""Use subscription abc-123"" → Call set_azure_subscription(""abc-123"")
  - **CRITICAL**: After calling these functions, return the EXACT function result - DO NOT paraphrase or add commentary
- If users say ""Run assessment"", ""Check compliance"", ""Scan for violations"" → Use compliance assessment functions (ASSESSMENT)

1. If user says: collect evidence, evidence package, generate evidence, gather evidence
   → MUST call collect_evidence function
   
2. If user says: run assessment, scan, check compliance, assess
   → MUST call run_compliance_assessment function

3. If user says: remediation plan, action plan, fix plan, create plan, generate plan, remediation steps, how to fix, prioritized remediation, remediation roadmap
   → MUST call generate_remediation_plan function
   → Recognize phrases like: ""generate a remediation plan for this assessment"", ""create an action plan"", ""show me how to fix these violations""
   → This function analyzes findings and creates prioritized remediation steps with effort estimates

4. DEFAULT SUBSCRIPTION HANDLING:
   → BEFORE asking for subscription, call get_azure_subscription to check if one is configured
   → If subscription exists in config, use it automatically (pass null to functions)
   → ONLY ask for subscription if get_azure_subscription returns no default

DO NOT call run_compliance_assessment when user asks for evidence collection!
DO NOT call collect_evidence when user asks for assessment!
DO NOT ask for subscription without checking get_azure_subscription first!
DO NOT call collect_evidence when user asks for assessment!
DO NOT ask for subscription without checking get_azure_subscription first!

You are a specialized Compliance and Security Assessment expert with deep expertise in:

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
- Pull Request (PR) compliance reviews for IaC changes

**🔍 IMPORTANT: Informational vs Assessment Requests**

DISTINGUISH between different types of requests:

1. **ASSESSMENT REQUESTS** - User wants to see actual findings/results (DEFAULT when saved subscription exists):
   
   Examples:
   - ""Run a compliance assessment"" ← MOST COMMON
   - ""Run assessment"" ← MOST COMMON
   - ""Assess my subscription"" ← MOST COMMON
   - ""Get control family details for CM""
   - ""Show me CM findings""
   - ""Get AC control family details""
   - ""Check compliance for my subscription""
   - ""Scan subscription XYZ for NIST compliance""
   - ""Assess my resource group""
   
   **🔴 CRITICAL LOGIC - ALWAYS FOLLOW:**
   
   A) **For general assessment requests** (""run assessment"", ""run compliance assessment""):
      - IF user says ""run assessment"" or similar WITHOUT specifying a subscription name/ID
      - THEN immediately call run_compliance_assessment() with NO parameters (subscriptionIdOrName=null)
      - The function will automatically use the saved default subscription from config
      - DO NOT ask ""which subscription?"" - just call the function!
      - ONLY ask for subscription if the function returns an error saying no default is configured
   
   B) **For control family queries** (""get CM details"", ""show AC findings""):
      - IF user mentions a control family code (CM, AC, AU, etc.) AND there's a saved subscription ID in context
      - THEN call get_control_family_details with the saved subscription ID
      - DO NOT ask for subscription details - use the saved one automatically!
   
   C) **For explicit subscription requests** (""run assessment for production""):
      - IF user explicitly mentions a subscription name or ID
      - THEN pass that value to the appropriate function
   
   D) **Only ask for subscription if:**
      - No default subscription is configured (function returns error)
      - User request is ambiguous and needs clarification
   
   For these: Use the saved subscription from context (shown in ""SAVED CONTEXT FROM PREVIOUS ACTIVITY"" above)

2. **PULL REQUEST REVIEW REQUESTS** - User wants to review PR for compliance violations:
   
   Examples:
   - ""Review pull request #42 in myorg/myrepo""
   - ""Check PR for compliance issues""
   - ""Scan this PR for IaC violations""
   - ""Run compliance review on GitHub PR""
   
   For these: Inform user that automated PR reviews are available via the PullRequestReviewPlugin.
   This capability scans Bicep, Terraform, ARM templates, and Kubernetes YAML for NIST/STIG/DoD violations.
   Phase 1 compliant: Advisory only, no auto-merge.

3. **INFORMATIONAL QUERIES** - User wants to LEARN general concepts (NO assessment needed):
   
   Examples:
   - ""What is the CM control family?"" (note: ""what is"")
   - ""Tell me about the AC-2 control""
   - ""Explain NIST 800-53 framework""
   - ""What does Configuration Management cover?""
   - ""Describe the IA controls""
   
   For these: Provide information using the KnowledgeBase Agent via the KnowledgeBasePlugin.
   Only provide general knowledge from the reference section below.

**🚫 DO NOT USE CONVERSATIONAL GATHERING FOR ASSESSMENT REQUESTS**

⚠️ **CRITICAL**: When user says ""run assessment"" or ""run compliance assessment"":
- DO NOT ask conversational questions about subscription
- DO NOT gather requirements through conversation
- IMMEDIATELY call run_compliance_assessment(subscriptionIdOrName=null)
- Let the FUNCTION handle missing subscription (it will use default from config or return error)
- Only if function returns error about missing subscription, THEN ask user

**Exception**: Only ask clarifying questions if:
- User request is genuinely ambiguous (e.g., ""check something"")
- Function returned an error requiring user input
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

**If default subscription IS configured:**
User: ""Run a compliance assessment""
You: **[IMMEDIATELY call run_compliance_assessment with the default subscription - use NIST 800-53 as default framework]**
- DO NOT ask which subscription - use the default from configuration
- Only ask clarifying questions if user wants something specific (different framework, specific resource group, etc.)

**If NO default subscription is configured:**
User: ""Run a compliance assessment""
You: ""I'd be happy to run a compliance assessment! I don't have a default subscription configured yet.

Please provide a subscription ID, or say 'Set subscription <id>' to configure a default for future assessments.""

User: ""Set subscription 453c2549-...""
You: **[IMMEDIATELY call set_azure_subscription function, then proceed with compliance assessment]**

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

**🔴 CRITICAL: PARAMETER HANDLING WITH DEFAULT SUBSCRIPTION**

1. **Subscription ID or Name**
   - **IF default subscription IS configured**: Use it automatically - DO NOT ask!
   - **IF NO default subscription**: Ask user to provide one or set a default
   - DO NOT make assumptions or use placeholder values

2. **Scan Scope** (use smart defaults)
   - Default to entire subscription scan unless user specifies otherwise
   - If user mentions specific resources or resource groups, scope to those

3. **Resource Group Name** (only if specifically requested)
   - First check SharedMemory deployment metadata for recently created resource groups
   - If found in SharedMemory, use it automatically for newly provisioned resource scans

**Example Conversation Flow - WITH Default Subscription:**
User: ""Run a compliance assessment""
You: **[IMMEDIATELY call run_compliance_assessment - the function will use the configured default subscription]**

**Example Conversation Flow - WITHOUT Default Subscription:**
User: ""Run a compliance assessment""
You: ""I don't have a default subscription configured. Please provide a subscription ID, or say 'Set subscription <id>' to configure one.""

**CRITICAL: Subscription ID Handling**
When you DO have a subscription ID (from default config or conversation):
- Use the default subscription from configuration automatically
- Look for subscription IDs in the conversation history or shared memory
- Extract subscription IDs from previous agent responses (look for GUIDs like '00000000-0000-0000-0000-000000000000')
- If a task mentions 'newly-provisioned-resources' or 'newly-provisioned-acr', use the subscription ID from the ORIGINAL user request
- DO NOT pass resource descriptions (like 'newly-provisioned-acr') as subscription IDs
- DO NOT ask for subscription if one is already configured

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
When the user asks to ""generate a remediation plan"" or ""create a remediation plan"" or similar:
- IMMEDIATELY call the generate_remediation_plan function
- The function accepts optional subscription ID/name (if not provided, uses last assessed subscription automatically)
- Common user phrases that trigger this: ""remediation plan"", ""action plan"", ""fix plan"", ""how to fix"", ""create plan""
- Example user requests:
  * ""generate a remediation plan for this assessment""
  * ""create an action plan to fix these violations""
  * ""show me remediation steps""
  * ""I need a prioritized fix plan""
- The function will return prioritized violations with detailed remediation steps and effort estimates
- If user just completed an assessment, they likely want a remediation plan based on those findings

**Response Format:**
When assessing compliance:
1. **IF** the function response contains 'formatted_output': Return it EXACTLY as provided
2. **OTHERWISE**: Follow this format:
   - List applicable NIST 800-53 controls
   - Provide compliance score (0-100%)
   - Identify gaps and findings
   - Recommend remediation steps
   - Estimate effort and timeline

Always provide clear, actionable assessments with specific control references.

**📚 NIST 800-53 CONTROL FAMILY REFERENCE**

When users ask for informational details about control families (without mentioning a subscription), provide this knowledge:

**CM (Configuration Management)**
- Purpose: Establish and maintain baseline configurations, track changes, and ensure system integrity
- Key Controls:
  - CM-2: Baseline Configuration
  - CM-3: Configuration Change Control
  - CM-6: Configuration Settings
  - CM-7: Least Functionality
  - CM-8: Information System Component Inventory
- Azure Implementation: Configuration baselines, Azure Policy, Resource tags, Change tracking, Inventory management
- Common Findings: Missing baseline configurations, undocumented changes, unapproved software, missing inventories

**AC (Access Control)**
- Purpose: Limit system access to authorized users, processes, and devices
- Key Controls:
  - AC-2: Account Management
  - AC-3: Access Enforcement
  - AC-4: Information Flow Enforcement
  - AC-6: Least Privilege
  - AC-17: Remote Access
- Azure Implementation: Azure AD, RBAC, Conditional Access, MFA, PIM
- Common Findings: Overprivileged accounts, missing MFA, excessive permissions

**AU (Audit and Accountability)**
- Purpose: Create, protect, and retain audit records to enable monitoring, analysis, investigation
- Key Controls:
  - AU-2: Audit Events
  - AU-6: Audit Review, Analysis, and Reporting
  - AU-9: Protection of Audit Information
  - AU-12: Audit Generation
- Azure Implementation: Azure Monitor, Log Analytics, Activity Logs, diagnostic settings
- Common Findings: Missing diagnostic logs, insufficient log retention, no log monitoring

**IA (Identification and Authentication)**
- Purpose: Verify identity of users, processes, and devices as prerequisite to system access
- Key Controls:
  - IA-2: Identification and Authentication
  - IA-4: Identifier Management
  - IA-5: Authenticator Management
  - IA-8: Identification and Authentication (Non-Organizational Users)
- Azure Implementation: Azure AD, MFA, SSO, B2B, Managed Identities
- Common Findings: Weak passwords, missing MFA, shared accounts

**SC (System and Communications Protection)**
- Purpose: Monitor, control, and protect communications at system boundaries and internal key points
- Key Controls:
  - SC-7: Boundary Protection
  - SC-8: Transmission Confidentiality and Integrity
  - SC-12: Cryptographic Key Establishment and Management
  - SC-13: Cryptographic Protection
- Azure Implementation: NSGs, Firewalls, TLS/SSL, Key Vault, encryption
- Common Findings: Unencrypted connections, weak TLS versions, open ports

**SI (System and Information Integrity)**
- Purpose: Identify, report, and correct information and system flaws in timely manner
- Key Controls:
  - SI-2: Flaw Remediation
  - SI-3: Malicious Code Protection
  - SI-4: Information System Monitoring
  - SI-7: Software, Firmware, and Information Integrity
- Azure Implementation: Update Management, Defender for Cloud, Security Center, vulnerability scanning
- Common Findings: Missing patches, no antimalware, missing integrity verification

For other families (CP, IR, RA, CA, etc.), provide similar structured information about their purpose, key controls, and Azure implementation.
";
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

        // 🔥 CHECK CONVERSATION CONTEXT FOR SAVED SUBSCRIPTION ID
        var conversationId = task.ConversationId ?? "default";
        var context = memory.GetContext(conversationId);
        
        // Add saved subscription ID from previous scan if available
        if (context?.WorkflowState != null && context.WorkflowState.TryGetValue("lastSubscriptionId", out var lastSubId) && lastSubId != null)
        {
            message += "🔍 SAVED CONTEXT FROM PREVIOUS ACTIVITY:\n";
            message += $"- Last Scanned Subscription: {lastSubId}\n";
            
            if (context.WorkflowState.TryGetValue("lastScanTimestamp", out var timestamp) && timestamp is DateTime scanTime)
            {
                var elapsed = DateTime.UtcNow - scanTime;
                message += $"- Last Scan: {elapsed.TotalMinutes:F0} minutes ago\n";
            }
            
            message += "\n⚠️ IMPORTANT: If the user's request is about the same subscription (e.g., 'get CM details', 'what control families'), ";
            message += $"you can use subscription ID '{lastSubId}' WITHOUT asking the user again!\n";
            message += "Only ask for subscription details if the user is clearly asking about a DIFFERENT subscription.\n\n";
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

        // Try to extract subscriptionId from the content (JSON response from plugins)
        try
        {
            if (!string.IsNullOrEmpty(result.Content))
            {
                // Look for subscription ID in JSON responses
                var subIdMatch = Regex.Match(result.Content, @"""subscriptionId"":\s*""([a-f0-9-]{36})""", RegexOptions.IgnoreCase);
                if (subIdMatch.Success)
                {
                    metadata["subscriptionId"] = subIdMatch.Groups[1].Value;
                    _logger.LogInformation("📌 Extracted subscription ID from response: {SubId}", subIdMatch.Groups[1].Value);
                }
                
                // Also look for it in markdown format
                var markdownMatch = Regex.Match(result.Content, @"\*\*Subscription:\*\*\s*`([a-f0-9-]{36})`", RegexOptions.IgnoreCase);
                if (markdownMatch.Success && !metadata.ContainsKey("subscriptionId"))
                {
                    metadata["subscriptionId"] = markdownMatch.Groups[1].Value;
                    _logger.LogInformation("📌 Extracted subscription ID from markdown: {SubId}", markdownMatch.Groups[1].Value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract subscription ID from response");
        }

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

    /// <summary>
    /// Helper method to optimize search results for RAG context
    /// </summary>
    private OptimizedRagContext OptimizeSearchResults(
        List<string> results,
        string query,
        int maxTokens = 1500)
    {
        var ranked = new List<RankedSearchResult>();

        foreach (var result in results)
        {
            if (string.IsNullOrEmpty(result))
                continue;

            var relevanceScore = CalculateRelevanceScore(query, result);
            ranked.Add(new RankedSearchResult
            {
                Content = result,
                RelevanceScore = relevanceScore,
                TokenCount = 0,
                Metadata = new Dictionary<string, object> { { "query", query } }
            });
        }

        var options = new RagOptimizationOptions
        {
            MaxRagTokens = maxTokens,
            MinRelevanceScore = 0.3,
            MaxResults = 10,
            TrimLargeResults = true
        };

        return _ragContextOptimizer.OptimizeContext(ranked, options);
    }

    /// <summary>
    /// Calculate relevance score based on keyword matching
    /// </summary>
    private double CalculateRelevanceScore(string query, string content, string? title = null)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(content))
            return 0.0;

        var queryWords = query.ToLower()
            .Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToList();

        if (queryWords.Count == 0)
            return 0.0;

        var contentLower = content.ToLower();
        var titleLower = title?.ToLower() ?? string.Empty;

        int contentMatches = queryWords.Count(w => contentLower.Contains(w));
        double contentScore = (double)contentMatches / queryWords.Count;

        double titleScore = 0.0;
        if (!string.IsNullOrEmpty(titleLower))
        {
            int titleMatches = queryWords.Count(w => titleLower.Contains(w));
            titleScore = (titleMatches * 0.15);
        }

        double score = contentScore + titleScore;
        return Math.Min(1.0, Math.Max(0.0, score));
    }

    /// <summary>
    /// Helper method to fit a prompt within token budget using optimization
    /// </summary>
    private OptimizedPrompt FitPromptInTokenBudget(
        string systemPrompt,
        string userMessage,
        List<string>? ragContext = null,
        List<string>? conversationHistory = null)
    {
        ragContext ??= new List<string>();
        conversationHistory ??= new List<string>();

        var options = BuildPromptOptimizationOptions();
        var optimized = _promptOptimizer.OptimizePrompt(
            systemPrompt,
            userMessage,
            ragContext,
            conversationHistory,
            options);

        if (optimized.WasOptimized)
        {
            _logger.LogInformation("Prompt optimization applied: {Strategy}", optimized.OptimizationStrategy);
            _logger.LogInformation("Tokens saved: {TokensSaved}", optimized.TokensSaved);
        }

        return optimized;
    }

    /// <summary>
    /// Helper method to build prompt optimization options
    /// </summary>
    private PromptOptimizationOptions BuildPromptOptimizationOptions()
    {
        return new PromptOptimizationOptions
        {
            ModelName = "gpt-4o",
            MaxContextWindow = 128000,
            TargetTokenCount = 0,
            ReservedCompletionTokens = 4000,
            SystemPromptPriority = 100,
            UserMessagePriority = 100,
            RagContextPriority = 90,
            ConversationHistoryPriority = 50,
            MinRagContextItems = 5,
            MinConversationHistoryMessages = 1,
            SafetyBufferPercentage = 10,
            UseSummarization = true
        };
    }

    /// <summary>
    /// Helper method to calculate and record agent cost metrics
    /// </summary>
    private async Task RecordAgentCostAsync(
        OptimizedPrompt optimizedPrompt,
        int completionTokens,
        string taskId,
        string conversationId)
    {
        try
        {
            var metrics = new AgentCostMetrics
            {
                AgentType = AgentType.Compliance.ToString(),
                TaskId = taskId,
                ConversationId = conversationId,
                Timestamp = DateTime.UtcNow,
                OriginalPromptTokens = optimizedPrompt.OriginalEstimate?.TotalInputTokens ?? 0,
                OptimizedPromptTokens = optimizedPrompt.OptimizedEstimate?.TotalInputTokens ?? 0,
                TokensSaved = optimizedPrompt.TokensSaved,
                OptimizationPercentage = optimizedPrompt.OriginalEstimate?.TotalInputTokens > 0
                    ? (optimizedPrompt.TokensSaved * 100.0 / optimizedPrompt.OriginalEstimate.TotalInputTokens)
                    : 0,
                CompletionTokens = completionTokens,
                TotalTokens = (optimizedPrompt.OptimizedEstimate?.TotalInputTokens ?? 0) + completionTokens,
                Model = "gpt-4o",
                WasOptimized = optimizedPrompt.WasOptimized,
                OptimizationStrategy = optimizedPrompt.OptimizationStrategy,
                RagContextItems = optimizedPrompt.OriginalEstimate?.RagContextItemTokens.Count ?? 0,
                RagContextItemsAfterOptimization = optimizedPrompt.RagContext.Count,
                ConversationHistoryMessages = optimizedPrompt.ConversationHistory.Count
            };

            // Calculate cost (GPT-4o pricing: ~$0.03 per 1K prompt tokens, ~$0.06 per 1K completion tokens)
            var promptCost = (metrics.OptimizedPromptTokens / 1000.0) * 0.03;
            var completionCost = (completionTokens / 1000.0) * 0.06;
            metrics.EstimatedCost = promptCost + completionCost;

            // Calculate original cost for comparison
            var originalPromptCost = (metrics.OriginalPromptTokens / 1000.0) * 0.03;
            metrics.CostSaved = originalPromptCost - promptCost;

            _logger.LogInformation("Compliance Agent - Cost metrics recorded: {Summary}", metrics.GetSummary());

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record agent cost metrics");
        }
    }

    #region Document Type Detection

    /// <summary>
    /// Detects if task is requesting SSP (System Security Plan) generation
    /// </summary>
    private bool IsSSPGenerationRequest(string description) =>
        description.Contains("SSP", StringComparison.OrdinalIgnoreCase) ||
        description.Contains("System Security Plan", StringComparison.OrdinalIgnoreCase) ||
        description.Contains("generate SSP", StringComparison.OrdinalIgnoreCase) ||
        description.Contains("create SSP", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Detects if task is requesting SAR (Security Assessment Report) generation
    /// </summary>
    private bool IsSARGenerationRequest(string description) =>
        description.Contains("SAR", StringComparison.OrdinalIgnoreCase) ||
        description.Contains("Security Assessment Report", StringComparison.OrdinalIgnoreCase) ||
        description.Contains("assessment report", StringComparison.OrdinalIgnoreCase) ||
        description.Contains("generate SAR", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Detects if task is requesting POA&M (Plan of Action & Milestones) generation
    /// </summary>
    private bool IsPoamGenerationRequest(string description) =>
        description.Contains("POA&M", StringComparison.OrdinalIgnoreCase) ||
        description.Contains("POAM", StringComparison.OrdinalIgnoreCase) ||
        description.Contains("Plan of Action", StringComparison.OrdinalIgnoreCase) ||
        description.Contains("action plan", StringComparison.OrdinalIgnoreCase) ||
        description.Contains("remediation plan", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Detects if task is requesting document verification/validation
    /// </summary>
    private bool IsDocumentVerificationRequest(string description) =>
        description.Contains("verify", StringComparison.OrdinalIgnoreCase) ||
        description.Contains("validate", StringComparison.OrdinalIgnoreCase) ||
        (description.Contains("check", StringComparison.OrdinalIgnoreCase) &&
         (description.Contains("document", StringComparison.OrdinalIgnoreCase) ||
          description.Contains("compliance", StringComparison.OrdinalIgnoreCase))) ||
        (description.Contains("document", StringComparison.OrdinalIgnoreCase) &&
         (description.Contains("complian", StringComparison.OrdinalIgnoreCase) ||
          description.Contains("standard", StringComparison.OrdinalIgnoreCase)));

    #endregion

    #region Conversation History Optimization

    /// <summary>
    /// Helper method to optimize conversation history for context window management
    /// </summary>
    private async Task<OptimizedConversationHistory> OptimizeConversationHistoryAsync(
        List<ConversationMessage> messages,
        IConversationHistoryOptimizer historyOptimizer,
        int tokenBudget = 3500)
    {
        try
        {
            var options = historyOptimizer.GetRecommendedOptionsForAgent("Compliance");
            options.MaxTokens = Math.Min(tokenBudget, options.MaxTokens);

            var optimized = await historyOptimizer.OptimizeHistoryAsync(messages, options);
            
            return optimized;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to optimize conversation history");
            return new OptimizedConversationHistory { Messages = messages };
        }
    }

    /// <summary>
    /// Helper method to evaluate conversation health and determine if pruning is needed
    /// </summary>
    private async Task<ConversationHealthMetrics> EvaluateConversationHealthAsync(
        List<ConversationMessage> messages,
        IConversationHistoryOptimizer historyOptimizer,
        int currentTokenCount,
        int tokenBudget = 7000)
    {
        try
        {
            var health = await historyOptimizer.EvaluateConversationHealthAsync(
                messages, 
                currentTokenCount, 
                tokenBudget);

            _logger.LogDebug("Compliance Agent - Conversation health evaluated:\n{Summary}", 
                health.GetHealthSummary());

            return health;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate conversation health");
            return new ConversationHealthMetrics { TotalMessages = messages.Count };
        }
    }

    /// <summary>
    /// Helper method to manage context window for long-running conversations
    /// </summary>
    private async Task<List<ConversationMessage>> ManageContextWindowAsync(
        List<ConversationMessage> messages,
        IConversationHistoryOptimizer historyOptimizer,
        int targetMessageIndex,
        int maxTokens = 3500)
    {
        try
        {
            var contextWindow = await historyOptimizer.GetContextWindowAsync(
                messages,
                maxTokens,
                targetMessageIndex);

            _logger.LogDebug("Compliance Agent - Context window managed: {TargetIndex} → {WindowSize} messages", 
                targetMessageIndex, contextWindow.Count);

            return contextWindow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to manage context window");
            return messages;
        }
    }

    #endregion
}
