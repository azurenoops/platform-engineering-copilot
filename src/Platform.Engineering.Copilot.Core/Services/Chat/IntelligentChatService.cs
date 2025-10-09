using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Platform.Engineering.Copilot.Core.Contracts;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;
using Platform.Engineering.Copilot.Core.Services.Cache;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// AI-powered intelligent chat service using Azure OpenAI and Semantic Kernel
/// Provides intent classification, tool chaining, and proactive suggestions with caching
/// 
/// NOTE: This is the V1 implementation with manual intent classification and routing.
/// V2 (IntelligentChatService_v2) uses SK auto-calling and is now the default.
/// This class is kept for reference and potential rollback.
/// </summary>
[Obsolete("Use IntelligentChatService_v2 which leverages SK automatic function calling. This V1 is kept for reference only.")]
public class IntelligentChatService : IIntelligentChatService
{
    private readonly ISemanticKernelService _semanticKernel;
    private readonly IToolRouter _toolRouter;
    private readonly ILogger<IntelligentChatService> _logger;
    private readonly Kernel _kernel;
    private readonly IIntelligentChatCacheService? _cacheService;
    
    // In-memory conversation store (replace with distributed cache in production)
    private static readonly Dictionary<string, ConversationContext> _conversations = new();
    private static readonly object _conversationLock = new();

    public IntelligentChatService(
        ISemanticKernelService semanticKernel,
        IToolRouter toolRouter,
        Kernel kernel,
        ILogger<IntelligentChatService> logger,
        IIntelligentChatCacheService? cacheService = null)
    {
        _semanticKernel = semanticKernel;
        _toolRouter = toolRouter;
        _kernel = kernel;
        _logger = logger;
        _cacheService = cacheService;
    }

    /// <summary>
    /// Process a user message with AI-powered intent classification
    /// </summary>
    public async Task<IntelligentChatResponse> ProcessMessageAsync(
        string message,
        string conversationId,
        ConversationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Processing message for conversation {ConversationId}", conversationId);

            // Get or create conversation context
            context ??= await GetOrCreateContextAsync(conversationId, cancellationToken: cancellationToken);

            // Update context with user message
            var userMessage = new MessageSnapshot
            {
                Role = "user",
                Content = message,
                Timestamp = DateTime.UtcNow
            };
            await UpdateContextAsync(context, userMessage, cancellationToken);

            // Classify intent using AI
            var intent = await ClassifyIntentAsync(message, context, cancellationToken);

            // OVERRIDE: Check for active onboarding session and route to provide_info instead of start_onboarding
            if (!string.IsNullOrEmpty(context.ActiveWorkflow) && 
                context.ActiveWorkflow.EndsWith("_onboarding") &&
                intent.ToolName?.EndsWith("_start_onboarding") == true)
            {
                // User is in an active onboarding session but AI routed to start_onboarding
                // Override to use provide_info instead
                var workflowPrefix = context.ActiveWorkflow.Replace("_onboarding", "");
                var originalToolName = intent.ToolName;
                
                intent.ToolName = $"{workflowPrefix}_provide_info";
                
                // Extract conversationId, requestId, and userEmail from context
                intent.Parameters ??= new Dictionary<string, object?>();
                intent.Parameters["conversationId"] = conversationId;
                
                if (context.WorkflowState.TryGetValue("requestId", out var requestIdObj))
                {
                    intent.Parameters["requestId"] = requestIdObj?.ToString();
                }
                
                if (context.WorkflowState.TryGetValue("userEmail", out var userEmailObj))
                {
                    intent.Parameters["userEmail"] = userEmailObj?.ToString();
                }
                
                // Use userInput parameter to pass the full user message for bulk extraction
                intent.Parameters["userInput"] = message;
                
                _logger.LogInformation(
                    "Active onboarding session detected. Overriding {OriginalTool} → {NewTool} for conversation {ConversationId}",
                    originalToolName,
                    intent.ToolName,
                    conversationId);
            }

            var response = new IntelligentChatResponse
            {
                ConversationId = conversationId,
                Intent = intent,
                Context = context
            };

            // Check if we need to ask for missing parameters before executing tools
            if (intent.RequiresFollowUp && !string.IsNullOrEmpty(intent.FollowUpPrompt))
            {
                // Don't execute the tool yet - ask for missing parameters first
                response.ToolExecuted = false;
                response.RequiresFollowUp = true;
                response.FollowUpPrompt = intent.FollowUpPrompt;
                response.Response = intent.FollowUpPrompt;
                
                _logger.LogInformation(
                    "Tool execution deferred for conversation {ConversationId}. Asking for missing parameters: {FollowUpPrompt}", 
                    conversationId, 
                    intent.FollowUpPrompt);
                
                return response;
            }

            // Handle based on intent type
            if (intent.RequiresToolChain)
            {
                // Execute multi-step tool chain
                response.ToolChainResult = await ExecuteToolChainAsync(
                    intent.ToolChain, 
                    conversationId, 
                    cancellationToken);
                response.ToolExecuted = true;
                response.Response = GenerateToolChainResponse(response.ToolChainResult);
            }
            else if (intent.RequiresTool)
            {
                // Ensure conversationId is always included in tool parameters
                intent.Parameters ??= new Dictionary<string, object?>();
                if (!intent.Parameters.ContainsKey("conversationId"))
                {
                    intent.Parameters["conversationId"] = conversationId;
                }
                
                // For onboarding provide_info tools, ensure userInput is included
                if (intent.ToolName?.EndsWith("_provide_info") == true && 
                    !intent.Parameters.ContainsKey("userInput") &&
                    !string.IsNullOrEmpty(message))
                {
                    intent.Parameters["userInput"] = message;
                    _logger.LogInformation(
                        "Injecting userInput into {ToolName} for message processing",
                        intent.ToolName);
                }
                
                // For onboarding start_onboarding tools, ensure initialInfo is included
                if (intent.ToolName?.EndsWith("_start_onboarding") == true && 
                    !intent.Parameters.ContainsKey("initialInfo") &&
                    !string.IsNullOrEmpty(message))
                {
                    intent.Parameters["initialInfo"] = message;
                    _logger.LogInformation(
                        "Injecting initialInfo into {ToolName} for bulk field extraction",
                        intent.ToolName);
                }
                
                // Execute single tool
                var toolResult = await _toolRouter.RouteToolCallAsync(
                    intent.ToolCall!, 
                    cancellationToken);
                response.ToolExecuted = true;
                response.ToolResult = toolResult;
                
                // Update context if tool result contains onboarding session metadata
                if (toolResult.IsSuccess && toolResult.Content != null)
                {
                    try
                    {
                        // Check if the result is a dictionary with onboarding metadata
                        if (toolResult.Content is Dictionary<string, object?> resultDict)
                        {
                            if (resultDict.TryGetValue("_activeWorkflow", out var activeWorkflow) && activeWorkflow != null)
                            {
                                context.ActiveWorkflow = activeWorkflow.ToString();
                                _logger.LogInformation(
                                    "Set active workflow to {ActiveWorkflow} for conversation {ConversationId}",
                                    context.ActiveWorkflow,
                                    conversationId);
                            }
                            
                            if (resultDict.TryGetValue("_workflowState", out var workflowState) && 
                                workflowState is Dictionary<string, object?> stateDict)
                            {
                                foreach (var kvp in stateDict)
                                {
                                    context.WorkflowState[kvp.Key] = kvp.Value;
                                }
                                _logger.LogInformation(
                                    "Updated workflow state for conversation {ConversationId}",
                                    conversationId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to extract workflow metadata from tool result");
                    }
                }
                
                response.Response = await GenerateResponseWithContextAsync(
                    message, 
                    toolResult, 
                    intent, 
                    cancellationToken);
            }
            else
            {
                // Conversational or informational response
                response.Response = await _semanticKernel.ProcessQueryAsync(
                    message, 
                    BuildContextString(context), 
                    cancellationToken);
            }

            // Generate proactive suggestions
            response.Suggestions = await GenerateProactiveSuggestionsAsync(
                conversationId, 
                context, 
                cancellationToken);

            // Handle follow-up
            response.RequiresFollowUp = intent.RequiresFollowUp;
            response.FollowUpPrompt = intent.FollowUpPrompt;

            // Update metadata
            stopwatch.Stop();
            response.Metadata.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            response.Metadata.ModelUsed = "gpt-4";

            // Store assistant response in context
            var assistantMessage = new MessageSnapshot
            {
                Role = "assistant",
                Content = response.Response,
                IntentType = intent.IntentType,
                ToolExecuted = response.ToolExecuted ? intent.ToolName : null,
                Timestamp = DateTime.UtcNow,
                PartOfChain = intent.RequiresToolChain
            };
            await UpdateContextAsync(context, assistantMessage, cancellationToken);

            _logger.LogInformation(
                "Processed message in {ElapsedMs}ms. Intent: {IntentType}, Tool: {ToolName}", 
                stopwatch.ElapsedMilliseconds, 
                intent.IntentType, 
                intent.ToolName ?? "none");

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message for conversation {ConversationId}", conversationId);
            throw;
        }
    }

    /// <summary>
    /// Classify user intent using Azure OpenAI via Semantic Kernel with caching
    /// </summary>
    public async Task<IntentClassificationResult> ClassifyIntentAsync(
        string message,
        ConversationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get cached result first
            if (_cacheService != null)
            {
                var cacheKey = _cacheService.GenerateCacheKey("intent", message, context?.ConversationId ?? "default");
                var cachedResult = await _cacheService.GetAsync<IntentClassificationResult>(cacheKey, cancellationToken);
                
                if (cachedResult != null)
                {
                    _logger.LogWarning("⚠️ Using CACHED intent classification: {IntentType}, Tool: {ToolName}, Message: {Message}", 
                        cachedResult.IntentType,
                        cachedResult.ToolName ?? "none",
                        message.Length > 80 ? message.Substring(0, 80) + "..." : message);
                    return cachedResult;
                }
                else
                {
                    _logger.LogInformation("No cached result found, performing fresh intent classification");
                }
            }

            var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();

            // System prompt for intent classification
            history.AddSystemMessage(@"You are an AI assistant that classifies user intents for a cloud platform management system.
You MUST respond with valid JSON ONLY - no markdown code blocks, no backticks, no other text. Return ONLY the raw JSON object.

🚨 CRITICAL RULE #1: If user message contains words like 'deploy', 'create', 'provision', 'build', 'set up' AND mentions Azure resources (web app, database, SQL, storage, blob, AKS, VM, container), then intentType MUST be 'tool_execution' with toolName 'infrastructure_provisioning'. This is NEVER 'conversational'!
🚨 CRITICAL RULE #2: Check for Navy Flankspeed onboarding keywords FIRST before other intents!
                     Keywords that trigger flankspeed_start_onboarding: onboard, onboarding, flankspeed, new mission, request access, need subscription

Analyze the user's message and determine:
1. Intent type: 'tool_execution', 'information_request', or 'conversational'
2. If tool_execution: which tool to use and what parameters
3. If multi-step workflow is needed, identify the tool chain
4. Confidence score (0.0 to 1.0)

Available tools and their use cases:
- ato_compliance: Use for ALL ATO compliance operations via natural language queries (assessments, monitoring, evidence collection, remediation, risk assessment, timeline, certificates)
- document_upload_analyze: Use ONLY for document analysis (uploading/analyzing SSP, architecture diagrams, security plan documents)
- analyze_azure_costs: Use for analyzing Azure costs (historical analysis, breakdowns, trends, forecasting)
- optimize_costs: Use for cost optimization recommendations and implementing cost-saving measures
- configure_budget_alerts: Use for setting up budget alerts and spending notifications
- flankspeed_start_onboarding: Use for starting Navy Flankspeed mission owner onboarding conversations
- flankspeed_provide_info: Use during active onboarding to provide mission details, technical requirements, compliance needs. Also handles 'review' and 'submit' commands when in Review step
- flankspeed_cancel_onboarding: Use to cancel an active Navy Flankspeed onboarding conversation
- infrastructure_provisioning: Use for ALL infrastructure operations via natural language queries (creating resources, generating templates, validation, deployment, resource queries)
- schedule_compliance_scan: Use for scheduling recurring compliance scans
- apply_security_hardening: Use for applying security hardening to Azure resources (encryption, networking, authentication, RBAC, logging)
- security_baseline_assessment: Use for assessing current security posture against best practices
- setup_incident_response: Use for setting up security incident response (SIEM, analytics rules, automation, playbooks)
- configure_sentinel: Use for configuring Microsoft Sentinel workspace and data sources
- azure_metrics: Use for Azure Monitor metrics
- service_templates: Use for service template management
- environment_management: Use for Azure environment management
- developer_workflow: Use for CI/CD and deployment workflows
- infrastructure_as_code: Use for Terraform/Bicep operations
- test_framework: Use for running tests
- azure_resource_discovery: Use for ALL Azure resource discovery operations via natural language queries (listing subscriptions, discovering resources, listing resource groups)
- security_scanning: Use for ALL security scanning operations via natural language queries (container scanning, code scanning, IaC scanning, GitHub repository security analysis)

CRITICAL CLASSIFICATION RULES - FOLLOW EXACTLY:

**** HIGHEST PRIORITY - CHECK FIRST ****
NAVY FLANKSPEED ONBOARDING - These rules override ALL other rules:
**IMPORTANT**: Onboarding is ONLY for first-time Mission Owner setup to get Flankspeed access. NOT for deploying infrastructure or creating resources.

1. Use ""flankspeed_start_onboarding"" ONLY when user explicitly wants to START/BEGIN their FIRST-TIME Flankspeed access:
   - ""I need to onboard to Flankspeed"" OR ""start onboarding"" OR ""begin onboarding""
   - ""onboard a new mission to Flankspeed"" OR ""request Flankspeed access"" OR ""get access to Flankspeed""
   - First-time mission owner registration/setup
   - Getting initial subscription and environment provisioned
   
2. Use ""flankspeed_provide_info"" for ALL other messages during ACTIVE onboarding session:
   - Providing additional details during onboarding (""We need AKS"", ""The mission name is..."")
   - Updating information during onboarding (""Change the region to..."")
   - Commands during active session (""review"", ""submit"")
   
3. DO NOT use onboarding tools for:
   - ❌ Deploying infrastructure to existing environment (use infrastructure_provisioning)
   - ❌ Creating individual resources (use infrastructure_provisioning)
   - ❌ Managing existing environments (use environment_management)
   - ❌ Template generation (use infrastructure_provisioning)
   - ❌ Any request that doesn't mention ""onboard"" or ""Flankspeed access""

REQUIRED PARAMETER RULES:
- If a tool requires a parameter (like subscriptionId or filePath) and the user didn't provide it, set requiresFollowUp: true and ask for it in followUpPrompt
- Common required parameters: subscriptionId (for ATO tools), filePath (for document tools)
- DO NOT execute tools with missing required parameters - ALWAYS ask first

ATO COMPLIANCE TOOLS:
1. For ALL ATO/NIST compliance queries, use ""ato_compliance"" with natural language query parameter:
   - ""Run comprehensive ATO assessment"" → ato_compliance with query
   - ""Show continuous monitoring status"" → ato_compliance with query
   - ""Collect evidence for AC controls"" → ato_compliance with query  
   - ""Generate remediation plan for critical findings"" → ato_compliance with query
   - ""Assess compliance risk"" → ato_compliance with query
   - ""Show ATO timeline"" → ato_compliance with query
   - ""Generate compliance certificate"" → ato_compliance with query
   
   The ato_compliance tool uses AI to understand the specific compliance operation requested.

INFRASTRUCTURE PROVISIONING TOOLS:
2. For ALL infrastructure operations, use ""infrastructure_provisioning"" with natural language query parameter:
   - KEYWORDS THAT TRIGGER THIS TOOL: ""deploy"", ""create"", ""provision"", ""set up"", ""build"", ""configure"", ""generate template"", ""Bicep"", ""Terraform"", ""infrastructure"", ""resource"", ""web app"", ""application"", ""database"", ""storage"", ""AKS"", ""Kubernetes"", ""VM"", ""virtual machine"", ""Key Vault"", ""App Service"", ""SQL"", ""Cosmos"", ""Redis"", ""Container"", ""Function App""
   - CLOUD KEYWORDS: ""Azure"", ""Azure Government"", ""AWS"", ""AzureUSGovernment"", ""usgovvirginia"", ""usgovtexas""
   - COMPLIANCE KEYWORDS: ""FedRAMP"", ""NIST"", ""compliant"", ""compliance requirements"", ""meet compliance"", ""certified""
   
   Examples:
   - ""Create a storage account named mydata in eastus"" → infrastructure_provisioning with query
   - ""Generate Terraform for Kubernetes cluster"" → infrastructure_provisioning with query
   - ""Deploy AKS with 5 nodes"" → infrastructure_provisioning with query
   - ""Validate my Bicep template"" → infrastructure_provisioning with query
   - ""Generate Bicep for key vault"" → infrastructure_provisioning with query
   - ""Deploy web application to Azure Government with SQL database"" → infrastructure_provisioning with query (CRITICAL: This is infrastructure provisioning, NOT conversational!)
   - ""I need to deploy infrastructure for my mission"" → infrastructure_provisioning with query
   - ""Set up a web app with database and storage in Azure Government"" → infrastructure_provisioning with query
   
   The infrastructure_provisioning tool uses AI to understand whether to create, generate templates, validate, or query resources.
   
   IMPORTANT: If a query mentions deploying/creating Azure resources (web apps, databases, storage, VMs, containers, etc.), it is ALWAYS infrastructure_provisioning, NEVER conversational!

AZURE RESOURCE DISCOVERY TOOLS:
3. For ALL Azure resource discovery queries, use ""azure_resource_discovery"" with natural language query parameter:
   - ""List all my Azure subscriptions"" → azure_resource_discovery with query
   - ""Find all storage accounts in prod resource group"" → azure_resource_discovery with query
   - ""Show all resource groups in subscription 12345"" → azure_resource_discovery with query
   - ""Discover resources in eastus with tag environment=production"" → azure_resource_discovery with query
   - ""List subscriptions with quota and spending information"" → azure_resource_discovery with query
   
   The azure_resource_discovery tool uses AI to understand whether to list subscriptions, discover resources, or list resource groups.

SECURITY SCANNING TOOLS:
4. For ALL security scanning queries, use ""security_scanning"" with natural language query parameter:
   - ""Scan container image myapp:latest for vulnerabilities"" → security_scanning with query
   - ""Run SAST on Python code in /src/api"" → security_scanning with query
   - ""Check Terraform templates for security misconfigurations"" → security_scanning with query
   - ""Scan GitHub repo owner/repo for vulnerabilities"" → security_scanning with query
   - ""Run Trivy scan on docker image with high severity threshold"" → security_scanning with query
   
   The security_scanning tool uses AI to understand what type of scan to perform (container, code, IaC, GitHub repository).

COMPLIANCE SCANNING TOOLS:
13. If query mentions (""compliance scan"" OR ""security scan"" OR ""audit"" OR ""run scan"" OR ""check compliance"" OR ""validate compliance"") AND framework (""FedRAMP"" OR ""NIST 800-53"" OR ""NIST"" OR ""ISO 27001"" OR ""SOC 2"" OR ""SOC2"") → toolName MUST be ""run_compliance_scan""
   - Extract: framework OR frameworks (array for multiple), resource_group_name OR subscription_id, severity_levels, auto_remediate, schedule
   - MULTI-FRAMEWORK: If query lists multiple frameworks (e.g., ""FedRAMP, NIST, ISO 27001""), extract as array and set cross_framework_mapping=true
   - ATO-FOCUSED: If query mentions ""ATO"" OR ""authorization"" OR ""blocking gaps"", set priority=""ato_blocking_gaps"" and use requiresToolChain=true
   - Example: ""Run FedRAMP High scan on mission-prod-rg"" → run_compliance_scan with framework=""FedRAMP High"", resource_group=""mission-prod-rg""
   - Example: ""Check NIST 800-53 compliance for subscription"" → run_compliance_scan with framework=""NIST 800-53""
   - Example: ""Validate compliance against FedRAMP, NIST, and ISO 27001"" → run_compliance_scan with frameworks=[""FedRAMP High"",""NIST 800-53"",""ISO 27001""], cross_framework_mapping=true

14. If query mentions (""compliance report"" OR ""scan results"" OR ""compliance status"" OR ""show compliance"") → toolName MUST be ""get_compliance_report""
   - Extract: scan_id OR resource_group_name, framework (optional)
   - Example: ""Show me the FedRAMP compliance report"" → get_compliance_report

15. If query mentions (""fix compliance"" OR ""remediate"" OR ""auto-fix"" OR ""auto-remediate"") AND (""compliance"" OR ""violations"" OR ""issues"" OR ""gaps"") → toolName MUST be ""remediate_compliance_issues""
   - Extract: resource_group_name OR subscription_id, severity_filter, dry_run
   - Example: ""Auto-fix critical compliance issues in prod-rg"" → remediate_compliance_issues with severity=""critical"", dry_run=false
   - Always default dry_run=true unless user explicitly says ""execute"" or ""apply""

16. If query mentions (""schedule scan"" OR ""recurring scan"" OR ""weekly scan"" OR ""automate compliance"") → toolName MUST be ""schedule_compliance_scan""
   - Extract: framework, resource_group_name OR subscription_id, frequency, alert_on
   - Example: ""Run FedRAMP scan weekly on all resources"" → schedule_compliance_scan with frequency=""weekly""

SECURITY HARDENING TOOLS:
17. If query mentions (""security hardening"" OR ""harden"" OR ""apply security"" OR ""security baseline"") AND mentions security controls (""encryption"" OR ""networking"" OR ""authentication"" OR ""RBAC"" OR ""MFA"" OR ""logging"" OR ""monitoring"" OR ""defender"") → toolName MUST be ""apply_security_hardening""
   - Extract: scope (resource_group OR subscription), encryption_config, networking_config, authentication_config, rbac_config, logging_config, monitoring_config
   - COMPREHENSIVE: If query lists 5+ security controls, extract all configurations into structured parameters
   - Example: ""Apply security hardening to prod-rg"" → apply_security_hardening with basic security controls
   - Example: ""Harden all resources: encryption, private endpoints, Azure AD, MFA, logging"" → apply_security_hardening with all specified controls

18. If query mentions ""security posture"" OR ""security assessment"" OR ""security baseline assessment"" (without ""hardening"" or ""apply"") → toolName MUST be ""security_baseline_assessment""
   - Extract: scope, frameworks (CIS, NIST, etc.)
   - Example: ""Assess security posture for subscription"" → security_baseline_assessment

INCIDENT RESPONSE TOOLS:
19. If query mentions (""incident response"" OR ""security incident"" OR ""SIEM"" OR ""Sentinel"" OR ""security operations"" OR ""SOC"") AND mentions setup/configuration (""set up"" OR ""setup"" OR ""configure"" OR ""implement"") → toolName MUST be ""setup_incident_response""
   - Extract: siem_config, data_sources, analytics_rules, automation_config, notification_config, playbooks, retention_days
   - COMPREHENSIVE: If query lists multiple incident response components (SIEM, analytics, automation, playbooks), extract all configurations
   - Example: ""Set up incident response with Sentinel"" → setup_incident_response with basic SIEM config
   - Example: ""Configure security incident response: SIEM, analytics rules, automation, playbooks, 2 year retention"" → setup_incident_response with all components

20. If query mentions ""Sentinel"" OR ""Microsoft Sentinel"" (without full incident response setup) → toolName MUST be ""configure_sentinel""
   - Extract: workspace_config, data_sources, connectors
   - Example: ""Configure Sentinel workspace"" → configure_sentinel

COST ANALYSIS TOOLS:
21. If query mentions (""cost analysis"" OR ""analyze costs"" OR ""cost breakdown"" OR ""spending analysis"" OR ""cost trends"" OR ""forecast costs"") AND mentions scope (""subscription"" OR ""resource group"" OR ""resources"") → toolName MUST be ""analyze_azure_costs""
   - Extract: subscription_id OR subscription_name, time_period, breakdown_by (resource_group, resource_type, location, tags), include_trends, include_forecasting, budget_threshold
   - COMPREHENSIVE: If query lists multiple analysis dimensions (breakdown, trends, forecasting, recommendations), extract all parameters
   - Example: ""Analyze costs for last 3 months"" → analyze_azure_costs with time_period=""3 months""
   - Example: ""Show cost breakdown by resource group and trends"" → analyze_azure_costs with breakdown_by=[""resource_group""], include_trends=true

22. If query mentions (""cost optimization"" OR ""reduce costs"" OR ""cost savings"" OR ""optimize spending"" OR ""right-sizing"") → toolName MUST be ""optimize_costs""
   - Extract: scope, target_savings_percentage, constraints, optimization_areas (right-sizing, reserved_instances, unused_resources, storage_tiering)
   - Example: ""Optimize costs for production environment"" → optimize_costs with scope=""production""
   - Example: ""Find cost savings opportunities"" → optimize_costs with generate_recommendations=true

23. If query mentions (""budget alert"" OR ""spending alert"" OR ""cost alert"" OR ""budget notification"") → toolName MUST be ""configure_budget_alerts""
   - Extract: budget_amount, alert_threshold_percentage, notification_channels (email, webhook, action_group)
   - Example: ""Alert me if spending exceeds 80% of $50K budget"" → configure_budget_alerts with budget_amount=50000, threshold=80

ENVIRONMENT MANAGEMENT TOOLS:
24. If query mentions managing/operating Azure environments (AKS, Web Apps, Function Apps) AND actions like (""create environment"" OR ""clone environment"" OR ""scale"" OR ""migrate"" OR ""list environments"" OR ""delete environment"" OR ""environment status"" OR ""environment metrics"" OR ""environment health"") → toolName MUST be ""environment_management""
   - Extract: action (create, clone, scale, migrate, list, delete, status, metrics, health), name, environmentType (aks, webapp, function), resourceGroupName, location, nodeCount, scaleSettings, cloneSettings
   - Example: ""Create an AKS environment named api-prod in eastus"" → environment_management with action=""create"", name=""api-prod"", environmentType=""aks"", location=""eastus""
   - Example: ""Scale my production AKS cluster to 5 nodes"" → environment_management with action=""scale"", nodeCount=5
   - Example: ""List all my environments"" → environment_management with action=""list""
   - Example: ""Show health metrics for platform-staging"" → environment_management with action=""health"", name=""platform-staging""
   - Example: ""Clone dev environment to staging and prod"" → environment_management with action=""clone"", cloneSettings with source and targets

OTHER TOOLS:
25. If query mentions ""document"" AND (""upload"" OR ""analyze"" OR ""scan"") → toolName MUST be ""document_upload_analyze""
26. NEVER use document_upload_analyze for ATO or NIST compliance assessments
27. If query mentions ""cancel onboarding"" OR ""start over"" OR ""discard my request"" during active onboarding → toolName MUST be ""flankspeed_cancel_onboarding""

Examples:

Example 1 - Comprehensive Assessment:
User: ""Run a comprehensive ATO compliance assessment on subscription 12345""
Response: {{""intentType"":""tool_execution"",""confidence"":0.95,""toolName"":""ato_compliance"",""parameters"":{{""subscriptionId"":""12345"",""query"":""Run a comprehensive ATO compliance assessment""}},""requiresToolChain"":false,""reasoning"":""User explicitly requested comprehensive ATO assessment with subscription ID""}}

Example 2 - Azure Resource Discovery:
User: ""List all my subscriptions with quota information""
Response: {{""intentType"":""tool_execution"",""confidence"":0.95,""toolName"":""azure_resource_discovery"",""parameters"":{{""query"":""List all my subscriptions with quota information""}},""requiresToolChain"":false,""reasoning"":""User wants to discover Azure subscriptions with quota details""}}

Example 3 - Security Scanning:
User: ""Scan my Python code in /src/api for security vulnerabilities""
Response: {{""intentType"":""tool_execution"",""confidence"":0.95,""toolName"":""security_scanning"",""parameters"":{{""query"":""Scan my Python code in /src/api for security vulnerabilities""}},""requiresToolChain"":false,""reasoning"":""User wants SAST scan on Python codebase""}}

Example 2 - Continuous Monitoring:
User: ""What's the continuous monitoring status?""
Response: {{""intentType"":""tool_execution"",""confidence"":0.88,""toolName"":""ato_compliance"",""parameters"":{{""query"":""What's the continuous monitoring status?""}},""requiresFollowUp"":true,""followUpPrompt"":""Which subscription would you like to monitor?"",""reasoning"":""User asked for continuous monitoring status but didn't specify subscription""}}

Example 3 - Evidence Collection:
User: ""Collect evidence for access control (AC) controls in subscription 67890""
Response: {{""intentType"":""tool_execution"",""confidence"":0.92,""toolName"":""ato_compliance"",""parameters"":{{""subscriptionId"":""67890"",""query"":""Collect evidence for access control (AC) controls""}},""requiresToolChain"":false,""reasoning"":""User requested evidence collection for Access Control family with subscription ID""}}

Example 4 - Document Analysis (Missing Required Parameter):
User: ""Analyze this architecture document""
Response: {{""intentType"":""tool_execution"",""confidence"":0.90,""toolName"":""document_upload_analyze"",""parameters"":{{""analysisType"":""ArchitectureDiagram""}},""requiresFollowUp"":true,""followUpPrompt"":""Please provide the file path to the document you'd like me to analyze."",""reasoning"":""User requested document analysis but didn't provide the required filePath parameter""}}

Example 4 - Remediation Plan:
User: ""Generate a remediation plan for compliance findings""
Response: {{""intentType"":""tool_execution"",""confidence"":0.90,""toolName"":""ato_generate_remediation_plan"",""parameters"":{{""prioritySeverity"":""High""}},""requiresFollowUp"":true,""followUpPrompt"":""Which subscription's findings would you like to remediate?"",""reasoning"":""User wants remediation plan but needs subscription context""}}

Example 5 - Risk Assessment:
User: ""What are the high-risk compliance issues?""
Response: {{""intentType"":""tool_execution"",""confidence"":0.89,""toolName"":""ato_risk_assessment"",""parameters"":{{""includeMitigations"":true}},""requiresFollowUp"":true,""followUpPrompt"":""Which subscription should I assess for risks?"",""reasoning"":""User asking about high-risk issues, needs subscription context""}}

Example 6 - Compliance Drift:
User: ""Check compliance drift in my Azure environment""
Response: {{""intentType"":""tool_execution"",""confidence"":0.87,""toolName"":""ato_continuous_monitoring"",""parameters"":{{""enableAutoRemediation"":false}},""requiresFollowUp"":true,""followUpPrompt"":""Which subscription should I check for compliance drift?"",""reasoning"":""User wants drift detection via continuous monitoring""}}

Example 7 - Navy Flankspeed Onboarding Start (Various Phrasings):
User: ""I need to onboard to Flankspeed""
Response: {{""intentType"":""tool_execution"",""confidence"":0.95,""toolName"":""flankspeed_start_onboarding"",""parameters"":{{}},""requiresFollowUp"":false,""reasoning"":""User wants to start Navy Flankspeed mission owner onboarding process""}}

User: ""I want to onboard a new mission""
Response: {{""intentType"":""tool_execution"",""confidence"":0.95,""toolName"":""flankspeed_start_onboarding"",""parameters"":{{}},""requiresFollowUp"":false,""reasoning"":""User wants to start new mission onboarding to Flankspeed""}}

User: ""Request Flankspeed access for my mission""
Response: {{""intentType"":""tool_execution"",""confidence"":0.95,""toolName"":""flankspeed_start_onboarding"",""parameters"":{{}},""requiresFollowUp"":false,""reasoning"":""User requesting Flankspeed access, triggering onboarding workflow""}}

User: ""Request Flankspeed access for my mission called AEGIS Integration""
Response: {{""intentType"":""tool_execution"",""confidence"":0.95,""toolName"":""flankspeed_start_onboarding"",""parameters"":{{}},""requiresFollowUp"":false,""reasoning"":""User wants to start onboarding and provided mission""}}

User: ""Onboard to Flankspeed with email jsmith@navy.mil""
Response: {{""intentType"":""tool_execution"",""confidence"":0.95,""toolName"":""flankspeed_start_onboarding"",""parameters"":{{""userEmail"":""jsmith@navy.mil""}},""requiresFollowUp"":false,""reasoning"":""User wants to start onboarding and provided email address""}}

User: ""I need to onboard a new mission for NAVWAR. This is for Commander Sarah Chen at SPAWAR. The mission is called AEGIS Integration. We need Azure Kubernetes Service with SQL Server and blob storage. Estimated 200 users, about 1TB of data. Network should be 10.150.0.0/16. Classification is UNCLASS. My email is sarah.chen@navy.mil. The subscription should be called aegis-prod and we want region usgovvirginia.""
Response: {{""intentType"":""tool_execution"",""confidence"":0.98,""toolName"":""flankspeed_start_onboarding"",""parameters"":{{""userEmail"":""sarah.chen@navy.mil""}},""requiresFollowUp"":false,""reasoning"":""User wants to START onboarding with all details provided upfront - use start_onboarding (not provide_info) because there's no active session yet. The tool will auto-extract all fields from the message via initialInfo parameter.""}}

Example 8 - Navy Flankspeed Onboarding Review:
Example 8 - Navy Flankspeed Review/Submit Commands (conversationId auto-injected):
User: ""review""
Response: {{""intentType"":""tool_execution"",""confidence"":0.95,""toolName"":""flankspeed_provide_info"",""parameters"":{{""userInput"":""review""}},""requiresFollowUp"":false,""reasoning"":""User wants to review their onboarding request - use provide_info with 'review' command""}}

User: ""Review my onboarding request""
Response: {{""intentType"":""tool_execution"",""confidence"":0.92,""toolName"":""flankspeed_provide_info"",""parameters"":{{""userInput"":""review""}},""requiresFollowUp"":false,""reasoning"":""User wants to review their Navy Flankspeed onboarding request before submission""}}

User: ""submit""
Response: {{""intentType"":""tool_execution"",""confidence"":0.95,""toolName"":""flankspeed_provide_info"",""parameters"":{{""userInput"":""submit""}},""requiresFollowUp"":false,""reasoning"":""User wants to submit their onboarding request for approval""}}

Example 9 - Navy Flankspeed Provide Info (bulk input - conversationId auto-injected):
User: ""The mission name is Project Seawolf, I'm Cdr John Smith from NNWC, classification is UNCLASS, and my email is jsmith@us.navy.mil""
Response: {{""intentType"":""tool_execution"",""confidence"":0.95,""toolName"":""flankspeed_provide_info"",""parameters"":{{""userEmail"":""jsmith@us.navy.mil"",""rawInput"":""The mission name is Project Seawolf, I'm Cdr John Smith from NNWC, classification is UNCLASS, and my email is jsmith@us.navy.mil""}},""requiresFollowUp"":false,""reasoning"":""User providing multiple mission details - using rawInput for bulk processing with userEmail for session recovery""}}

Example 10 - Navy Flankspeed Provide Info (single field - conversationId auto-injected):
User: ""The VNet CIDR should be 10.50.0.0/16""
Response: {{""intentType"":""tool_execution"",""confidence"":0.93,""toolName"":""flankspeed_provide_info"",""parameters"":{{""field"":""vnetCidr"",""value"":""10.50.0.0/16""}},""requiresFollowUp"":false,""reasoning"":""User updating single technical field - using field/value parameters""}}

Example 11 - Infrastructure Provisioning: Create Storage Account:
User: ""Create a storage account named mydata in resource group prod-rg in eastus""
Response: {{""intentType"":""tool_execution"",""confidence"":0.95,""toolName"":""create_azure_resource"",""parameters"":{{""resource_type"":""storage_account"",""resource_name"":""mydata"",""resource_group_name"":""prod-rg"",""location"":""eastus""}},""requiresFollowUp"":false,""reasoning"":""User wants to create Azure storage account with all required parameters provided""}}

Example 12 - Infrastructure Provisioning: Missing Parameters:
User: ""Create a storage account""
Response: {{""intentType"":""tool_execution"",""confidence"":0.90,""toolName"":""create_azure_resource"",""parameters"":{{""resource_type"":""storage_account""}},""requiresFollowUp"":true,""followUpPrompt"":""To create a storage account, I need:\n- Storage account name (3-24 lowercase letters and numbers)\n- Resource group name\n- Azure region (e.g., eastus, usgovvirginia)"",""reasoning"":""User wants storage account but missing required parameters""}}

Example 13 - Infrastructure Provisioning: Key Vault in Azure Government:
User: ""Deploy a Key Vault named secrets-vault in Azure Government usgovvirginia in security-rg""
Response: {{""intentType"":""tool_execution"",""confidence"":0.93,""toolName"":""create_azure_resource"",""parameters"":{{""resource_type"":""key_vault"",""resource_name"":""secrets-vault"",""resource_group_name"":""security-rg"",""location"":""usgovvirginia"",""azure_environment"":""AzureUSGovernment""}},""requiresFollowUp"":false,""reasoning"":""User wants Key Vault in Azure Government cloud with all details""}}

Example 14 - Infrastructure Query:
User: ""Show me all storage accounts in my subscription""
Response: {{""intentType"":""tool_execution"",""confidence"":0.88,""toolName"":""azure_get_resource"",""parameters"":{{""resourceType"":""Microsoft.Storage/storageAccounts""}},""requiresFollowUp"":true,""followUpPrompt"":""Which subscription and resource group should I check?"",""reasoning"":""User wants to list storage accounts but needs subscription context""}}

Example 15 - Template Generation:
User: ""Generate a Bicep template for an AKS cluster""
Response: {{""intentType"":""tool_execution"",""confidence"":0.92,""toolName"":""generate_bicep_template"",""parameters"":{{""resourceType"":""kubernetes_cluster""}},""requiresFollowUp"":true,""followUpPrompt"":""What should I name the AKS cluster and which region should it be in?"",""reasoning"":""User wants Bicep template generation for AKS""}}

Example 16 - Cost Estimation:
User: ""How much would a storage account cost in Azure Government?""
Response: {{""intentType"":""information_request"",""confidence"":0.85,""reasoning"":""User asking for cost information - need more details about SKU, redundancy, usage patterns for accurate estimate""}}

Example 17 - Complex AKS Deployment (Day-to-day operations, NOT onboarding):
User: ""Deploy a Kubernetes cluster with: Cloud: Azure Government, Name: mission-app-cluster, Node pool: 5 Standard_D4s_v3 nodes, Auto-scaling: 3-10 nodes based on CPU, Networking: Azure CNI with network policies, Monitoring: Container Insights enabled, Security: Azure AD integration, RBAC enabled, Add-ons: Application Gateway Ingress Controller, Azure Key Vault CSI driver""
Response: {{""intentType"":""tool_execution"",""confidence"":0.92,""toolName"":""create_azure_resource"",""parameters"":{{""resource_type"":""kubernetes_cluster"",""resource_name"":""mission-app-cluster"",""azure_environment"":""AzureUSGovernment"",""node_count"":5,""node_size"":""Standard_D4s_v3"",""enable_autoscaling"":true,""min_nodes"":3,""max_nodes"":10,""network_plugin"":""azure"",""enable_monitoring"":true,""enable_rbac"":true,""enable_azure_ad"":true,""addons"":[""application-gateway-ingress"",""azure-keyvault-secrets-provider""]}},""requiresFollowUp"":true,""followUpPrompt"":""I've captured your AKS configuration. I still need:\n- Resource group name\n- Azure region (e.g., usgovvirginia, usgovtexas)\n\nThis is a production-grade setup. Would you like me to:\n1. Create it directly (will show cost estimate first)\n2. Generate a Bicep/Terraform template for review"",""reasoning"":""Complex AKS deployment for existing Flankspeed user - extracted parameters, asking for missing fields. This is day-to-day infrastructure provisioning, not first-time onboarding""}}

Example 18 - Simple AKS Deployment (Existing user):
User: ""Create an AKS cluster named dev-cluster in eastus""
Response: {{""intentType"":""tool_execution"",""confidence"":0.94,""toolName"":""create_azure_resource"",""parameters"":{{""resource_type"":""kubernetes_cluster"",""resource_name"":""dev-cluster"",""location"":""eastus""}},""requiresFollowUp"":true,""followUpPrompt"":""To create the AKS cluster, I need:\n- Resource group name\n- Node count (default: 3)\n- Node size (default: Standard_D2s_v3)\n\nWould you like to use defaults or customize the configuration?"",""reasoning"":""Simple AKS request for existing environment - will use sensible defaults""}}

Example 19 - First-Time Onboarding (Different from resource deployment):
User: ""I need to onboard my mission to Flankspeed""
Response: {{""intentType"":""tool_execution"",""confidence"":0.98,""toolName"":""flankspeed_start_onboarding"",""parameters"":{{}},""requiresFollowUp"":false,""reasoning"":""User explicitly wants first-time Flankspeed onboarding to get initial access and subscription""}}

User: ""Deploy infrastructure for my new mission"" (without mentioning onboarding/Flankspeed)
Response: {{""intentType"":""tool_execution"",""confidence"":0.75,""toolName"":""create_azure_resource"",""parameters"":{{}},""requiresFollowUp"":true,""followUpPrompt"":""I can help deploy infrastructure. First, are you:\n1. A new Mission Owner needing Flankspeed onboarding (first-time setup)\n2. An existing user who wants to deploy resources to your environment\n\nPlease clarify so I can assist you properly."",""reasoning"":""Ambiguous - could be first-time onboarding or existing user deploying resources""}}

Example 19a - Complex Multi-Resource Deployment with Compliance (Navy Commander):
User: ""I'm a Navy Commander working on a classified mission. I need to deploy a web application to Azure Government with SQL database and blob storage. It must meet FedRAMP High compliance requirements.""
Response: {{""intentType"":""tool_execution"",""confidence"":0.94,""toolName"":""infrastructure_provisioning"",""parameters"":{{""query"":""Deploy web application to Azure Government with SQL database, blob storage, and FedRAMP High compliance""}},""requiresFollowUp"":true,""followUpPrompt"":""I'll help you deploy a FedRAMP High compliant web application to Azure Government with:\n\n**Resources:**\n- App Service (web application)\n- SQL Database  \n- Blob Storage\n\n**Compliance:** FedRAMP High certified\n\nTo proceed, I need:\n- Mission name (for resource naming)\n- Azure Government region (usgovvirginia, usgovtexas, usgovarizona)\n- Resource group name (or I can create one)\n- App tier (Standard/Premium/Isolated)\n\nAre you:\n1. New to Flankspeed? (Start onboarding to get subscription + compliance baseline)\n2. Have existing subscription? (Deploy directly)\n3. Want template first? (Generate Bicep/Terraform for review)"",""reasoning"":""Navy Commander requesting multi-resource Azure Government deployment with FedRAMP High compliance. This is INFRASTRUCTURE PROVISIONING not conversational. Detected: web app + SQL + storage + compliance requirements. Using unified infrastructure_provisioning tool with query-based routing.""}}

Example 20 - Comprehensive Compliance Scan (from PROMPT-GUIDE.md):
User: ""Run comprehensive compliance scan for resource group 'mission-prod-rg' with:\n- Framework: FedRAMP High (325 controls)\n- Include: All resources (VMs, storage, networking, databases)\n- Severity: Report all findings (critical, high, medium, low)\n- Output: Detailed report with remediation steps\n- Auto-fix: Where possible, automatically remediate low-risk issues\n- Schedule: Run weekly, alert on new critical findings\n- Evidence: Collect artifacts for ATO package""
Response: {{""intentType"":""tool_execution"",""confidence"":0.96,""toolName"":""run_compliance_scan"",""parameters"":{{""framework"":""FedRAMP High"",""resource_group_name"":""mission-prod-rg"",""include_resource_types"":[""VMs"",""storage"",""networking"",""databases""],""severity_levels"":[""critical"",""high"",""medium"",""low""],""generate_remediation_steps"":true,""auto_remediate"":true,""auto_remediate_severity"":[""low""],""schedule"":""weekly"",""alert_on"":[""critical""],""collect_ato_evidence"":true}},""requiresFollowUp"":false,""reasoning"":""Detailed compliance scan request with all parameters specified. Auto-remediation enabled only for low-risk issues as requested.""}}

Example 21 - Simple Compliance Check:
User: ""Check FedRAMP compliance for my production environment""
Response: {{""intentType"":""tool_execution"",""confidence"":0.89,""toolName"":""run_compliance_scan"",""parameters"":{{""framework"":""FedRAMP High""}},""requiresFollowUp"":true,""followUpPrompt"":""I'll run a FedRAMP High compliance scan. I need:\n- Resource group name or subscription ID\n- Should I include all severity levels (critical, high, medium, low) or just critical/high?\n- Do you want auto-remediation for low-risk issues?"",""reasoning"":""User wants FedRAMP scan but didn't specify scope or options""}}

Example 22 - Get Compliance Report:
User: ""Show me the compliance report for mission-prod-rg""
Response: {{""intentType"":""tool_execution"",""confidence"":0.92,""toolName"":""get_compliance_report"",""parameters"":{{""resource_group_name"":""mission-prod-rg""}},""requiresFollowUp"":false,""reasoning"":""User wants to view existing compliance report""}}

Example 23 - Auto-Remediate Compliance Issues:
User: ""Fix critical compliance violations in prod-rg, but show me what will change first""
Response: {{""intentType"":""tool_execution"",""confidence"":0.95,""toolName"":""remediate_compliance_issues"",""parameters"":{{""resource_group_name"":""prod-rg"",""severity_filter"":[""critical""],""dry_run"":true}},""requiresFollowUp"":true,""followUpPrompt"":""I'll run a dry-run to show what changes would be made to fix critical compliance violations. After you review, you can approve the remediation."",""reasoning"":""User wants to fix critical issues but requested to see changes first - dry_run=true""}}

Example 24 - Schedule Recurring Compliance Scan:
User: ""Set up weekly NIST 800-53 scans for all production resources, alert me on new critical findings""
Response: {{""intentType"":""tool_execution"",""confidence"":0.94,""toolName"":""schedule_compliance_scan"",""parameters"":{{""framework"":""NIST 800-53"",""frequency"":""weekly"",""alert_on"":[""critical""],""alert_on_change_only"":true}},""requiresFollowUp"":true,""followUpPrompt"":""I'll schedule weekly NIST 800-53 scans with alerts for new critical findings. Which resource group or subscription should I scan?"",""reasoning"":""User wants recurring scans with alerting but didn't specify scope""}}

Example 25 - Multi-Framework Compliance Validation (from PROMPT-GUIDE.md):
User: ""Validate compliance against multiple frameworks:\n- FedRAMP High (for government authorization)\n- NIST 800-53 Rev 5 (security controls)\n- ISO 27001 (information security management)\n- SOC 2 Type II (trust service criteria)\n- Scope: Entire Azure Government subscription\n- Priority: Identify gaps blocking ATO approval\n- Timeline: Need ATO within 60 days, prioritize critical gaps""
Response: {{""intentType"":""tool_execution"",""confidence"":0.97,""toolName"":""run_compliance_scan"",""parameters"":{{""frameworks"":[""FedRAMP High"",""NIST 800-53 Rev 5"",""ISO 27001"",""SOC 2 Type II""],""scope"":""subscription"",""cloud_environment"":""AzureGovernment"",""priority"":""ato_blocking_gaps"",""severity_levels"":[""critical"",""high""],""timeline_days"":60,""generate_gap_analysis"":true,""generate_remediation_plan"":true,""cross_framework_mapping"":true}},""requiresToolChain"":true,""toolChain"":[{{""stepNumber"":1,""toolName"":""run_compliance_scan"",""action"":""scan_multi_framework"",""parameters"":{{""frameworks"":[""FedRAMP High"",""NIST 800-53 Rev 5"",""ISO 27001"",""SOC 2 Type II""],""scope"":""subscription""}},""description"":""Run parallel compliance scans across all 4 frameworks""}},{{""stepNumber"":2,""toolName"":""run_compliance_scan"",""action"":""identify_ato_blockers"",""parameters"":{{""priority"":""ato_blocking_gaps"",""timeline_days"":60}},""description"":""Identify critical gaps blocking ATO approval within 60-day timeline""}},{{""stepNumber"":3,""toolName"":""run_compliance_scan"",""action"":""generate_remediation_roadmap"",""parameters"":{{""prioritize_by"":""ato_impact""}},""description"":""Generate prioritized remediation roadmap focusing on ATO-blocking issues first""}}],""reasoning"":""Complex multi-framework compliance validation with ATO timeline. Requires tool chain: 1) Scan all frameworks, 2) Identify ATO blockers, 3) Generate prioritized remediation plan. Cross-framework mapping will show overlapping controls to optimize remediation effort.""}}

Example 26 - Comprehensive Security Hardening (from PROMPT-GUIDE.md):
User: ""Apply security hardening to all resources in subscription:\n- Encryption: Encrypt all data at rest with customer-managed keys\n- Networking: Disable public access, enable private endpoints\n- Authentication: Enforce Azure AD, disable local auth\n- MFA: Require MFA for all admin access\n- RBAC: Least privilege, no Owner role assignments to users\n- Logging: Enable diagnostic settings on all resources\n- Monitoring: Azure Defender for Cloud (all plans)\n- Secrets: No connection strings in app config, use Key Vault references\n- Certificates: Managed certificates with auto-renewal\n- Vulnerabilities: Enable Defender for containers, SQL, storage""
Response: {{""intentType"":""tool_execution"",""confidence"":0.96,""toolName"":""apply_security_hardening"",""parameters"":{{""scope"":""subscription"",""encryption_config"":{{""enable_encryption_at_rest"":true,""use_customer_managed_keys"":true,""key_vault_integration"":true}},""networking_config"":{{""disable_public_access"":true,""enable_private_endpoints"":true,""network_isolation"":true}},""authentication_config"":{{""enforce_azure_ad"":true,""disable_local_auth"":true,""require_mfa"":true}},""rbac_config"":{{""least_privilege"":true,""no_owner_assignments_to_users"":true,""review_existing_assignments"":true}},""logging_config"":{{""enable_diagnostic_settings"":true,""log_to_log_analytics"":true,""retention_days"":365}},""monitoring_config"":{{""enable_defender_for_cloud"":true,""defender_plans"":[""containers"",""sql"",""storage"",""app_service"",""key_vault""],""enable_security_center_recommendations"":true}},""secrets_config"":{{""use_key_vault_references"":true,""remove_connection_strings_from_config"":true}},""certificates_config"":{{""use_managed_certificates"":true,""enable_auto_renewal"":true}},""vulnerability_config"":{{""enable_defender_for_containers"":true,""enable_defender_for_sql"":true,""enable_defender_for_storage"":true}}}},""requiresToolChain"":true,""toolChain"":[{{""stepNumber"":1,""toolName"":""security_baseline_assessment"",""action"":""assess_current_state"",""parameters"":{{""scope"":""subscription""}},""description"":""Assess current security posture before applying changes""}},{{""stepNumber"":2,""toolName"":""apply_security_hardening"",""action"":""apply_encryption"",""parameters"":{{""encryption_config"":""...(see above)""}},""description"":""Apply encryption hardening (customer-managed keys, Key Vault integration)""}},{{""stepNumber"":3,""toolName"":""apply_security_hardening"",""action"":""apply_networking"",""parameters"":{{""networking_config"":""...(see above)""}},""description"":""Apply networking hardening (disable public access, enable private endpoints)""}},{{""stepNumber"":4,""toolName"":""apply_security_hardening"",""action"":""apply_authentication_rbac"",""parameters"":{{""authentication_config"":""..."",""rbac_config"":""...""}},""description"":""Apply authentication and RBAC hardening (Azure AD, MFA, least privilege)""}},{{""stepNumber"":5,""toolName"":""apply_security_hardening"",""action"":""apply_monitoring_logging"",""parameters"":{{""logging_config"":""..."",""monitoring_config"":""...""}},""description"":""Enable logging and monitoring (diagnostic settings, Defender for Cloud)""}},{{""stepNumber"":6,""toolName"":""apply_security_hardening"",""action"":""apply_secrets_certificates"",""parameters"":{{""secrets_config"":""..."",""certificates_config"":""...""}},""description"":""Secure secrets and certificates (Key Vault references, managed certificates)""}},{{""stepNumber"":7,""toolName"":""security_baseline_assessment"",""action"":""verify_hardening"",""parameters"":{{""scope"":""subscription""}},""description"":""Verify all hardening applied successfully and generate compliance report""}}],""reasoning"":""Comprehensive security hardening with 10 security controls. Requires tool chain: 1) Assess baseline, 2-6) Apply each hardening category, 7) Verify. This is a major security transformation.""}}

Example 27 - Simple Security Hardening:
User: ""Harden security for resource group prod-rg""
Response: {{""intentType"":""tool_execution"",""confidence"":0.88,""toolName"":""apply_security_hardening"",""parameters"":{{""scope"":""resource_group"",""resource_group_name"":""prod-rg""}},""requiresFollowUp"":true,""followUpPrompt"":""I'll apply security hardening to resource group 'prod-rg'. Which security controls would you like to apply?\n\n1. Encryption (customer-managed keys, encryption at rest)\n2. Networking (private endpoints, disable public access)\n3. Authentication & RBAC (Azure AD, MFA, least privilege)\n4. Logging & Monitoring (diagnostic settings, Defender)\n5. All of the above (comprehensive hardening)\n\nPlease specify, or I can recommend based on your resource types."",""reasoning"":""User wants security hardening but didn't specify which controls""}}

Example 28 - Security Baseline Assessment:
User: ""Assess the security posture of my Azure Government subscription""
Response: {{""intentType"":""tool_execution"",""confidence"":0.93,""toolName"":""security_baseline_assessment"",""parameters"":{{""scope"":""subscription"",""cloud_environment"":""AzureGovernment"",""frameworks"":[""CIS Azure Foundations"",""NIST 800-53"",""Azure Security Benchmark""],""generate_recommendations"":true}},""requiresFollowUp"":false,""reasoning"":""User wants to assess current security posture without applying changes yet. Will compare against CIS, NIST, and Azure Security Benchmark.""}}

Example 29 - Comprehensive Incident Response Setup (from PROMPT-GUIDE.md):
User: ""Set up security incident response:\n- SIEM: Microsoft Sentinel workspace\n- Data sources: Azure AD logs, activity logs, NSG flow logs, WAF logs\n- Analytics rules: Detect suspicious logins, privilege escalation, data exfiltration, crypto-mining, lateral movement\n- Automation: Auto-block suspicious IPs, disable compromised accounts, isolate infected VMs\n- Notifications: Email security team, create PagerDuty incident\n- Playbooks: Auto-response for common incident types\n- Retention: 2 years (compliance requirement)""
Response: {{""intentType"":""tool_execution"",""confidence"":0.97,""toolName"":""setup_incident_response"",""parameters"":{{""siem_config"":{{""provider"":""Microsoft Sentinel"",""create_workspace"":true,""workspace_sku"":""PerGB2018""}},""data_sources"":[""Azure AD logs"",""Activity logs"",""NSG flow logs"",""WAF logs""],""analytics_rules"":{{""suspicious_logins"":true,""privilege_escalation"":true,""data_exfiltration"":true,""crypto_mining"":true,""lateral_movement"":true}},""automation_config"":{{""auto_block_suspicious_ips"":true,""disable_compromised_accounts"":true,""isolate_infected_vms"":true}},""notification_config"":{{""email"":{{""enabled"":true,""recipients"":[""security-team@organization.mil""]}},""pagerduty"":{{""enabled"":true,""create_incident"":true}}}},""playbooks"":{{""enable_auto_response"":true,""incident_types"":[""malware"",""phishing"",""data_breach"",""unauthorized_access""]}},""retention_days"":730}},""requiresToolChain"":true,""toolChain"":[{{""stepNumber"":1,""toolName"":""configure_sentinel"",""action"":""create_workspace"",""parameters"":{{""workspace_config"":{{""sku"":""PerGB2018""}}}},""description"":""Create Microsoft Sentinel workspace""}},{{""stepNumber"":2,""toolName"":""configure_sentinel"",""action"":""connect_data_sources"",""parameters"":{{""data_sources"":[""Azure AD"",""Activity"",""NSG"",""WAF""]}},""description"":""Connect all specified data sources (Azure AD, Activity, NSG flow logs, WAF)""}},{{""stepNumber"":3,""toolName"":""setup_incident_response"",""action"":""configure_analytics_rules"",""parameters"":{{""rules"":[""suspicious_logins"",""privilege_escalation"",""data_exfiltration"",""crypto_mining"",""lateral_movement""]}},""description"":""Create analytics rules for threat detection""}},{{""stepNumber"":4,""toolName"":""setup_incident_response"",""action"":""configure_automation"",""parameters"":{{""automation_config"":""...(see above)""}},""description"":""Set up automation (auto-block IPs, disable accounts, isolate VMs)""}},{{""stepNumber"":5,""toolName"":""setup_incident_response"",""action"":""configure_notifications"",""parameters"":{{""notification_config"":""...(see above)""}},""description"":""Configure notification channels (email, PagerDuty)""}},{{""stepNumber"":6,""toolName"":""setup_incident_response"",""action"":""deploy_playbooks"",""parameters"":{{""playbooks"":""...(see above)""}},""description"":""Deploy auto-response playbooks for common incident types""}},{{""stepNumber"":7,""toolName"":""configure_sentinel"",""action"":""set_retention"",""parameters"":{{""retention_days"":730}},""description"":""Set 2-year data retention for compliance""}}],""reasoning"":""Comprehensive incident response setup with 7 components. Requires tool chain: 1) Create Sentinel workspace, 2) Connect data sources, 3) Configure analytics rules, 4) Set up automation, 5) Configure notifications, 6) Deploy playbooks, 7) Set retention. This is a complete SOC setup.""}}

Example 30 - Simple Sentinel Configuration:
User: ""Configure Microsoft Sentinel for my subscription""
Response: {{""intentType"":""tool_execution"",""confidence"":0.90,""toolName"":""configure_sentinel"",""parameters"":{{""create_workspace"":true}},""requiresFollowUp"":true,""followUpPrompt"":""I'll set up Microsoft Sentinel. To get started, I need:\n\n1. Which data sources should I connect? (Azure AD logs, Activity logs, NSG flow logs, Security Center alerts, etc.)\n2. Do you want pre-configured analytics rules for common threats?\n3. Should I set up automated responses?\n4. What's your log retention requirement?\n\nI can configure a basic setup or customize based on your security operations needs."",""reasoning"":""User wants Sentinel but didn't specify configuration details""}}

Example 31 - Incident Response with Existing Sentinel:
User: ""Add analytics rules for data exfiltration and privilege escalation to my existing Sentinel workspace""
Response: {{""intentType"":""tool_execution"",""confidence"":0.94,""toolName"":""setup_incident_response"",""parameters"":{{""use_existing_sentinel"":true,""analytics_rules"":{{""data_exfiltration"":true,""privilege_escalation"":true}},""create_alerts"":true}},""requiresFollowUp"":true,""followUpPrompt"":""I'll add analytics rules for data exfiltration and privilege escalation. Should I also:\n\n1. Configure automated responses for these threats?\n2. Set up notification channels (email, Teams, PagerDuty)?\n3. Create playbooks for automatic remediation?\n\nOr just create the detection rules for now?"",""reasoning"":""User has existing Sentinel and wants to add specific analytics rules""}}

Example 32 - Comprehensive Cost Analysis (from PROMPT-GUIDE.md):
User: ""Analyze Azure costs for last 3 months:\n- Subscription: Production subscription\n- Breakdown: By resource group, resource type, location, tags\n- Trends: Show spending trends and anomalies\n- Forecasting: Predict next month's costs\n- Recommendations: Identify cost optimization opportunities\n- Budget alerts: Notify if spending exceeds 80% of monthly budget ($50K)""
Response: {{""intentType"":""tool_execution"",""confidence"":0.97,""toolName"":""analyze_azure_costs"",""parameters"":{{""subscription_name"":""Production subscription"",""time_period"":""3 months"",""breakdown_by"":[""resource_group"",""resource_type"",""location"",""tags""],""include_trends"":true,""include_anomaly_detection"":true,""include_forecasting"":true,""forecast_period"":""1 month"",""include_recommendations"":true,""budget_threshold"":50000,""alert_threshold_percentage"":80}},""requiresToolChain"":true,""toolChain"":[{{""stepNumber"":1,""toolName"":""analyze_azure_costs"",""action"":""get_historical_costs"",""parameters"":{{""subscription_name"":""Production subscription"",""time_period"":""3 months""}},""description"":""Retrieve historical cost data for last 3 months""}},{{""stepNumber"":2,""toolName"":""analyze_azure_costs"",""action"":""breakdown_analysis"",""parameters"":{{""breakdown_by"":[""resource_group"",""resource_type"",""location"",""tags""]}},""description"":""Generate cost breakdowns by resource group, type, location, and tags""}},{{""stepNumber"":3,""toolName"":""analyze_azure_costs"",""action"":""trend_analysis"",""parameters"":{{""include_anomalies"":true}},""description"":""Analyze spending trends and detect cost anomalies""}},{{""stepNumber"":4,""toolName"":""analyze_azure_costs"",""action"":""forecast_costs"",""parameters"":{{""forecast_period"":""1 month""}},""description"":""Predict next month's costs based on historical trends""}},{{""stepNumber"":5,""toolName"":""optimize_costs"",""action"":""generate_recommendations"",""parameters"":{{""scope"":""subscription""}},""description"":""Identify cost optimization opportunities (right-sizing, reserved instances, unused resources)""}},{{""stepNumber"":6,""toolName"":""configure_budget_alerts"",""action"":""create_alert"",""parameters"":{{""budget_amount"":50000,""alert_threshold_percentage"":80}},""description"":""Configure budget alert for 80% of $50K monthly budget""}}],""reasoning"":""Comprehensive cost analysis with 6 components: historical data, multi-dimensional breakdowns, trend/anomaly analysis, forecasting, optimization recommendations, and budget alerts. Requires tool chain for complete analysis.""}}

Example 33 - Cost Optimization Request:
User: ""Optimize costs for production environment - looking for 20% savings""
Response: {{""intentType"":""tool_execution"",""confidence"":0.92,""toolName"":""optimize_costs"",""parameters"":{{""scope"":""production"",""target_savings_percentage"":20,""optimization_areas"":[""right_sizing"",""reserved_instances"",""unused_resources"",""storage_tiering"",""compute_scheduling""],""include_impact_analysis"":true}},""requiresToolChain"":true,""toolChain"":[{{""stepNumber"":1,""toolName"":""analyze_azure_costs"",""action"":""baseline_analysis"",""parameters"":{{""scope"":""production""}},""description"":""Analyze current spending to establish baseline""}},{{""stepNumber"":2,""toolName"":""optimize_costs"",""action"":""identify_opportunities"",""parameters"":{{""optimization_areas"":[""right_sizing"",""reserved_instances"",""unused_resources"",""storage_tiering"",""compute_scheduling""]}},""description"":""Identify optimization opportunities across all areas""}},{{""stepNumber"":3,""toolName"":""optimize_costs"",""action"":""calculate_savings"",""parameters"":{{""target_savings_percentage"":20}},""description"":""Calculate potential savings and prioritize recommendations""}},{{""stepNumber"":4,""toolName"":""optimize_costs"",""action"":""impact_analysis"",""parameters"":{{""include_impact_analysis"":true}},""description"":""Assess impact of each optimization on performance and availability""}}],""reasoning"":""Cost optimization with 20% savings target. Requires analysis of current spending, identification of opportunities across multiple areas (right-sizing, RIs, unused resources, storage, scheduling), calculation of savings, and impact assessment.""}}

Example 34 - Budget Alert Configuration:
User: ""Alert me if spending exceeds 80% of my $100K monthly Azure budget""
Response: {{""intentType"":""tool_execution"",""confidence"":0.95,""toolName"":""configure_budget_alerts"",""parameters"":{{""budget_amount"":100000,""budget_period"":""monthly"",""alert_threshold_percentage"":80,""notification_channels"":[""email""],""include_forecast_alerts"":true}},""requiresFollowUp"":true,""followUpPrompt"":""I'll configure a budget alert for 80% of your $100K monthly budget. A few questions:\n\n1. Which email address(es) should receive alerts?\n2. Should I also alert at other thresholds (e.g., 50%, 90%, 100%)?\n3. Do you want alerts for forecasted spending (if projected to exceed budget)?\n4. Should I create action groups for automated responses?\n\nI can set up basic email alerts now and add more sophisticated monitoring later."",""reasoning"":""User wants budget alert at specific threshold. Need to confirm notification details and whether to add multiple threshold levels or forecast-based alerts.""}}

Respond with JSON format:
{
  ""intentType"": ""tool_execution"" | ""information_request"" | ""conversational"",
  ""confidence"": 0.95,
  ""toolName"": ""tool_name"" (if tool_execution),
  ""parameters"": {key: value} (if tool_execution),
  ""requiresToolChain"": true/false,
  ""toolChain"": [{""stepNumber"": 1, ""toolName"": ""name"", ""action"": ""action"", ""parameters"": {}, ""description"": ""what this does""}],
  ""reasoning"": ""explanation""
}");


            // Add conversation context if available
            if (context != null && context.MessageHistory.Any())
            {
                var recentMessages = context.MessageHistory.TakeLast(5);
                foreach (var msg in recentMessages)
                {
                    if (msg.Role == "user")
                        history.AddUserMessage(msg.Content);
                    else if (msg.Role == "assistant")
                        history.AddAssistantMessage(msg.Content);
                }
            }

            // Add current user message
            history.AddUserMessage(message);

            // Get AI classification with retry logic for rate limits
            ChatMessageContent? result = null;
            int maxRetries = 3;
            int retryCount = 0;
            int baseDelaySeconds = 2;
            int maxDelaySeconds = 10; // Cap retry delay at 10 seconds to avoid timeout
            
            while (retryCount < maxRetries)
            {
                try
                {
                    result = await chatCompletion.GetChatMessageContentAsync(
                        history,
                        cancellationToken: cancellationToken);
                    break; // Success, exit retry loop
                }
                catch (Microsoft.SemanticKernel.HttpOperationException ex) when (ex.Message.Contains("429"))
                {
                    retryCount++;
                    
                    // Extract retry-after delay from error message if present
                    var retryAfterMatch = System.Text.RegularExpressions.Regex.Match(ex.Message, @"retry after (\d+) seconds");
                    int delaySeconds = retryAfterMatch.Success 
                        ? Math.Min(int.Parse(retryAfterMatch.Groups[1].Value), maxDelaySeconds) // Cap at maxDelaySeconds
                        : Math.Min(baseDelaySeconds * (int)Math.Pow(2, retryCount - 1), maxDelaySeconds); // Exponential backoff with cap
                    
                    if (retryCount < maxRetries)
                    {
                        _logger.LogWarning(
                            "Rate limit hit (429), retrying in {DelaySeconds} seconds (attempt {RetryCount}/{MaxRetries}). Consider reducing request frequency.", 
                            delaySeconds, retryCount, maxRetries);
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                    }
                    else
                    {
                        _logger.LogError(ex, "Rate limit exceeded after {MaxRetries} retries, falling back to default classification. You may need to increase Azure OpenAI quota or reduce request frequency.", maxRetries);
                        throw; // Re-throw to be caught by outer catch
                    }
                }
            }
            
            if (result == null)
            {
                _logger.LogWarning("Failed to get AI classification after retries, using default classification");
                return CreateDefaultClassification(message);
            }

            // Parse JSON response
            var jsonResponse = result.Content ?? "{}";

            
            // Extract JSON from markdown code blocks if present
            jsonResponse = ExtractJsonFromMarkdown(jsonResponse);
            
            var classificationData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonResponse);

            if (classificationData == null)
            {
                return CreateDefaultClassification(message);
            }

            // Build classification result
            var classification = new IntentClassificationResult
            {
                IntentType = classificationData.TryGetValue("intentType", out var intentType) 
                    ? intentType.GetString() ?? "conversational" 
                    : "conversational",
                Confidence = classificationData.TryGetValue("confidence", out var confidence) 
                    ? confidence.GetDouble() 
                    : 0.5,
                Reasoning = classificationData.TryGetValue("reasoning", out var reasoning) 
                    ? reasoning.GetString() 
                    : null
            };

            if (classification.IntentType == "tool_execution")
            {
                classification.ToolName = classificationData.TryGetValue("toolName", out var toolName) 
                    ? toolName.GetString() 
                    : null;

                if (classificationData.TryGetValue("parameters", out var parameters))
                {
                    classification.Parameters = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                        parameters.GetRawText()) ?? new();
                }

                classification.RequiresToolChain = classificationData.TryGetValue("requiresToolChain", out var requiresChain) 
                    && requiresChain.GetBoolean();

                if (classification.RequiresToolChain && classificationData.TryGetValue("toolChain", out var toolChain))
                {
                    var chainSteps = JsonSerializer.Deserialize<List<ToolStep>>(toolChain.GetRawText());
                    if (chainSteps != null)
                    {
                        classification.ToolChain = chainSteps;
                    }
                }

                // Parse follow-up requirements
                classification.RequiresFollowUp = classificationData.TryGetValue("requiresFollowUp", out var requiresFollowUp) 
                    && requiresFollowUp.GetBoolean();

                if (classification.RequiresFollowUp && classificationData.TryGetValue("followUpPrompt", out var followUpPrompt))
                {
                    classification.FollowUpPrompt = followUpPrompt.GetString();
                }
            }

            if (classification.IntentType == "conversational")
            {
                _logger.LogWarning(
                    "⚠️ CLASSIFIED AS CONVERSATIONAL - Message: {Message}, Reasoning: {Reasoning}", 
                    message.Length > 100 ? message.Substring(0, 100) + "..." : message,
                    classification.Reasoning ?? "No reasoning provided");
            }
            else
            {
                _logger.LogInformation(
                    "✅ Classified intent: {IntentType} (confidence: {Confidence:P0}), Tool: {ToolName}, RequiresFollowUp: {RequiresFollowUp}", 
                    classification.IntentType, 
                    classification.Confidence,
                    classification.ToolName ?? "none",
                    classification.RequiresFollowUp);
            }

            // Cache the result for future similar queries
            if (_cacheService != null)
            {
                var cacheKey = _cacheService.GenerateCacheKey("intent", message, context?.ConversationId ?? "default");
                await _cacheService.SetAsync(cacheKey, classification, TimeSpan.FromMinutes(30), cancellationToken);
                _logger.LogDebug("Cached intent classification result for message");
            }

            return classification;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying intent, using default classification");
            return CreateDefaultClassification(message);
        }
    }

    /// <summary>
    /// Execute a multi-step tool chain
    /// </summary>
    public async Task<ToolChainResult> ExecuteToolChainAsync(
        List<ToolStep> steps,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        var result = new ToolChainResult
        {
            ChainId = Guid.NewGuid().ToString(),
            Steps = steps,
            Status = "running",
            StartedAt = DateTime.UtcNow
        };

        var context = result.Context;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Executing tool chain {ChainId} with {StepCount} steps", 
                result.ChainId, 
                steps.Count);

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                result.CurrentStep = step.StepNumber;

                try
                {
                    step.Status = "running";
                    var stepStopwatch = Stopwatch.StartNew();

                    _logger.LogInformation(
                        "Executing step {StepNumber}/{TotalSteps}: {ToolName}.{Action}", 
                        step.StepNumber, 
                        steps.Count, 
                        step.ToolName, 
                        step.Action);

                    // Merge context from previous steps if dependent
                    if (step.DependsOnPrevious && i > 0)
                    {
                        var previousStep = steps[i - 1];
                        if (previousStep.Result != null)
                        {
                            step.Parameters["previousResult"] = previousStep.Result;
                        }
                    }

                    // Execute tool
                    var toolCall = new McpToolCall
                    {
                        Name = step.ToolName,
                        Arguments = step.Parameters,
                        RequestId = conversationId // Pass conversation ID for state tracking
                    };

                    // Also add conversation ID to arguments for tools that need it
                    // Replace placeholder values like "<from_context>" with actual conversationId
                    if (toolCall.Arguments.TryGetValue("conversationId", out var existingConvId))
                    {
                        var convIdStr = existingConvId?.ToString();
                        if (string.IsNullOrEmpty(convIdStr) || convIdStr == "<from_context>" || convIdStr.StartsWith("<") || convIdStr.StartsWith("{"))
                        {
                            // Placeholder or template value - replace with actual conversationId
                            toolCall.Arguments["conversationId"] = conversationId;
                        }
                    }
                    else
                    {
                        toolCall.Arguments["conversationId"] = conversationId;
                    }

                    var toolResult = await _toolRouter.RouteToolCallAsync(toolCall, cancellationToken);

                    step.Result = toolResult;
                    step.Status = "completed";
                    step.CompletedAt = DateTime.UtcNow;
                    stepStopwatch.Stop();
                    step.DurationMs = stepStopwatch.ElapsedMilliseconds;

                    // Store result in chain context for next steps
                    context[$"step_{step.StepNumber}_result"] = toolResult;

                    _logger.LogInformation(
                        "Step {StepNumber} completed in {DurationMs}ms", 
                        step.StepNumber, 
                        step.DurationMs);
                }
                catch (Exception ex)
                {
                    step.Status = "failed";
                    step.ErrorMessage = ex.Message;
                    step.CompletedAt = DateTime.UtcNow;
                    result.Errors.Add($"Step {step.StepNumber} failed: {ex.Message}");

                    _logger.LogError(
                        ex, 
                        "Step {StepNumber} failed: {ToolName}.{Action}", 
                        step.StepNumber, 
                        step.ToolName, 
                        step.Action);

                    // Decide whether to continue or abort
                    if (step.DependsOnPrevious)
                    {
                        result.Status = "failed";
                        break;
                    }
                }
            }

            stopwatch.Stop();
            result.TotalDurationMs = stopwatch.ElapsedMilliseconds;
            result.CompletedAt = DateTime.UtcNow;

            // Determine final status
            if (result.Status != "failed")
            {
                result.Status = result.FailedSteps == 0 ? "completed" : "partial_success";
            }

            result.Summary = GenerateChainSummary(result);

            _logger.LogInformation(
                "Tool chain {ChainId} {Status} in {DurationMs}ms. Success rate: {SuccessRate:P0}", 
                result.ChainId, 
                result.Status, 
                result.TotalDurationMs, 
                result.SuccessRate);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool chain {ChainId}", result.ChainId);
            result.Status = "failed";
            result.Errors.Add($"Chain execution failed: {ex.Message}");
            result.CompletedAt = DateTime.UtcNow;
            result.TotalDurationMs = stopwatch.ElapsedMilliseconds;
            return result;
        }
    }

    /// <summary>
    /// Generate proactive suggestions based on conversation context
    /// </summary>
    public async Task<List<ProactiveSuggestion>> GenerateProactiveSuggestionsAsync(
        string conversationId,
        ConversationContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var suggestions = new List<ProactiveSuggestion>();

            // Get chat completion service
            var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();

            // System prompt for suggestion generation
            history.AddSystemMessage(@"You are an AI assistant that generates proactive suggestions for next actions.
Based on the conversation history and context, suggest 2-3 relevant next actions the user might want to take.

Respond with JSON array:
[
  {
    ""title"": ""Check compliance status"",
    ""description"": ""Verify Azure governance compliance for resources"",
    ""priority"": ""high"",
    ""category"": ""compliance"",
    ""icon"": ""🔍"",
    ""confidence"": 0.85,
    ""toolName"": ""ato_comprehensive_assessment"",
    ""action"": ""check"",
    ""suggestedPrompt"": ""Check ATO compliance for my subscription"",
    ""expectedOutcome"": ""Compliance report with any violations""
  }
]

Categories: optimization, security, compliance, workflow, learning
Priority: high, medium, low
Icon: relevant emoji");

            // Add recent conversation history
            var recentMessages = context.MessageHistory.TakeLast(5);
            foreach (var msg in recentMessages)
            {
                if (msg.Role == "user")
                    history.AddUserMessage(msg.Content);
                else if (msg.Role == "assistant")
                    history.AddAssistantMessage(msg.Content);
            }

            history.AddUserMessage("Based on our conversation, what should I do next? Suggest 2-3 actions. Return ONLY the JSON array, no markdown formatting.");

            // Get AI suggestions with retry logic for rate limits
            ChatMessageContent? result = null;
            int maxRetries = 3;
            int retryCount = 0;
            int baseDelaySeconds = 2;
            int maxDelaySeconds = 10; // Cap retry delay at 10 seconds to avoid timeout
            
            while (retryCount < maxRetries)
            {
                try
                {
                    result = await chatCompletion.GetChatMessageContentAsync(
                        history,
                        cancellationToken: cancellationToken);
                    break; // Success
                }
                catch (Microsoft.SemanticKernel.HttpOperationException ex) when (ex.Message.Contains("429"))
                {
                    retryCount++;
                    
                    // Extract retry-after delay from error message if present
                    var retryAfterMatch = System.Text.RegularExpressions.Regex.Match(ex.Message, @"retry after (\d+) seconds");
                    int delaySeconds = retryAfterMatch.Success 
                        ? Math.Min(int.Parse(retryAfterMatch.Groups[1].Value), maxDelaySeconds) // Cap at maxDelaySeconds
                        : Math.Min(baseDelaySeconds * (int)Math.Pow(2, retryCount - 1), maxDelaySeconds); // Exponential backoff with cap
                    
                    if (retryCount < maxRetries)
                    {
                        _logger.LogWarning(
                            "Rate limit hit (429) during suggestions generation, retrying in {DelaySeconds} seconds (attempt {RetryCount}/{MaxRetries})", 
                            delaySeconds, retryCount, maxRetries);
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                    }
                    else
                    {
                        _logger.LogError(ex, "Rate limit exceeded after {MaxRetries} retries for proactive suggestions, returning empty list", maxRetries);
                        return new List<ProactiveSuggestion>(); // Return empty list on final failure
                    }
                }
            }

            var jsonResponse = result?.Content ?? "[]";
            
            // Extract JSON from markdown code blocks if present
            jsonResponse = ExtractJsonFromMarkdown(jsonResponse);
            
            var suggestionsData = JsonSerializer.Deserialize<List<ProactiveSuggestion>>(jsonResponse);

            if (suggestionsData != null)
            {
                suggestions.AddRange(suggestionsData);
            }

            _logger.LogInformation(
                "Generated {Count} proactive suggestions for conversation {ConversationId}", 
                suggestions.Count, 
                conversationId);

            return suggestions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating proactive suggestions");
            return new List<ProactiveSuggestion>();
        }
    }

    /// <summary>
    /// Get or create conversation context
    /// </summary>
    public Task<ConversationContext> GetOrCreateContextAsync(
        string conversationId,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        lock (_conversationLock)
        {
            if (_conversations.TryGetValue(conversationId, out var context))
            {
                context.LastActivityAt = DateTime.UtcNow;
                return Task.FromResult(context);
            }

            var newContext = new ConversationContext
            {
                ConversationId = conversationId,
                UserId = userId,
                StartedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                IsActive = true
            };

            _conversations[conversationId] = newContext;
            return Task.FromResult(newContext);
        }
    }

    /// <summary>
    /// Update conversation context with new message
    /// </summary>
    public Task UpdateContextAsync(
        ConversationContext context,
        MessageSnapshot message,
        CancellationToken cancellationToken = default)
    {
        lock (_conversationLock)
        {
            context.MessageHistory.Add(message);
            context.MessageCount++;
            context.LastActivityAt = DateTime.UtcNow;

            // Keep only last 20 messages to avoid context bloat
            if (context.MessageHistory.Count > 20)
            {
                context.MessageHistory = context.MessageHistory.TakeLast(20).ToList();
            }

            // Track used tools
            if (!string.IsNullOrEmpty(message.ToolExecuted) && !context.UsedTools.Contains(message.ToolExecuted))
            {
                context.UsedTools.Add(message.ToolExecuted);
            }
        }

        return Task.CompletedTask;
    }

    #region Private Helper Methods

    private IntentClassificationResult CreateDefaultClassification(string message)
    {
        // Use keyword-based fallback when AI classification fails (e.g., rate limits)
        var lowerMessage = message.ToLowerInvariant();
        
        // Check for infrastructure provisioning keywords
        var infraKeywords = new[] { "deploy", "create", "provision", "build", "set up", "configure" };
        var resourceKeywords = new[] { "web app", "application", "database", "sql", "storage", "blob", "aks", "kubernetes", "vm", "container", "app service", "key vault", "function" };
        
        bool hasInfraKeyword = infraKeywords.Any(k => lowerMessage.Contains(k));
        bool hasResourceKeyword = resourceKeywords.Any(k => lowerMessage.Contains(k));
        
        if (hasInfraKeyword && hasResourceKeyword)
        {
            _logger.LogWarning("⚠️ AI classification failed, using KEYWORD-BASED fallback: infrastructure_provisioning");
            return new IntentClassificationResult
            {
                IntentType = "tool_execution",
                ToolName = "infrastructure_provisioning",
                Confidence = 0.7,
                Parameters = new Dictionary<string, object?>
                {
                    { "query", message }
                },
                Reasoning = "Keyword-based fallback: detected infrastructure provisioning keywords (deploy/create + resource types). AI classification failed due to rate limits."
            };
        }
        
        // Check for onboarding keywords
        if (lowerMessage.Contains("onboard") || lowerMessage.Contains("flankspeed") || lowerMessage.Contains("new mission"))
        {
            _logger.LogWarning("⚠️ AI classification failed, using KEYWORD-BASED fallback: flankspeed_start_onboarding");
            return new IntentClassificationResult
            {
                IntentType = "tool_execution",
                ToolName = "flankspeed_start_onboarding",
                Confidence = 0.7,
                Reasoning = "Keyword-based fallback: detected onboarding keywords. AI classification failed due to rate limits."
            };
        }
        
        // Check for compliance keywords
        if ((lowerMessage.Contains("compliance") || lowerMessage.Contains("ato") || lowerMessage.Contains("fedramp") || lowerMessage.Contains("nist")) 
            && (lowerMessage.Contains("scan") || lowerMessage.Contains("assess") || lowerMessage.Contains("check")))
        {
            _logger.LogWarning("⚠️ AI classification failed, using KEYWORD-BASED fallback: ato_compliance");
            return new IntentClassificationResult
            {
                IntentType = "tool_execution",
                ToolName = "ato_compliance",
                Confidence = 0.7,
                Parameters = new Dictionary<string, object?>
                {
                    { "query", message }
                },
                Reasoning = "Keyword-based fallback: detected compliance keywords. AI classification failed due to rate limits."
            };
        }
        
        // Default conversational fallback
        _logger.LogWarning("⚠️ AI classification failed, using conversational fallback (no infrastructure/onboarding/compliance keywords found)");
        return new IntentClassificationResult
        {
            IntentType = "conversational",
            Confidence = 0.5,
            Reasoning = "Fallback classification due to error - no infrastructure or onboarding keywords detected"
        };
    }

    /// <summary>
    /// Maps intent types to specific tool names based on user message content.
    /// Provides fallback/override logic for tool selection.
    /// </summary>
    private string? GetToolNameForIntent(string intentType, string userMessage)
    {
        return intentType.ToLowerInvariant() switch
        {
            // Onboarding intents
            "onboarding" when userMessage.Contains("start", StringComparison.OrdinalIgnoreCase) 
                => "flankspeed_start_onboarding",
            "onboarding" => "flankspeed_provide_info",
            
            // Infrastructure provisioning intents - NOW UNIFIED
            "infrastructure_provisioning" => "infrastructure_provisioning",
            
            // Template generation intents - NOW ROUTED TO UNIFIED TOOL
            "template_generation" => "infrastructure_provisioning",
            
            // Validation intents - NOW ROUTED TO UNIFIED TOOL
            "template_validation" => "infrastructure_provisioning",
            
            // Resource query intents - NOW ROUTED TO UNIFIED TOOL
            "resource_query" => "infrastructure_provisioning",
            
            // Environment management intents
            "environment_management" => "environment_management",
            
            // ATO Compliance intents - NOW UNIFIED
            "ato_compliance" => "ato_compliance",
            
            // Azure resource discovery intents - NOW UNIFIED
            "resource_discovery" => "azure_resource_discovery",
            "subscription_query" => "azure_resource_discovery",
            "azure_discovery" => "azure_resource_discovery",
            
            // Security scanning intents - NOW UNIFIED
            "security_scanning" => "security_scanning",
            "vulnerability_assessment" => "security_scanning",
            "container_scanning" => "security_scanning",
            "code_scanning" => "security_scanning",
            
            // Compliance intents - LEGACY (kept for backward compatibility)
            "compliance_scanning" when userMessage.Contains("scan", StringComparison.OrdinalIgnoreCase) 
                || userMessage.Contains("check", StringComparison.OrdinalIgnoreCase)
                => "run_compliance_scan",
            "compliance_scanning" when userMessage.Contains("report", StringComparison.OrdinalIgnoreCase)
                => "get_compliance_report",
            "compliance_scanning" when userMessage.Contains("remediate", StringComparison.OrdinalIgnoreCase) 
                || userMessage.Contains("fix", StringComparison.OrdinalIgnoreCase)
                => "remediate_compliance_issues",
            "compliance_scanning" when userMessage.Contains("schedule", StringComparison.OrdinalIgnoreCase)
                => "schedule_compliance_scan",
            "compliance_scanning" => "run_compliance_scan", // Default to scan
            
            // Security hardening intents
            "security_hardening" when userMessage.Contains("assess", StringComparison.OrdinalIgnoreCase) 
                || userMessage.Contains("baseline", StringComparison.OrdinalIgnoreCase)
                => "security_baseline_assessment",
            "security_hardening" when userMessage.Contains("apply", StringComparison.OrdinalIgnoreCase) 
                || userMessage.Contains("harden", StringComparison.OrdinalIgnoreCase)
                => "apply_security_hardening",
            "security_hardening" => "apply_security_hardening", // Default to apply
            
            // Incident response intents
            "incident_response" when userMessage.Contains("sentinel", StringComparison.OrdinalIgnoreCase)
                => "configure_sentinel",
            "incident_response" when userMessage.Contains("setup", StringComparison.OrdinalIgnoreCase) 
                || userMessage.Contains("configure", StringComparison.OrdinalIgnoreCase)
                => "setup_incident_response",
            "incident_response" => "setup_incident_response", // Default to setup
            
            // Cost analysis intents
            "cost_analysis" when userMessage.Contains("analyze", StringComparison.OrdinalIgnoreCase) 
                || userMessage.Contains("breakdown", StringComparison.OrdinalIgnoreCase) 
                || userMessage.Contains("spending", StringComparison.OrdinalIgnoreCase)
                => "analyze_azure_costs",
            "cost_analysis" when userMessage.Contains("optimize", StringComparison.OrdinalIgnoreCase) 
                || userMessage.Contains("reduce", StringComparison.OrdinalIgnoreCase) 
                || userMessage.Contains("savings", StringComparison.OrdinalIgnoreCase)
                => "optimize_costs",
            "cost_analysis" when userMessage.Contains("budget", StringComparison.OrdinalIgnoreCase) 
                || userMessage.Contains("alert", StringComparison.OrdinalIgnoreCase)
                => "configure_budget_alerts",
            "cost_analysis" => "analyze_azure_costs", // Default to analyze
            
            // Deployment orchestration intents - NOW ROUTED TO UNIFIED TOOL
            "deployment_orchestration" => "infrastructure_provisioning",
            
            // Fallback for unknown intent types
            _ => null
        };
    }

    private string BuildContextString(ConversationContext context)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(context.CurrentTopic))
        {
            parts.Add($"Current topic: {context.CurrentTopic}");
        }

        if (context.UsedTools.Any())
        {
            parts.Add($"Tools used: {string.Join(", ", context.UsedTools)}");
        }

        if (context.MentionedResources.Any())
        {
            parts.Add($"Resources: {string.Join(", ", context.MentionedResources.Values)}");
        }

        return string.Join(". ", parts);
    }

    private async Task<string> GenerateResponseWithContextAsync(
        string message,
        McpToolResult toolResult,
        IntentClassificationResult intent,
        CancellationToken cancellationToken)
    {
        return await _semanticKernel.FormatResponseAsync(
            toolResult,
            message,
            intent.ToolName ?? "unknown",
            cancellationToken);
    }

    private string GenerateToolChainResponse(ToolChainResult chainResult)
    {
        var response = $"I executed a {chainResult.TotalSteps}-step workflow:\n\n";

        foreach (var step in chainResult.Steps)
        {
            var status = step.Status == "completed" ? "✅" : step.Status == "failed" ? "❌" : "⏳";
            response += $"{status} Step {step.StepNumber}: {step.Description}\n";
            
            if (!string.IsNullOrEmpty(step.ErrorMessage))
            {
                response += $"   Error: {step.ErrorMessage}\n";
            }
        }

        response += $"\n{chainResult.Summary}";
        return response;
    }

    private string GenerateChainSummary(ToolChainResult result)
    {
        if (result.Status == "completed")
        {
            return $"All {result.TotalSteps} steps completed successfully in {result.TotalDurationMs}ms.";
        }
        else if (result.Status == "partial_success")
        {
            return $"{result.CompletedSteps} of {result.TotalSteps} steps completed. {result.FailedSteps} failed.";
        }
        else
        {
            return $"Workflow failed. {result.CompletedSteps} of {result.TotalSteps} steps completed before failure.";
        }
    }

    /// <summary>
    /// Extracts JSON content from markdown code blocks if present.
    /// Handles cases where AI returns JSON wrapped in ```json ... ``` or ``` ... ```
    /// </summary>
    private string ExtractJsonFromMarkdown(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "[]";

        var trimmed = content.Trim();

        // Check if wrapped in markdown code blocks
        if (trimmed.StartsWith("```"))
        {
            // Remove opening code fence (```json or ```)
            var lines = trimmed.Split('\n');
            var jsonLines = new List<string>();
            
            bool inCodeBlock = false;
            foreach (var line in lines)
            {
                if (line.TrimStart().StartsWith("```"))
                {
                    inCodeBlock = !inCodeBlock;
                    continue; // Skip the fence line
                }
                
                if (inCodeBlock)
                {
                    jsonLines.Add(line);
                }
            }
            
            return string.Join('\n', jsonLines).Trim();
        }

        return trimmed;
    }

    #endregion
}
