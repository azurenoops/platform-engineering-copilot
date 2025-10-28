using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Core.Services.Chat;
using Platform.Engineering.Copilot.Core.Services.Infrastructure;
using Platform.Engineering.Copilot.Core.Services.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Services.Azure;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Services.Agents;

/// <summary>
/// Specialized agent for Azure infrastructure provisioning and management
/// </summary>
public class InfrastructureAgent : ISpecializedAgent
{
    public AgentType AgentType => AgentType.Infrastructure;

    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;
    private readonly ILogger<InfrastructureAgent> _logger;
    private readonly InfrastructurePlugin _infrastructurePlugin;

    public InfrastructureAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<InfrastructureAgent> logger,
        ILoggerFactory loggerFactory,
        IInfrastructureProvisioningService infrastructureService,
        IDeploymentOrchestrationService deploymentService,
        IDynamicTemplateGenerator templateGenerator,
        INetworkTopologyDesignService networkDesignService,
        IPredictiveScalingEngine scalingEngine,
        IComplianceAwareTemplateEnhancer complianceEnhancer,
        SharedMemory sharedMemory,
        AzureMcpClient azureMcpClient)
    {
        _logger = logger;

        // Create specialized kernel for infrastructure work
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.Infrastructure);

        // Create and register infrastructure plugin
        _infrastructurePlugin = new InfrastructurePlugin(
            loggerFactory.CreateLogger<InfrastructurePlugin>(),
            _kernel,
            infrastructureService,
            templateGenerator,
            networkDesignService,
            scalingEngine,
            complianceEnhancer,
            sharedMemory,
            azureMcpClient);

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

        try
        {
            _kernel.Plugins.AddFromObject(
                new DeploymentPlugin(
                    loggerFactory.CreateLogger<DeploymentPlugin>(),
                    _kernel,
                    deploymentService,
                    azureMcpClient),
                "Deployment");

            _logger.LogInformation("✅ Registered DeploymentPlugin for agent");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register DeploymentPlugin");
        }

        _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<AgentResponse> ProcessAsync(AgentTask task, SharedMemory memory)
    {
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("🏗️  Infrastructure Agent processing task: {TaskId}", task.TaskId);

        try
        {
            var chatHistory = new ChatHistory();

            // Agent-specific system prompt optimized for infrastructure work
            chatHistory.AddSystemMessage(@"
You are an Azure infrastructure specialist with deep expertise in:
- Azure resource provisioning and lifecycle management
- Infrastructure as Code (Bicep and Terraform)
- Compliance frameworks (FedRAMP High, DoD IL5, NIST 800-53, SOC2, GDPR)
- Network topology design and subnet calculations
- Predictive scaling and auto-scaling optimization
- Networking, security groups, and virtual networks
- Storage accounts, databases, and compute resources
- Azure best practices for security, scalability, and cost optimization

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
   - User says: ""453c2549-4cc5-464f-ba66-acad920823e8"" → Use exact GUID (NOT ""your-subscription-id"")

4. **Build complete resource IDs using ACTUAL values:**
   - Correct: /subscriptions/453c2549-4cc5-464f-ba66-acad920823e8/resourceGroups/rg-ml-sbx-jrs/providers/Microsoft.Web/serverfarms/plan-ml-sbx-jrs
   - WRONG: /subscriptions/your-subscription-id/resourceGroups/your-resource-group/providers/Microsoft.Web/serverfarms/your-app-service-plan

5. **If the user does NOT provide enough detail, ASK for the specific resource name - do NOT use placeholders**

**CRITICAL: Smart Requirements Gathering!**
When a user asks to deploy, create, or set up infrastructure:

1. **FIRST**, analyze what information they've already provided in their request
2. **ONLY ask for missing critical information** - don't ask questions they've already answered
3. **If they provided basic info** (resource type, location, size), ask ONLY the missing items in ONE message
4. **When they answer your follow-up questions**, IMMEDIATELY call the appropriate generation function - DO NOT ask for confirmation or ask the same questions again
5. **NEVER repeat questions** - if the user has provided an answer (even partial), accept it and use smart defaults for anything minor that's missing

**IMPORTANT: One Question Cycle Only!**
- First message: User requests infrastructure → Ask for missing critical info (if any)
- Second message: User provides answers → **IMMEDIATELY generate the template**
- **DO NOT** ask a third time or ask for confirmation - just generate!

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

User: ""I need to deploy a new Kubernetes cluster in Azure. Can you help me set up an AKS cluster in the US GOV Virginia region with 3 nodes in subscription 453c2549-4cc5-464f-ba66-acad920823e8?""

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
- subscriptionId: ""453c2549-4cc5-464f-ba66-acad920823e8""
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
   - subscriptionId: ALWAYS use the actual subscription ID provided (e.g., 453c2549-4cc5-464f-ba66-acad920823e8)
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
                
                // Add previous USER messages from this conversation (most important!)
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

            // Invoke chat completion with plugin auto-invocation
            _logger.LogInformation("InfrastructureAgent: Starting OpenAI chat completion for conversation {ConversationId}", task.ConversationId);
            var result = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = 0.1, // Very low temperature to ensure function calling
                    MaxTokens = 4000
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

            // Clean the response content - remove email signatures
            var cleanedContent = CleanResponseContent(result.Content ?? "No content generated");

            // Store result in shared memory for other agents
            memory.AddAgentCommunication(
                task.ConversationId,
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
}
