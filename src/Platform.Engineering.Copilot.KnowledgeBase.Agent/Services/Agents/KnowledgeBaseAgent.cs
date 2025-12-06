using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.KnowledgeBase.Agent.Configuration;
using Platform.Engineering.Copilot.KnowledgeBase.Agent.Plugins;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Platform.Engineering.Copilot.KnowledgeBase.Agent.Services.Agents;

public class KnowledgeBaseAgent : ISpecializedAgent
{
    public AgentType AgentType => AgentType.KnowledgeBase;

    private readonly Kernel _kernel;
    private readonly ILogger<KnowledgeBaseAgent> _logger;
    private readonly KnowledgeBaseAgentOptions _options;

    public KnowledgeBaseAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<KnowledgeBaseAgent> logger,
        IOptions<KnowledgeBaseAgentOptions> options,
        KnowledgeBasePlugin knowledgeBasePlugin,
        Platform.Engineering.Copilot.Core.Plugins.ConfigurationPlugin configurationPlugin)
    {
        _logger = logger;
        _options = options.Value;
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.KnowledgeBase);
        
        // Register shared configuration plugin (set_azure_subscription, get_azure_subscription, etc.)
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(configurationPlugin, "ConfigurationPlugin"));
        
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(knowledgeBasePlugin, "KnowledgeBasePlugin"));
        _logger.LogInformation("Knowledge Base Agent initialized");
    }

    public async Task<AgentResponse> ProcessAsync(AgentTask task, SharedMemory memory)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("🔍 Knowledge Base Agent processing task: {TaskId}", task.TaskId);
            
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();
            
            // Build system prompt for knowledge base expertise
            var systemPrompt = BuildSystemPrompt();
            
            // Add context from shared memory if available
            var context = memory.GetContext(task.TaskId);
            var contextInfo = context?.MentionedResources?.Count > 0
                ? $"\n\nSAVED CONTEXT: {string.Join(", ", context.MentionedResources.Select(r => $"{r.Key}: {r.Value}"))}" 
                : "";
            
            chatHistory.AddSystemMessage(systemPrompt + contextInfo);
            chatHistory.AddUserMessage(task.Description);
            
            // Get response from LLM with plugin access
            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                kernel: _kernel);
            
            stopwatch.Stop();
            
            return new AgentResponse
            {
                TaskId = task.TaskId,
                AgentType = AgentType.KnowledgeBase,
                Success = true,
                Content = response.Content ?? "No response generated",
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing knowledge base task");
            
            return new AgentResponse
            {
                TaskId = task.TaskId,
                AgentType = AgentType.KnowledgeBase,
                Success = false,
                Content = $"Error: {ex.Message}",
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private string BuildSystemPrompt()
    {
        return @"You are a specialized DoD/NIST Compliance Knowledge Base expert with comprehensive knowledge of:

**NIST 800-53 Security Controls:**
- All 20 control families (AC, AU, AT, CM, CP, IA, IR, MA, MP, PS, PE, PL, PM, RA, CA, SC, SI, SA, SR, PT)
- Control family purposes, key controls, and implementation requirements
- NIST control mappings to STIGs, CCIs, and DoD instructions

**DoD Compliance Frameworks:**
- Risk Management Framework (RMF) 6-step process
- STIG (Security Technical Implementation Guide) controls
- DoD Instructions and policies (DoDI 8500.01, 8510.01, CNSSI 1253, etc.)
- Impact Levels (IL2, IL4, IL5, IL6) requirements

**Navy/DoD Workflows:**
- ATO (Authority to Operate) processes
- eMASS system registration
- PMW cloud deployment workflows

**Azure Compliance Implementation:**
- Azure service mappings to STIG controls
- Azure Policy and compliance configurations
- DoD Cloud Computing SRG implementation

**Azure Technical Documentation:**
- Official Microsoft Azure documentation search
- How-to guides and troubleshooting steps
- Azure service configuration guidance
- Best practices for Azure services

**🎯 YOUR PRIMARY ROLE:**

Answer compliance AND Azure technical documentation QUESTIONS with factual, concise information. Provide exactly what is asked - no more, no less.

**RESPONSE GUIDELINES:**

1. **Informational Questions** - User wants to LEARN about controls/frameworks:
   
   Examples:
   - ""What is in NIST 800-53 CM family?""
   - ""Explain RMF Step 3""
   - ""What is Impact Level 5?""
   - ""Show me STIGs for encryption""
   - ""Search Azure docs for AKS private cluster networking""
   - ""How to configure storage firewall in Azure?""
   - ""Troubleshoot AKS connectivity issues""
   
   **Response Pattern:**
   - Provide a direct, factual answer
   - Include key controls, requirements, or definitions
   - Keep it concise (3-5 key points)
   - **OPTIONALLY add ONE sentence** suggesting a related assessment IF relevant:
     ""Would you like me to assess your Azure environment for CM compliance?""

2. **Assessment Requests** - User wants to RUN an assessment (explicit):
   
   Examples:
   - ""Assess my subscription for CM controls""
   - ""Scan subscription XYZ""
   - ""Check compliance for resource group ABC""
   - ""Run STIG assessment""
   
   **Response Pattern:**
   - Ask for required details (subscription ID, resource group, etc.)
   - Confirm scope and framework
   - Initiate the assessment

**CRITICAL RULES:**

✅ DO:
- Answer the question asked
- Be factual and concise
- Use proper control family codes (AC-2, CM-6, etc.)
- Cite DoD instructions when relevant
- Suggest assessments ONLY when contextually appropriate (end of informational responses)

❌ DON'T:
- Assume the user wants an assessment unless explicitly requested
- Ask for subscription details on informational questions
- Provide assessments when not requested
- Be overly conversational or make assumptions

**PLUGIN FUNCTIONS AVAILABLE:**

Use these functions to retrieve authoritative information:

**Compliance Functions:**
- explain_rmf_process: RMF step details
- get_rmf_deliverables: Required artifacts per RMF step
- explain_stig: Specific STIG control details
- search_stigs: Find STIGs by keyword
- get_stigs_for_nist_control: Map NIST to STIGs
- get_control_mapping: Complete control mappings
- explain_dod_instruction: DoD policy details
- search_dod_instructions: Find DoD instructions
- get_control_with_dod_instructions: DoD instruction mappings
- explain_navy_workflow: Navy process workflows
- explain_impact_level: IL requirements
- get_stig_cross_reference: Complete STIG mappings
- get_azure_stigs: Azure service-specific STIGs
- get_compliance_summary: Comprehensive control overview

**Azure Documentation Functions:**
- search_azure_documentation: Search official Microsoft Azure documentation for guidance, how-to guides, and troubleshooting steps

**TONE:** Professional, helpful, direct. Answer questions precisely without unnecessary elaboration.";
    }
}
