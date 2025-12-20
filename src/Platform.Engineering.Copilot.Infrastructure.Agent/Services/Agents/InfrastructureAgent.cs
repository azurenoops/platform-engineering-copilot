using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Platform.Engineering.Copilot.Core.Interfaces.TokenManagement;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.TokenManagement;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Infrastructure.Core.Services;
using Platform.Engineering.Copilot.Core.Interfaces.Infrastructure;
using Platform.Engineering.Copilot.Core.Interfaces.Deployment;
using Platform.Engineering.Copilot.Infrastructure.Agent.Configuration;

namespace Platform.Engineering.Copilot.Infrastructure.Core;

/// <summary>
/// Specialized agent for Azure infrastructure provisioning and management
/// </summary>
public class InfrastructureAgent : ISpecializedAgent
{
    public AgentType AgentType => AgentType.Infrastructure;

    private readonly Kernel _kernel;
    private readonly IChatCompletionService? _chatCompletion;
    private readonly ILogger<InfrastructureAgent> _logger;
    private readonly InfrastructurePlugin _infrastructurePlugin;
    private readonly string? _defaultSubscriptionId;
    private readonly InfrastructureAgentOptions _options;
    private readonly INetworkTopologyDesignService? _networkDesignService;
    private readonly IPredictiveScalingEngine? _scalingEngine;
    private readonly IComplianceAwareTemplateEnhancer? _complianceEnhancer;
    private readonly ITemplateStorageService _templateStorageService;
    private readonly ITokenCounter _tokenCounter;
    private readonly IPromptOptimizer _promptOptimizer;
    private readonly IRagContextOptimizer _ragContextOptimizer;
    private readonly IConversationHistoryOptimizer _conversationHistoryOptimizer;

    public InfrastructureAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<InfrastructureAgent> logger,
        ILoggerFactory loggerFactory,
        IInfrastructureProvisioningService infrastructureService,
        IDeploymentOrchestrationService deploymentService,
        IDynamicTemplateGenerator templateGenerator,
        IPolicyEnforcementService policyEnforcementService,
        SharedMemory sharedMemory,
        AzureMcpClient azureMcpClient,
        ITemplateStorageService templateStorageService,
        ConfigService configService,
        IAzureResourceService azureResourceService,
        IMemoryCache cache,
        IOptions<AzureGatewayOptions> azureOptions,
        IOptions<InfrastructureAgentOptions> options,
        Platform.Engineering.Copilot.Core.Plugins.ConfigurationPlugin configurationPlugin,
        ITokenCounter tokenCounter,
        IPromptOptimizer promptOptimizer,
        IRagContextOptimizer ragContextOptimizer,
        IConversationHistoryOptimizer conversationHistoryOptimizer,
        INetworkTopologyDesignService? networkDesignService = null,
        IPredictiveScalingEngine? scalingEngine = null,
        IComplianceAwareTemplateEnhancer? complianceEnhancer = null)
    {
        _logger = logger;
        _options = options.Value;
        _defaultSubscriptionId = azureOptions.Value.SubscriptionId;
        _templateStorageService = templateStorageService;
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _promptOptimizer = promptOptimizer ?? throw new ArgumentNullException(nameof(promptOptimizer));
        _ragContextOptimizer = ragContextOptimizer ?? throw new ArgumentNullException(nameof(ragContextOptimizer));
        _conversationHistoryOptimizer = conversationHistoryOptimizer ?? throw new ArgumentNullException(nameof(conversationHistoryOptimizer));
        
        // Store optional services based on configuration
        _networkDesignService = _options.EnableNetworkDesign ? networkDesignService : null;
        _scalingEngine = _options.EnablePredictiveScaling ? scalingEngine : null;
        _complianceEnhancer = _options.EnableComplianceEnhancement ? complianceEnhancer : null;

        // Create specialized kernel for infrastructure work
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.Infrastructure);

        // Create and register infrastructure plugin
        _infrastructurePlugin = new InfrastructurePlugin(
            loggerFactory.CreateLogger<InfrastructurePlugin>(),
            _kernel,
            infrastructureService,
            templateGenerator,
            _networkDesignService,
            _scalingEngine,
            _complianceEnhancer,
            policyEnforcementService,
            sharedMemory,
            azureMcpClient,
            templateStorageService,
            configService,
            azureResourceService,
            cache);

        // Register shared configuration plugin (set_azure_subscription, get_azure_subscription, etc.)
        try
        {
            _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(configurationPlugin, "ConfigurationPlugin"));
            _logger.LogInformation("✅ Registered ConfigurationPlugin for agent");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register ConfigurationPlugin");
        }

        // Register only infrastructure-related plugins
        try
        {
            _kernel.Plugins.AddFromObject(_infrastructurePlugin, "Infrastructure");

            _logger.LogInformation("✅ Registered InfrastructurePlugin for agent");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register InfrastructurePlugin");
        }

        // Try to get chat completion service - make it optional for basic functionality
        try
        {
            _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            _logger.LogInformation("✅ Infrastructure Agent initialized with AI chat completion service");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Infrastructure Agent initialized without AI chat completion service. AI features will be limited.");
            _chatCompletion = null;
        }
    }

    public async Task<AgentResponse> ProcessAsync(AgentTask task, SharedMemory memory)
    {
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("🏗️  Infrastructure Agent processing task: {TaskId}", task.TaskId);

        try
        {
            // Check if AI services are available
            if (_chatCompletion == null)
            {
                _logger.LogWarning("⚠️ AI chat completion service not available. Returning basic response for task: {TaskId}", task.TaskId);
                
                return new AgentResponse
                {
                    TaskId = task.TaskId,
                    AgentType = AgentType.Infrastructure,
                    Success = true,
                    Content = "AI services not configured. Basic infrastructure functionality available through database operations only. " +
                             "Configure Azure OpenAI to enable full AI-powered infrastructure provisioning.",
                    ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds
                };
            }

            var chatHistory = new ChatHistory();

            // Build subscription info if available
            var subscriptionInfo = !string.IsNullOrEmpty(_defaultSubscriptionId)
                ? $@"

**🔧 DEFAULT CONFIGURATION:**
- Default Subscription ID: {_defaultSubscriptionId}
- When users don't specify a subscription, automatically use the default subscription ID above
- ALWAYS use the default subscription when available unless user explicitly specifies a different one
"
                : "";

            // Agent-specific system prompt optimized for infrastructure work
            chatHistory.AddSystemMessage($@"
You are an Azure infrastructure specialist with deep expertise in:
- Azure resource provisioning and lifecycle management
- Infrastructure as Code (Bicep and Terraform)
- Compliance frameworks (FedRAMP High, DoD IL5, NIST 800-53, SOC2, GDPR)
- Network topology design and subnet calculations
- Predictive scaling and auto-scaling optimization
- Networking, security groups, and virtual networks
- Storage accounts, databases, and compute resources
- Azure best practices for security, scalability, and cost optimization
- **Azure context configuration (subscription, tenant, authentication settings)**
{subscriptionInfo}

**CONFIGURATION vs PROVISIONING:**
- If users say ""Use subscription X"", ""Set tenant Y"", ""Set authentication Z"" → **IMMEDIATELY CALL** `set_azure_subscription`, `set_azure_tenant`, or `set_authentication_method` functions (CONFIGURATION)
  - DO NOT just acknowledge - you MUST call the function to actually configure the Azure MCP client
  - Extract the subscription ID/tenant ID from the user's message and pass it to the function
  - Example: ""Use subscription abc-123"" → Call set_azure_subscription(""abc-123"")
  - **CRITICAL**: After calling these functions, return the EXACT function result - DO NOT paraphrase or add commentary
- If users say ""Create resource X"", ""Deploy Y"", ""I need Z"" → Use template generation functions (PROVISIONING)

**ABSOLUTELY CRITICAL: Extract ACTUAL Resource Information!**

When calling functions that require Azure resource IDs or resource details:

1. **ALWAYS extract the ACTUAL resource name, resource group, and subscription ID from the user's message**
2. **NEVER use placeholder or example values** like:
   - ""your-subscription-id"", ""your-resource-group"", ""yourResourceGroup""
   - ""your-app-service-plan"", ""yourAppServicePlan"", ""your-vmss""
   - ""example-resource"", ""my-resource"", etc.

3. **If the user provides resource details, USE THEM EXACTLY:**
   - User says: ""plan-ml-sbx-jrs"" → Use ""plan-ml-sbx-jrs"" (NOT ""yourAppServicePlan"")
   - User says: ""rg-ml-sbx-jrs"" → Use ""rg-ml-sbx-jrs"" (NOT ""your-resource-group"")
   - User says: ""00000000-0000-0000-0000-000000000000"" → Use exact GUID (NOT ""your-subscription-id"")

4. **Build complete resource IDs using ACTUAL values:**
   - Correct: /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-ml-sbx-jrs/providers/Microsoft.Web/serverfarms/plan-ml-sbx-jrs
   - WRONG: /subscriptions/your-subscription-id/resourceGroups/your-resource-group/providers/Microsoft.Web/serverfarms/your-app-service-plan

5. **If the user does NOT provide enough detail, ASK for the specific resource name - do NOT use placeholders**

**CRITICAL: Smart Requirements Gathering!**
When a user asks to deploy, create, or set up infrastructure:

**STEP 1: Analyze what they provided**
Check if the user's request includes:
- ✅ Resource type (AKS, App Service, etc.)
- ✅ Location/region
- ✅ Environment type (dev/staging/production)
- ✅ Basic configuration details (node count, SKU, etc.)

**STEP 2: Decide whether to ask questions**
- **IF user provides ONLY resource type** (e.g., I need an AKS cluster) → **ALWAYS ASK QUESTIONS FIRST**
- **IF user provides MINIMAL details** (e.g., I need an AKS cluster in usgovvirginia) → **ALWAYS ASK QUESTIONS FIRST**
- **IF user provides MOST details but missing 1-2 items** → Ask ONLY for the missing items
- **IF user already answered your questions** → IMMEDIATELY call the function (no confirmation)

**CRITICAL: For Minimal Requests, YOU MUST ASK QUESTIONS!**

Examples of requests that REQUIRE asking questions BEFORE generating:
❌ I need an AKS cluster → TOO VAGUE, ask questions
❌ Deploy an AKS cluster → TOO VAGUE, ask questions
❌ Create a Kubernetes cluster in usgovvirginia → MINIMAL, ask questions
❌ I need Container Apps with DAPR → TOO VAGUE, ask questions
❌ Set up App Service for .NET → TOO VAGUE, ask questions

Examples of requests with ENOUGH detail to generate (but still ask for missing items):
✅ I need an AKS cluster in usgovvirginia with 3 nodes for production with Zero Trust security and monitoring → Has most details, ask about missing ACR/KV
✅ Deploy App Service Plan Premium v3 in eastus with auto-scaling and Application Insights → Has most details, ask about runtime/framework

**IMPORTANT: One Question Cycle Only!**
- First message: User requests infrastructure → **ALWAYS ask for missing critical info**
- Second message: User provides answers → **IMMEDIATELY generate the template**
- **DO NOT** ask a third time or ask for confirmation - just generate!

**CRITICAL: When to ask questions vs when to generate immediately:**

1. **Count the details provided in the user's request**
   - Only resource type (1 detail) → ASK QUESTIONS
   - Resource type + location (2 details) → ASK QUESTIONS
   - Resource type + location + environment (3 details) → ASK QUESTIONS
   - Resource type + location + environment + security/monitoring/scaling (4+ details) → Ask only for 1-2 missing items
   - User is answering your previous questions → GENERATE IMMEDIATELY

2. **Check if this is a follow-up message** (user answering your questions)
   - If you asked questions in the previous turn → User's response is the answer → CALL THE FUNCTION NOW
   - If this is the first message from user → Analyze detail count (see above)

3. **NEVER generate immediately for vague/minimal requests**
   - I need X → ASK QUESTIONS
   - Deploy X → ASK QUESTIONS
   - Create X in Y region → ASK QUESTIONS
   - Only generate immediately if user provided 4+ details or is answering your questions

**For AKS/Kubernetes clusters:**
Required info to generate (ask ONLY if missing):
- Resource type: AKS/Kubernetes (usually obvious from request)
- Location/region (if not provided)
- Node count (default: 3 if not specified)
- Environment type: dev/staging/prod (default: dev if not specified)

Optional info (use smart defaults if not provided):
- Security: Default to Zero Trust (workload identity, Azure Policy, private endpoints) for prod, relaxed for dev
- Monitoring: Default to Container Insights + Prometheus
- Networking: Default to Azure CNI
- Identity: Default to Azure AD RBAC enabled
- ACR: Default to include if production
- Key Vault: Default to include if production
  - Workload Identity for pod authentication?
- **Monitoring & Observability**:
  - Container Insights (Azure Monitor)?
  - Prometheus + Grafana?
  - Application Insights?
- **Scaling**:
  - Node count and VM size?
  - Auto-scaling needed? (min/max nodes)
  - Cluster auto-scaler?
- **Additional Services**:
  - Azure Container Registry (ACR)?
  - Key Vault for secrets?
  - Application Gateway for ingress?
  - Azure Defender for security?

**For Storage/Database resources, ask about:**
- Performance requirements (IOPS, throughput)
- Redundancy needs (LRS, GRS, ZRS, GZRS)
- Encryption requirements
- Access patterns (public, private endpoints)
- Backup and retention policies

**For App Service / Web Apps, ask about:**
- **Runtime & Framework**:
  - Programming language? (.NET, Node.js, Python, Java, PHP)
  - Framework version?
  - Container or code deployment?
- **Performance & Scaling**:
  - App Service Plan SKU? (B1/B2/B3, S1/S2/S3, P1v3/P2v3/P3v3)
  - Auto-scaling needed? (rules based on CPU, memory, requests)
  - Always On required? (production recommendation)
- **Security**:
  - HTTPS only enforcement?
  - Managed Identity for authentication?
  - VNet integration for backend access?
  - Private endpoints for inbound traffic?
- **Monitoring & Diagnostics**:
  - Application Insights?
  - Log Analytics integration?
  - Health check endpoints?
- **Integration**:
  - Key Vault for connection strings/secrets?
  - Azure SQL or other database connections?
  - Storage account for files/blobs?
  - Custom domain and SSL certificates?

**For Container Apps, ask about:**
- **Container Configuration**:
  - Container image source? (ACR, Docker Hub, private registry)
  - Container resource limits? (CPU cores, memory)
  - Environment variables needed?
- **Scaling & Availability**:
  - Min/max replica counts?
  - Scaling rules? (HTTP, CPU, memory, custom metrics)
  - Zero-scale needed? (scale to 0 when idle)
- **DAPR Integration**:
  - Enable DAPR for microservices? (service-to-service, pub/sub, state)
  - DAPR components needed? (state stores, pub/sub, bindings)
- **Networking & Ingress**:
  - External ingress (public internet) or internal only?
  - Custom domains needed?
  - Traffic splitting for blue/green deployments?
  - Session affinity required?
- **Security & Identity**:
  - Managed Identity for Azure service access?
  - Secrets management? (Container Apps secrets or Key Vault)
  - Private Container Apps Environment?
- **Monitoring**:
  - Log Analytics workspace?
  - Application Insights integration?
  - Custom metrics and alerts?

**For Virtual Networks, ask about:**
- Address space and subnet requirements
- Number of tiers (web, app, data)
- Bastion host needed?
- Azure Firewall or Network Virtual Appliance?
- Connectivity to on-premises (VPN, ExpressRoute)

**Example conversation flow (TWO messages maximum):**

User: ""I need to deploy a new Kubernetes cluster in Azure. Can you help me set up an AKS cluster in the US GOV Virginia region with 3 nodes in subscription 00000000-0000-0000-0000-000000000000?""

You: ""Great! I'd be happy to help you set up an AKS cluster in the US GOV Virginia region. I just need to know:

1. What environment is this for? (development, staging, production)
2. Do you need Zero Trust architecture with private endpoints and workload identity?
3. Would you like monitoring (Container Insights + Prometheus)?
4. Should I include Azure Container Registry (ACR) and Key Vault integration?

Based on your answers, I'll generate the template immediately.""

User: ""dev, zero trust, add monitoring, add ACR and KV""

You: **[IMMEDIATELY call generate_infrastructure_template function - DO NOT write out another message asking for confirmation]**

The function will be called with:
- resourceType: ""aks""
- location: ""usgovvirginia""
- nodeCount: 3
- subscriptionId: ""00000000-0000-0000-0000-000000000000""
- description: ""Development AKS cluster with Zero Trust, monitoring, ACR, and Key Vault""
- templateFormat: ""bicep""

**CRITICAL: After user answers your questions, call the function immediately. Do NOT:**
- Ask ""Should I proceed?""
- Ask ""Any adjustments needed?""
- Repeat the same questions
- Summarize and wait for confirmation
- Write another message - JUST CALL THE FUNCTION!
- Ask for ""Type of monitoring"" or ""specific configurations"" - use smart defaults!
- Say ""To set up X, we need to know..."" - NO! Just call the function!

**YOU HAVE PLUGINS/FUNCTIONS - USE THEM! Don't just talk about what you would do!**

**ABSOLUTELY CRITICAL - TEMPLATE GENERATION BEHAVIOR:**
1. When you call generate_infrastructure_template or generate_compliant_infrastructure_template, the function WILL RETURN THE COMPLETE TEMPLATE CODE
2. YOU MUST RETURN THAT COMPLETE RESPONSE TO THE USER - DO NOT SUMMARIZE IT!
3. The function response includes all files in code blocks with details tags
4. NEVER say 'The template has been generated' and list files without code - that is a summary, not the actual output!
5. If the function returns code blocks, you MUST include ALL those code blocks in your response to the user
6. DO NOT truncate, summarize, or paraphrase the template output - return it verbatim!

**When to use each function:**
- **generate_infrastructure_template**: Default choice for AKS, storage, networking, databases, App Service, Container Apps
- **generate_compliant_infrastructure_template**: ONLY when user explicitly mentions compliance frameworks (FedRAMP, DoD IL5, NIST)
- **design_network_topology**: When user asks to design/create VNet or network architecture
- **provision_infrastructure**: ONLY when user says ""deploy NOW"" or ""create it immediately""

**Function calling is MANDATORY after gathering requirements - do not just describe what you would do!**
- delete_resource_group: Delete a resource group

**Decision Matrix - Which function to use:**

USE generate_compliant_infrastructure_template when user mentions:
- ""FedRAMP"" or ""FedRAMP High""
- ""DoD IL5"" or ""DoD Impact Level 5""
- ""NIST 800-53"" or ""NIST controls""
- ""SOC2"" or ""SOC 2""
- ""GDPR"" or ""GDPR compliant""
- ""compliant"" or ""compliance""
- ""secure"" or ""hardened"" or ""production-ready""
- ""government cloud"" or ""federal""
- Any mention of security controls or compliance frameworks

USE generate_infrastructure_template when:
- No compliance framework mentioned
- User wants basic/standard templates
- Development or testing environments (non-production)

USE design_network_topology when user asks:
- ""Design a network topology""
- ""Create a VNet layout""
- ""Design a 3-tier network""
- ""Set up network with bastion and firewall""
- ""Plan subnet architecture""

USE calculate_subnet_cidrs when user asks:
- ""How many subnets fit in 10.0.0.0/16?""
- ""Calculate subnets for this address space""
- ""Subdivide this network into X subnets""

USE predict_scaling_needs when user asks:
- ""Will I need to scale up/down?""
- ""Predict my scaling needs""
- ""Forecast resource usage""
- ""When should I scale?""
- ""Anticipate load for next week/month""

USE optimize_scaling_configuration when user asks:
- ""Optimize my auto-scaling""
- ""Improve scaling efficiency""
- ""Tune my scaling rules""
- ""Better scaling configuration""
- ""How can I scale more efficiently?""

USE analyze_scaling_performance when user asks:
- ""How is my scaling performing?""
- ""Review scaling history""
- ""Scaling effectiveness""
- ""Was scaling efficient?""
- ""Analyze my scaling metrics""

USE generate_infrastructure_template when user wants:
- ""Show me the Bicep/Terraform for...""
- ""Generate code for...""
- ""What would the template look like for...""
- ""I need to deploy..."" (default - show code first)
- ""Create..."" (default - show code first)
- ""Set up..."" (default - show code first)
- ""Help me deploy..."" (default - show code first)

USE provision_infrastructure when user explicitly says:
- ""Deploy NOW""
- ""Provision IMMEDIATELY""
- ""Actually create the resource""
- ""Execute the deployment""
- ""Deploy this right now""

**DEFAULT BEHAVIOR**: Unless user explicitly says ""NOW"" or ""IMMEDIATELY"", always use generate_infrastructure_template 
to show them the IaC code first. This allows them to review, customize, and deploy when ready.

**ACTUAL PROVISIONING WORKFLOW - CRITICAL!**
When your task description includes keywords like ""Provision"", ""Deploy the template"", ""Create the resources"", ""Actually provision"":

1. **YOU MUST DEPLOY, NOT GENERATE!**
   - DO NOT call generate_infrastructure_template again (template was already generated earlier)
   - You are being asked to DEPLOY the previously generated template
   - The template files are stored in SharedMemory from the previous conversation turn

2. **HOW TO DEPLOY FROM SHAREDMEMORY:**
   - Templates from earlier in the conversation are stored in SharedMemory
   - Call deploy_bicep_template with the template information
   - Use the resource group name, location, and subscription ID that were used during generation
   - Deploy immediately - this is the actual provisioning step!

3. **EXAMPLE - WRONG (Don't do this):**
   Task: ""Provision the AKS cluster using the generated Bicep template""
   Response: [Calls generate_infrastructure_template again] ❌ WRONG - Already generated!

4. **EXAMPLE - CORRECT (Do this):**
   Task: ""Provision the AKS cluster using the generated Bicep template in subscription 453c...""
   Response: [Calls deploy_bicep_template with actual template path/content, resource group, subscription] ✅ CORRECT

5. **DEPLOYMENT REQUIRES:**
   - templatePath: **CRITICAL - JUST USE THE FILENAME**: ""main.bicep"" (DeploymentPlugin will retrieve it from SharedMemory automatically)
     - ❌ WRONG: /path/to/main.bicep, /full/path/to/template.bicep  
     - ✅ CORRECT: main.bicep (simple filename only!)
   - resourceGroup: Extract from user's request or use generated name
   - location: **For Azure Government, ONLY use**: usgovvirginia, usgovarizona, usgovtexas, usgoviowa, usdodeast, usdodcentral
     - ❌ WRONG: eastus, westus, centralus (these are commercial Azure regions, NOT Azure Government!)
     - ✅ CORRECT: usgovvirginia (default for dev), usgovarizona (for production)
   - subscriptionId: ALWAYS use the actual subscription ID provided (e.g., 00000000-0000-0000-0000-000000000000)
   - parameters: Optional - use values from conversation if needed

**CRITICAL RULE: ALWAYS CALL FUNCTIONS, NEVER JUST DESCRIBE!**
When you have enough information to fulfill a request:
- DO: Call the appropriate function (generate_infrastructure_template, design_network_topology, etc.)
- DON'T: Write a message explaining what you would do
- DON'T: Ask for more information unless truly critical data is missing
- DON'T: Provide step-by-step guides instead of calling functions
- DON'T: Write manual Bicep/Terraform code - ALWAYS use generate_infrastructure_template

**For MULTIPLE RESOURCES in one request:**
Call generate_infrastructure_template MULTIPLE TIMES - once for each resource type:
- SQL Database → call with resourceType=""sql-database""
- Storage Account → call with resourceType=""storage-account""
- Virtual Network → call with resourceType=""vnet""

**Example of WRONG behavior:**
User: ""I need SQL database, storage account, and VNet in usgovarizona""
You: ""Here's a Bicep template for the Storage Account: [writes manual code]""  ❌ WRONG - Never write manual templates!

**Example of CORRECT behavior:**
User: ""I need SQL database, storage account, and VNet in usgovarizona""
You: [Calls generate_infrastructure_template 3 times:
1. resourceType=""sql-database"", location=""usgovarizona""
2. resourceType=""storage-account"", location=""usgovarizona""  
3. resourceType=""vnet"", location=""usgovarizona""]  ✅ CORRECT

**NEVER EVER:**
- Write manual Bicep/Terraform code in your response
- Say ""Here's the template:"" and write code
- Provide code snippets - ONLY call functions
- Give step-by-step deployment instructions without calling functions first

**ALWAYS:**
- Use generate_infrastructure_template for ANY infrastructure request
- Call it multiple times for multiple resources
- Trust the function to generate proper, complete templates

**Remember: After one round of questions, you MUST call a function on the next turn. No more questions!**

Guidelines:
1. Default to showing code (generate_infrastructure_template) - safer, more transparent
2. Only actually provision (provision_infrastructure) when explicitly requested with urgency keywords
3. Use predictive scaling functions to help users optimize resource utilization
4. Call functions immediately when you have sufficient information
5. Suggest security improvements (encryption, network isolation)
6. Recommend cost-effective SKUs while meeting requirements
7. Ensure compliance with naming conventions

**Response Format:**
- Be precise, technical, and professional
- NEVER include email signatures, sign-offs, or placeholders like ""[Your Name]"", ""Best regards"", ""Sincerely"", etc.
- DO NOT end with ""Feel free to reach out"", ""Let me know if you need help"", or similar closing phrases
- Simply provide the information and end the response - you are an AI agent, not a human writing an email

Be precise, technical, and always CALL FUNCTIONS rather than just describing what you would do.

**CRITICAL FINAL REMINDER:**
You are an AI infrastructure agent. DO NOT sign your responses. DO NOT use closings like ""Best regards"" or ""[Your Name]"". 
End your responses immediately after providing the technical information. No signatures, no closings, no pleasantries at the end.
");

            // Add the user's task
            chatHistory.AddUserMessage(task.Description);

            // Add context from shared memory if available - THIS IS CRITICAL FOR CONVERSATION CONTINUITY
            if (memory.HasContext(task.ConversationId))
            {
                var context = memory.GetContext(task.ConversationId);
                
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
                            "Infrastructure Agent - Conversation optimization needed: {HealthStatus}", 
                            health.GetHealthSummary());

                        // Manage context window by getting focused message range
                        var managedMessages = await ManageContextWindowAsync(
                            conversationMessages, 
                            _conversationHistoryOptimizer, 
                            conversationMessages.Count - 1,
                            4000);
                        
                        conversationMessages = managedMessages;
                    }
                }                
                // Phase 5: Optimize conversation history before using it
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

                    var optimizedHistory = await OptimizeConversationHistoryAsync(conversationMessages, _conversationHistoryOptimizer, 4000);
                    
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
**IMPORTANT: Previous conversation context (optimized for this conversation):**
{historyText}

**The current message is a continuation of this conversation. User has ALREADY provided:**
- Original request details (AKS, location, node count, subscription, etc.)
- Answers to your follow-up questions (environment type, security needs, monitoring preferences, integrations)

**DO NOT ask for information the user already provided above. Instead, USE the information to call the appropriate function NOW!**
");
                    }
                }
                else
                {
                    // Fallback: Add previous USER messages from this conversation (if no optimized history)
                    if (context.MessageHistory != null && context.MessageHistory.Any())
                    {
                        var recentHistory = context.MessageHistory
                            .OrderBy(m => m.Timestamp)
                            .TakeLast(5) // Last 5 messages for context
                            .ToList();

                        if (recentHistory.Any())
                        {
                            var historyText = string.Join("\n", recentHistory.Select(h => 
                                $"{h.Role}: {h.Content}"));
                            
                            chatHistory.AddUserMessage($@"
**IMPORTANT: Previous conversation context (this is the SAME conversation):**
{historyText}

**The current message is a continuation of this conversation. User has ALREADY provided:**
- Original request details (AKS, location, node count, subscription, etc.)
- Answers to your follow-up questions (environment type, security needs, monitoring preferences, integrations)

**DO NOT ask for information the user already provided above. Instead, USE the information to call the appropriate function NOW!**
");
                        }
                    }
                }
                
                // Add previous agent results if any
                if (context.PreviousResults.Any())
                {
                    var contextSummary = string.Join("\n", context.PreviousResults
                        .Select(r => $"[{r.AgentType}]: {r.Content.Substring(0, Math.Min(200, r.Content.Length))}"));

                    chatHistory.AddUserMessage($@"
**Previous agent results:**
{contextSummary}

Consider these results in your response.
");
                }
            }

            // Set conversation ID in plugin for file storage/retrieval
            _infrastructurePlugin.SetConversationId(task.ConversationId);

            // Add CRITICAL final reminder to force function calling
            chatHistory.AddSystemMessage(@"
**FINAL REMINDER BEFORE YOU RESPOND:**
You MUST call one of your available functions. Do NOT write Bicep/Terraform code manually.

Available functions you MUST use:
- generate_infrastructure_template (for SQL, storage, VNet, AKS, etc.)
- design_network_topology (for network design)
- calculate_subnet_cidrs (for subnet calculations)

If the task asks to 'generate template' or 'create infrastructure code', call generate_infrastructure_template NOW.
Do NOT write a text response with manual code - CALL THE FUNCTION!
");

            // Phase 5: Optimize prompt to fit token budget before sending to LLM
            var systemPromptText = chatHistory.FirstOrDefault(m => m.Role == Microsoft.SemanticKernel.ChatCompletion.AuthorRole.System)?.Content ?? "";
            var userMessageText = task.Description;
            if (!string.IsNullOrEmpty(systemPromptText) && !string.IsNullOrEmpty(userMessageText))
            {
                var optimizedPrompt = FitPromptInTokenBudget(systemPromptText, userMessageText);
                if (optimizedPrompt.WasOptimized)
                {
                    _logger.LogInformation(
                        "Infrastructure Agent - Prompt optimized before LLM call: {Strategy}, Tokens saved: {Saved}",
                        optimizedPrompt.OptimizationStrategy, optimizedPrompt.TokensSaved);
                }
            }

            // Invoke chat completion with plugin auto-invocation
            _logger.LogInformation("InfrastructureAgent: Starting OpenAI chat completion for conversation {ConversationId}", task.ConversationId);
            var result = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = _options.Temperature,
                    MaxTokens = _options.MaxTokens
                },
                kernel: _kernel);
            _logger.LogInformation("InfrastructureAgent: Completed OpenAI chat completion for conversation {ConversationId}", task.ConversationId);

            // 🔍 DIAGNOSTIC: Log what the LLM actually did
            _logger.LogInformation("🔍 InfrastructureAgent DIAGNOSTIC:");
            _logger.LogInformation("   - Result Content Length: {Length} characters", result.Content?.Length ?? 0);
            _logger.LogInformation("   - Result Role: {Role}", result.Role);
            _logger.LogInformation("   - Result Metadata Keys: {Keys}", result.Metadata?.Keys != null ? string.Join(", ", result.Metadata.Keys) : "null");
            
            // Check if any functions were called by examining the chat history
            var functionCalls = chatHistory.Where(m => m.Role == Microsoft.SemanticKernel.ChatCompletion.AuthorRole.Tool).ToList();
            _logger.LogInformation("   - Function calls in history: {Count}", functionCalls.Count);
            
            // 🔧 SPECIAL HANDLING: For Azure context configuration functions, return the ACTUAL function result
            // instead of the LLM's paraphrased response
            string? configurationFunctionResult = null;
            var lastToolMessage = chatHistory.LastOrDefault(m => m.Role == Microsoft.SemanticKernel.ChatCompletion.AuthorRole.Tool);
            if (lastToolMessage != null)
            {
                var toolContent = lastToolMessage.Content;
                // Check if this was a configuration function (they all return messages with ✅)
                if (!string.IsNullOrEmpty(toolContent) && 
                    (toolContent.Contains("✅ **Azure Subscription Configured**") ||
                     toolContent.Contains("✅ **Azure Tenant Configured**") ||
                     toolContent.Contains("✅ **Authentication Method Configured**") ||
                     toolContent.Contains("📋 **Current Azure Context**")))
                {
                    _logger.LogInformation("🔧 Configuration function detected - using raw function result instead of LLM paraphrase");
                    configurationFunctionResult = toolContent;
                }
            }
            
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

            var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation("InfrastructureAgent: Total execution time: {ExecutionTime}ms", executionTime);

            // Extract metadata from the result
            var metadata = ExtractMetadata(result);
            metadata["ExecutionTime"] = executionTime;

            // Phase 5: Record agent cost metrics for this operation
            try
            {
                var completionTokens = _tokenCounter.CountTokens(result.Content ?? "");
                // Create a minimal OptimizedPrompt for cost tracking
                var costPrompt = new OptimizedPrompt
                {
                    WasOptimized = false,
                    RagContext = new List<string>(),
                    ConversationHistory = new List<string>()
                };
                await RecordAgentCostAsync(costPrompt, completionTokens, task.TaskId, task.ConversationId ?? "default");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record cost metrics for task {TaskId}", task.TaskId);
            }

            // Use configuration function result if available, otherwise use LLM response
            var responseContent = configurationFunctionResult ?? result.Content ?? "No content generated";
            
            // Clean the response content - remove email signatures (but preserve formatting for config messages)
            var cleanedContent = configurationFunctionResult != null 
                ? responseContent // Don't clean configuration messages - they're already formatted
                : CleanResponseContent(responseContent);

            // Store result in shared memory for other agents
            memory.AddAgentCommunication(
                task.ConversationId ?? "default",
                AgentType.Infrastructure,
                null, // broadcast to all agents
                "Infrastructure provisioning completed",
                new { TaskId = task.TaskId, Success = true, Metadata = metadata });

            return new AgentResponse
            {
                TaskId = task.TaskId,
                AgentType = AgentType,
                Content = cleanedContent,
                Success = true,
                Metadata = metadata,
                ExecutionTimeMs = executionTime,
                ToolsInvoked = metadata.ContainsKey("ToolCalls") 
                    ? metadata["ToolCalls"] as List<string> ?? new List<string>()
                    : new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in Infrastructure Agent processing task: {TaskId}", task.TaskId);

            var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            return new AgentResponse
            {
                TaskId = task.TaskId,
                AgentType = AgentType,
                Content = $"Failed to process infrastructure request: {ex.Message}",
                Success = false,
                Errors = new List<string> { ex.Message },
                ExecutionTimeMs = executionTime
            };
        }
    }

    /// <summary>
    /// Extract metadata from the chat completion result
    /// </summary>
    private Dictionary<string, object> ExtractMetadata(ChatMessageContent result)
    {
        var metadata = new Dictionary<string, object>();

        // Check if tools were called
        if (result.Metadata != null)
        {
            if (result.Metadata.ContainsKey("ToolCalls"))
            {
                metadata["ToolCallsExecuted"] = true;
                // Extract tool names if available
                var toolCalls = new List<string>();
                // Note: Actual tool call extraction depends on SK version
                metadata["ToolCalls"] = toolCalls;
            }
        }

        // Extract resource IDs from content (if mentioned)
        var resourceIds = ExtractResourceIds(result.Content);
        if (resourceIds.Any())
        {
            metadata["ResourceIds"] = resourceIds;
        }

        // Extract resource types
        var resourceTypes = ExtractResourceTypes(result.Content);
        if (resourceTypes.Any())
        {
            metadata["ResourceTypes"] = resourceTypes;
        }

        return metadata;
    }

    /// <summary>
    /// Extract Azure resource IDs from text
    /// </summary>
    private List<string> ExtractResourceIds(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return new List<string>();

        var resourceIds = new List<string>();
        var pattern = @"/subscriptions/[a-f0-9\-]+/resourceGroups/[^/\s]+/providers/[^\s]+";
        var matches = System.Text.RegularExpressions.Regex.Matches(content, pattern);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            resourceIds.Add(match.Value);
        }

        return resourceIds;
    }

    /// <summary>
    /// Extract Azure resource types from text
    /// </summary>
    private List<string> ExtractResourceTypes(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return new List<string>();

        var resourceTypes = new List<string>();
        var knownTypes = new[]
        {
            "Storage Account", "Virtual Network", "Virtual Machine", "Key Vault",
            "SQL Database", "App Service", "AKS", "Container Registry",
            "Function App", "Cosmos DB", "Service Bus", "Event Hub"
        };

        foreach (var type in knownTypes)
        {
            if (content.Contains(type, StringComparison.OrdinalIgnoreCase))
            {
                resourceTypes.Add(type);
            }
        }

        return resourceTypes.Distinct().ToList();
    }

    /// <summary>
    /// Clean response content by removing email signatures and closings
    /// </summary>
    private string CleanResponseContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        // Common email closing patterns to remove
        var closingPatterns = new[]
        {
            @"(?m)^Best regards,?\s*\r?\n\[Your Name\].*$",
            @"(?m)^Sincerely,?\s*\r?\n\[Your Name\].*$",
            @"(?m)^Thanks?,?\s*\r?\n\[Your Name\].*$",
            @"(?m)^Best,?\s*\r?\n\[Your Name\].*$",
            @"(?m)^Regards?,?\s*\r?\n\[Your Name\].*$",
            @"(?m)^Best regards,?\s*$",
            @"(?m)^\[Your Name\]\s*$",
            @"(?m)^Feel free to reach out.*$",
            @"(?m)^Let me know if .*$",
            @"(?m)^If you (?:need|have) (?:any|further).*$",
            @"(?m)^Please don't hesitate.*$",
            @"(?m)^I'm here to help.*$"
        };

        var cleaned = content;
        foreach (var pattern in closingPatterns)
        {
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, pattern, "", 
                System.Text.RegularExpressions.RegexOptions.Multiline | 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // Remove trailing whitespace
        cleaned = cleaned.TrimEnd();

        return cleaned;
    }

    /// <summary>
    /// Helper method to rank and optimize search results for RAG context
    /// </summary>
    protected OptimizedRagContext OptimizeSearchResults(
        List<string> searchResults,
        string query,
        int maxTokens = 1500)
    {
        if (searchResults == null || searchResults.Count == 0)
        {
            return new OptimizedRagContext
            {
                Results = new List<RankedSearchResult>(),
                TotalTokens = 0,
                OriginalResultCount = 0,
                WasOptimized = false,
                OptimizationStrategy = "No results to optimize"
            };
        }

        // Create ranked results from search strings
        var rankedResults = new List<RankedSearchResult>();
        foreach (var result in searchResults)
        {
            var relevanceScore = CalculateRelevanceScore(query, result);
            rankedResults.Add(new RankedSearchResult
            {
                Content = result,
                RelevanceScore = relevanceScore,
                Source = $"Infrastructure KB",
                TokenCount = 0
            });
        }

        // Apply RAG context optimization
        var options = new RagOptimizationOptions
        {
            MaxRagTokens = maxTokens,
            MinRelevanceScore = 0.3,
            MaxResults = 10,
            TrimLargeResults = true
        };

        var optimized = _ragContextOptimizer.OptimizeContext(rankedResults, options);
        
        _logger.LogInformation(
            "RAG optimization completed: {ResultsIncluded} results ({TokensUsed} tokens), " +
            "filtered: {Filtered}, trimmed: {Trimmed}, excluded: {Excluded}",
            optimized.Results.Count, optimized.TotalTokens,
            optimized.OriginalResultCount - optimized.Results.Count - optimized.ResultsRemoved,
            optimized.ResultsTrimmed, optimized.ResultsRemoved);

        return optimized;
    }

    /// <summary>
    /// Calculate relevance score for a search result based on query
    /// </summary>
    private double CalculateRelevanceScore(string query, string content)
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
        int matches = queryWords.Count(w => contentLower.Contains(w));
        
        // Score based on proportion of keywords found
        double score = (double)matches / queryWords.Count;
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
            RagContextPriority = 80,
            ConversationHistoryPriority = 60,
            MinRagContextItems = 3,
            MinConversationHistoryMessages = 2,
            SafetyBufferPercentage = 10,
            UseSummarization = false
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
                AgentType = AgentType.Infrastructure.ToString(),
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

            _logger.LogInformation("Infrastructure Agent - Cost metrics recorded: {Summary}", metrics.GetSummary());

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record agent cost metrics");
        }
    }

    /// <summary>
    /// Helper method to optimize conversation history for context window management
    /// </summary>
    private async Task<OptimizedConversationHistory> OptimizeConversationHistoryAsync(
        List<ConversationMessage> messages,
        IConversationHistoryOptimizer historyOptimizer,
        int tokenBudget = 4000)
    {
        try
        {
            var options = historyOptimizer.GetRecommendedOptionsForAgent("Infrastructure");
            options.MaxTokens = Math.Min(tokenBudget, options.MaxTokens);

            var optimized = await historyOptimizer.OptimizeHistoryAsync(messages, options);
            
            _logger.LogInformation("Infrastructure Agent - Conversation history optimized:\n{Summary}", 
                optimized.GetSummary());

            return optimized;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to optimize conversation history");
            return new OptimizedConversationHistory 
            { 
                Messages = messages,
                OriginalMessageCount = messages.Count
            };
        }
    }

    /// <summary>
    /// Helper method to evaluate conversation health and determine if pruning is needed
    /// </summary>
    private async Task<ConversationHealthMetrics> EvaluateConversationHealthAsync(
        List<ConversationMessage> messages,
        IConversationHistoryOptimizer historyOptimizer,
        int currentTokenCount,
        int tokenBudget = 8000)
    {
        try
        {
            var health = await historyOptimizer.EvaluateConversationHealthAsync(
                messages, 
                currentTokenCount, 
                tokenBudget);

            _logger.LogDebug("Infrastructure Agent - Conversation health evaluated:\n{Summary}", 
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
        int maxTokens = 4000)
    {
        try
        {
            var contextWindow = await historyOptimizer.GetContextWindowAsync(
                messages,
                maxTokens,
                targetMessageIndex);

            _logger.LogDebug("Infrastructure Agent - Context window managed: {TargetIndex} → {WindowSize} messages", 
                targetMessageIndex, contextWindow.Count);

            return contextWindow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to manage context window");
            return messages;
        }
    }
}
