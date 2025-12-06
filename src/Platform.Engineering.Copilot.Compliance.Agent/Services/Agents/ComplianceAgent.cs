using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Models.Agents;
using System.Text.RegularExpressions;
using Platform.Engineering.Copilot.Compliance.Agent.Plugins;
using Platform.Engineering.Copilot.Compliance.Agent.Plugins.ATO;
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

    public ComplianceAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<ComplianceAgent> logger,
        IOptions<ComplianceAgentOptions> options,
        CompliancePlugin compliancePlugin,
        DocumentGenerationPlugin documentGenerationPlugin,
        Platform.Engineering.Copilot.Core.Plugins.ConfigurationPlugin configurationPlugin)
    {
        _logger = logger;
        _options = options.Value;
        
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
        
        // Register compliance plugin
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(compliancePlugin, "CompliancePlugin"));
        
        // Register document generation plugin for control narratives, SSP, SAR, POA&M generation
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(documentGenerationPlugin, "DocumentGenerationPlugin"));

        _logger.LogInformation("✅ Compliance Agent initialized with specialized kernel (compliance + document generation)");
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
            // Check if AI services are available
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
                Temperature = _options.Temperature,
                MaxTokens = _options.MaxTokens,
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
        return @"🚨 CRITICAL FUNCTION SELECTION RULES - READ FIRST:

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
     - A subscription GUID (e.g., '00000000-0000-0000-0000-000000000000')""
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
- Extract subscription IDs from previous agent responses (look for GUIDs like '00000000-0000-0000-0000-000000000000')
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
}
