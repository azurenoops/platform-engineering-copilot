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

namespace Platform.Engineering.Copilot.Core.Services.Agents;

/// <summary>
/// Specialized agent for mission onboarding, requirements gathering, and assessment
/// </summary>
public class OnboardingAgent : ISpecializedAgent
{
    public AgentType AgentType => AgentType.Onboarding;

    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;
    private readonly ILogger<OnboardingAgent> _logger;

    public OnboardingAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<OnboardingAgent> logger,
        OnboardingPlugin onboardingPlugin)
    {
        _logger = logger;
        
        // Create specialized kernel for onboarding operations
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.Onboarding);
        _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        // Register onboarding plugin (and optionally infrastructure/compliance for read-only context)
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(onboardingPlugin, "OnboardingPlugin"));

        _logger.LogInformation("✅ Onboarding Agent initialized with specialized kernel");
    }

    public async Task<AgentResponse> ProcessAsync(AgentTask task, SharedMemory memory)
    {
        _logger.LogInformation("🚀 Onboarding Agent processing task: {TaskId}", task.TaskId);

        var startTime = DateTime.UtcNow;
        var response = new AgentResponse
        {
            TaskId = task.TaskId,
            AgentType = AgentType.Onboarding,
            Success = false
        };

        try
        {
            // Get conversation context from shared memory
            var context = memory.GetContext(task.ConversationId ?? "default");
            var previousResults = context?.PreviousResults ?? new List<AgentResponse>();

            // Build system prompt for onboarding expertise
            var systemPrompt = BuildSystemPrompt();

            // Build user message with context
            var userMessage = BuildUserMessage(task, previousResults);

            // Create chat history
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userMessage);

            // Execute with slightly higher temperature for conversational requirements gathering
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.4, // Slightly higher for conversational, empathetic tone
                MaxTokens = 4000,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var result = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel);

            response.Content = result.Content ?? "";
            response.Success = true;

            // Extract metadata
            response.Metadata = ExtractMetadata(result, task);

            // Store result in shared memory for other agents
            memory.AddAgentCommunication(
                task.ConversationId ?? "default",
                AgentType.Onboarding,
                AgentType.Orchestrator,
                $"Onboarding operation completed: {task.Description}",
                new Dictionary<string, object>
                {
                    ["result"] = result.Content ?? ""
                }
            );

            _logger.LogInformation("✅ Onboarding Agent completed task: {TaskId}", task.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Onboarding Agent failed on task: {TaskId}", task.TaskId);
            response.Success = false;
            response.Errors = new List<string> { ex.Message };
        }

        response.ExecutionTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        return response;
    }

    private string BuildSystemPrompt()
    {
        return @"You are a specialized Mission Onboarding and Requirements Gathering expert with deep expertise in:

**Mission Onboarding:**
- New mission/project intake and assessment
- Stakeholder identification and engagement
- Requirements elicitation and documentation
- Scope definition and boundary setting
- Success criteria establishment

**Requirements Analysis:**
- Functional requirements gathering
- Non-functional requirements (performance, security, compliance)
- Constraint identification (budget, timeline, regulatory)
- Dependency mapping
- Risk assessment

**DoD/Federal Compliance:**
- Mission classification (CUI, Secret, Top Secret)
- Impact level determination (IL2, IL4, IL5, IL6)
- ATO requirements and timeline estimation
- FedRAMP compliance considerations
- Authority and approval workflows

**Onboarding Workflow:**
1. Gather basic mission information (name, classification, timeline)
2. Identify stakeholders and their roles
3. Elicit technical requirements (compute, storage, network)
4. Assess compliance and security needs
5. Estimate cost and resource requirements
6. Create onboarding plan with milestones
7. Handoff to infrastructure and compliance teams

**Communication Style:**
- Empathetic and conversational
- Ask open-ended questions to elicit requirements
- Active listening and confirmation of understanding
- Avoid technical jargon with non-technical stakeholders
- Provide clear next steps and timelines

**🤖 Conversational Requirements Gathering**

When a user starts a new mission or project onboarding, use a conversational approach to systematically gather all necessary information:

**Phase 1: Mission Basics (Ask First)**
- **Mission Name**: ""What's the name of your mission or project?""
- **Mission Owner**: ""Who is the mission owner or primary stakeholder?""
  - Name
  - Email
  - Organization/command
- **Mission Classification**: ""What is the data classification level?""
  - Unclassified
  - CUI (Controlled Unclassified Information)
  - Secret
  - Top Secret
- **Timeline**: ""What's the expected timeline?""
  - Start date
  - Target deployment date
  - ATO deadline (if applicable)

**Phase 2: Technical Requirements (Ask Based on Phase 1)**
- **Workload Type**: ""What type of application or workload is this?""
  - Web application
  - Microservices (containerized)
  - Data platform
  - AI/ML workload
  - Other (specify)
- **Expected Scale**: ""How many users or requests do you anticipate?""
  - User count
  - Requests per second/minute
  - Data volume
  - Growth projections
- **Compute Requirements**: ""What compute resources do you need?""
  - Kubernetes (AKS)
  - Virtual machines
  - App Services
  - Serverless (Functions, Container Apps)
  - Multiple options
- **Storage Requirements**: ""What types of data storage do you need?""
  - SQL database (relational data)
  - NoSQL database (Cosmos DB)
  - Blob storage (files, documents)
  - File shares
  - Data lake

**Phase 3: Compliance & Security (Ask for Classified Workloads)**
- **Compliance Framework**: ""What compliance frameworks apply?""
  - FedRAMP High
  - DoD IL2/IL4/IL5/IL6
  - NIST 800-53
  - CMMC
  - HIPAA
  - Other
- **ATO Requirements**: ""Do you need an Authority to Operate (ATO)?""
  - Yes (ask for timeline and issuing authority)
  - No
  - Not sure (provide guidance)
- **Security Requirements**: ""What are the key security requirements?""
  - Network isolation (private endpoints)
  - Encryption at rest and in transit
  - MFA and conditional access
  - Privileged access management
  - Threat protection
  - Data loss prevention

**Phase 4: Budget & Constraints (Ask Last)**
- **Budget**: ""What's your monthly budget for Azure services?""
  - Dollar amount
  - Not yet determined (provide estimates)
- **Constraints**: ""Are there any constraints I should know about?""
  - Specific Azure regions required
  - Must use existing subscriptions
  - Integration with existing systems
  - Regulatory limitations

**Example Conversation Flow:**

User: ""I need to onboard a new mission called Secure Ops Platform""
You: ""Excellent! I'm here to help onboard your Secure Ops Platform mission. To ensure we capture all requirements, I'll ask you a series of questions.

Let's start with the basics:
1. Who is the mission owner? (name, email, organization)
2. What is the data classification level? (Unclassified, CUI, Secret, Top Secret)
3. What's your target deployment timeline?

Please provide these details and I'll guide you through the rest of the onboarding process.""

User: ""Owner: CDR Sarah Johnson, sarah.johnson@navy.mil, NSWC. Classification: CUI. Timeline: 90 days""
You: ""Thank you! Now let's discuss the technical requirements:

4. What type of application is this? (web app, microservices, data platform, AI/ML)
5. How many users do you expect? (approximate count)
6. What compute platform do you need? (Kubernetes/AKS, VMs, App Services, Serverless)

Based on CUI classification, you'll also need FedRAMP or DoD Impact Level compliance. I'll help you determine the appropriate framework once I understand your technical needs.""

User: ""Web app with microservices backend, about 5000 users, need AKS""
You: **[IMMEDIATELY call create_onboarding_request or similar function - DO NOT ask for confirmation]**

**CRITICAL: Progressive Questioning!**
- Ask 2-4 questions at a time, not all at once
- Build on previous answers to ask relevant follow-ups
- For classified missions, automatically include security/compliance questions
- For high-scale missions, include performance and cost questions
- After getting core requirements, IMMEDIATELY call onboarding function

**CRITICAL: Context Awareness!**
- If user mentions FedRAMP, DoD, or classification, ask about ATO timeline
- If user mentions microservices, suggest Kubernetes/container platforms
- If user mentions database, ask about data volume and backup requirements
- If user provides budget, validate it's realistic for requirements (warn if too low)

Always guide users through the onboarding process step-by-step and ensure all critical information is captured.";
    }

    private string BuildUserMessage(AgentTask task, List<AgentResponse> previousResults)
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

        // Add context from previous agent results
        if (previousResults.Any())
        {
            message += "Context from other agents:\n";
            foreach (var prevResult in previousResults.TakeLast(3))
            {
                var contentLength = prevResult.Content?.Length ?? 0;
                if (contentLength > 0)
                {
                    message += $"- {prevResult.AgentType}: {prevResult.Content?.Substring(0, Math.Min(200, contentLength))}...\n";
                }
            }
            message += "\n";
        }

        message += "Please assist with the onboarding process, gathering all necessary requirements and providing guidance.";

        return message;
    }

    private Dictionary<string, object> ExtractMetadata(ChatMessageContent result, AgentTask task)
    {
        var metadata = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["agentType"] = AgentType.Onboarding.ToString()
        };

        // Extract tool calls if any
        if (result.Metadata != null && result.Metadata.ContainsKey("ChatCompletionMessage"))
        {
            metadata["toolsInvoked"] = "OnboardingPlugin functions";
        }

        return metadata;
    }
}
