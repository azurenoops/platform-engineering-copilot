using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Platform.Engineering.Copilot.Compliance.Agent.Plugins;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.Chat;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.Core.Services.TokenManagement;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Agents;

/// <summary>
/// Sub-agent for ATO documentation generation and compliance artifact management
/// Creates SSP, SAR, POA&M documents using AI-enhanced generation with assessment data
/// Validates documents against NIST, FedRAMP, and DoD standards
/// Orchestrated internally by ComplianceAgent (not a top-level ISpecializedAgent)
/// </summary>
public class DocumentAgent
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService? _chatCompletion;
    private readonly ILogger<DocumentAgent> _logger;
    private readonly TokenManagementHelper? _tokenHelper;
    private readonly DocumentAgentOptions _options;
    private readonly IDocumentGenerationService? _documentGenerationService;
    private readonly IAtoComplianceEngine? _complianceEngine;

    public DocumentAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<DocumentAgent> logger,
        CompliancePlugin compliancePlugin,
        Platform.Engineering.Copilot.Core.Plugins.ConfigurationPlugin configurationPlugin,
        IOptions<DocumentAgentOptions> options,
        IDocumentGenerationService? documentGenerationService = null,
        IAtoComplianceEngine? complianceEngine = null,
        TokenManagementHelper? tokenHelper = null)
    {
        _logger = logger;
        _tokenHelper = tokenHelper;
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _documentGenerationService = documentGenerationService;
        _complianceEngine = complianceEngine;
        
        // Create specialized kernel for document operations
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.Compliance);
        
        // Try to get chat completion service
        try
        {
            _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            
            if (_options.EnableAIGeneration)
            {
                _logger.LogInformation("✅ Document Agent initialized with AI chat completion service for enhanced document generation");
            }
            else
            {
                _logger.LogWarning("⚠️ AI generation disabled in configuration. Using template-based generation only.");
                _chatCompletion = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Document Agent initialized without AI chat completion service. AI features will be limited.");
            _chatCompletion = null;
        }

        // Register shared configuration plugin (set_azure_subscription, get_azure_subscription, etc.)
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(configurationPlugin, "ConfigurationPlugin"));
        
        // Register compliance plugin (includes document generation utilities)
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(compliancePlugin, "CompliancePlugin"));
        
        _logger.LogInformation("📄 Document Agent initialized:");
        _logger.LogInformation("  ✓ AI Generation: {AIEnabled}", _options.EnableAIGeneration);
        _logger.LogInformation("  ✓ Templates: {TemplatesEnabled}", _options.EnableTemplates);
        _logger.LogInformation("  ✓ Max Doc Size: {MaxDocMB}MB", _options.DocumentGeneration.MaxDocumentSizeMB);
        _logger.LogInformation("  ✓ Supported Formats: {Formats}", string.Join(", ", _options.DocumentGeneration.SupportedFormats));
        _logger.LogInformation("  ✓ RAG Support: {RAGAvailable}", _tokenHelper != null);
        _logger.LogInformation("  ✓ Assessment Integration: {AssessmentAvailable}", _complianceEngine != null);
    }

    public async Task<string> GenerateSSPAsync(AgentTask task, SharedMemory memory, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📋 Generating SSP document with AI enhancement");

        try
        {
            var context = memory.GetContext(task.ConversationId ?? "default");
            var subscriptionId = context?.WorkflowState.ContainsKey("lastSubscriptionId") == true
                ? context.WorkflowState["lastSubscriptionId"].ToString()
                : "default";

            // Gather assessment data: Try SharedMemory first, then database
            string assessmentData = await GetAssessmentDataAsync(
                context, 
                subscriptionId, 
                cancellationToken);

            // Build AI prompt with assessment context
            var systemPrompt = BuildSSPSystemPrompt();
            var userMessage = BuildSSPUserMessage(task, memory, assessmentData);

            // Generate with AI
            var result = await InvokeWithFunctionCallingAsync(systemPrompt, userMessage);

            _logger.LogInformation("✅ SSP generated successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error generating SSP");
            throw;
        }
    }

    /// <summary>
    /// Generate SAR (Security Assessment Report) using assessment findings
    /// Called directly by ComplianceAgent for document generation
    /// </summary>
    public async Task<string> GenerateSARAsync(AgentTask task, SharedMemory memory, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📋 Generating SAR document with assessment findings");

        try
        {
            var context = memory.GetContext(task.ConversationId ?? "default");
            var subscriptionId = context?.WorkflowState.ContainsKey("lastSubscriptionId") == true
                ? context.WorkflowState["lastSubscriptionId"].ToString()
                : "default";

            // Gather assessment findings: Try SharedMemory first, then database
            string assessmentFindings = await GetAssessmentDataAsync(
                context, 
                subscriptionId, 
                cancellationToken);

            var systemPrompt = BuildSARSystemPrompt();
            var userMessage = BuildSARUserMessage(task, memory, assessmentFindings);

            var result = await InvokeWithFunctionCallingAsync(systemPrompt, userMessage);

            _logger.LogInformation("✅ SAR generated successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error generating SAR");
            throw;
        }
    }

    /// <summary>
    /// Generate POA&M (Plan of Action & Milestones) from findings
    /// Called directly by ComplianceAgent for remediation planning
    /// </summary>
    public async Task<string> GeneratePOAMAsync(AgentTask task, SharedMemory memory, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📋 Generating POA&M document from assessment findings");

        try
        {
            var context = memory.GetContext(task.ConversationId ?? "default");
            var subscriptionId = context?.WorkflowState.ContainsKey("lastSubscriptionId") == true
                ? context.WorkflowState["lastSubscriptionId"].ToString()
                : "default";

            // Get findings from SharedMemory first, then database
            string findings = await GetAssessmentDataAsync(
                context, 
                subscriptionId, 
                cancellationToken);

            var systemPrompt = BuildPOAMSystemPrompt();
            var userMessage = BuildPOAMUserMessage(task, memory, findings);

            var result = await InvokeWithFunctionCallingAsync(systemPrompt, userMessage);

            _logger.LogInformation("✅ POA&M generated successfully ({FindingsCount} findings)", CountFindings(findings));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error generating POA&M");
            throw;
        }
    }

    /// <summary>
    /// Verify and validate a document against NIST/FedRAMP standards
    /// Called directly by ComplianceAgent for compliance validation
    /// </summary>
    public async Task<string> VerifyDocumentAsync(AgentTask task, SharedMemory memory, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔍 Verifying document compliance with standards");

        try
        {
            var systemPrompt = BuildDocumentVerificationSystemPrompt();
            var userMessage = BuildDocumentVerificationMessage(task, memory);

            var result = await InvokeWithFunctionCallingAsync(systemPrompt, userMessage);

            _logger.LogInformation("✅ Document verification completed successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error verifying document");
            throw;
        }
    }

    /// <summary>
    /// Process general document guidance and knowledge queries
    /// Called directly by ComplianceAgent for document-related questions
    /// </summary>
    public async Task<string> ProcessDocumentQueryAsync(AgentTask task, SharedMemory memory, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📄 Processing document guidance request");

        try
        {
            var context = memory.GetContext(task.ConversationId ?? "default");
            var systemPrompt = BuildSystemPrompt();
            var userMessage = BuildUserMessage(task, memory);

            var result = await InvokeWithFunctionCallingAsync(systemPrompt, userMessage);

            if (context != null)
            {
                var response = new AgentResponse
                {
                    TaskId = task.TaskId,
                    AgentType = AgentType.Compliance,
                    Success = true,
                    Content = result
                };
                context.PreviousResults.Add(response);
            }

            _logger.LogInformation("✅ Document guidance query completed");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing document query");
            throw;
        }
    }

    /// <summary>
    /// Invoke AI with function calling and proper settings
    /// </summary>
    private async Task<string> InvokeWithFunctionCallingAsync(string systemPrompt, string userMessage)
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);
        chatHistory.AddUserMessage(userMessage);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = _options.Temperature,
            MaxTokens = _options.MaxTokens,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        var result = await _chatCompletion!.GetChatMessageContentAsync(
            chatHistory,
            executionSettings,
            _kernel);

        return result.Content ?? "No response generated.";
    }

    #region System Prompts

    private string BuildSSPSystemPrompt()
    {
        return @"You are an expert System Security Plan (SSP) author specializing in NIST SP 800-18 compliance and FedRAMP guidelines.

**YOUR TASK:**
Generate a comprehensive System Security Plan (SSP) document using:
1. Current compliance assessment data
2. System architecture and boundaries
3. Control implementation details
4. Roles and responsibilities

**DOCUMENT STRUCTURE:**
- Executive Summary
- System Description & Boundaries
- System Security Plan Organization
- Security Control Implementation (by control family)
- Evidence and Supporting Documentation

**KEY REQUIREMENTS:**
- Follow NIST SP 800-18 format
- Include FedRAMP baseline requirements if applicable
- Reference specific control implementation details
- Distinguish between customer and inherited (Azure) responsibilities
- Maintain consistent terminology and formatting
- Include clear ownership assignments

**VALIDATION:**
- Ensure all required sections are present
- Verify control descriptions reference implementation details
- Check consistency across all sections
- Validate references to evidence artifacts";
    }

    private string BuildSARSystemPrompt()
    {
        return @"You are an expert Security Assessment Report (SAR) author specializing in NIST SP 800-53A methodology.

**YOUR TASK:**
Generate a Security Assessment Report based on:
1. Assessment findings and test results
2. Control testing methodology
3. Evidence evaluation
4. Risk categorization

**DOCUMENT STRUCTURE:**
- Assessment Overview
- Control-by-Control Assessment Results
- Findings Summary (Open/Closed, Severity)
- Evidence Evaluation
- Risk Assessment
- Recommendations

**KEY REQUIREMENTS:**
- Use NIST SP 800-53A assessment procedures
- Categorize findings by severity
- Provide evidence references for each finding
- Include assessment methodology
- Recommend remediation priorities
- Maintain objectivity and clarity

**VALIDATION:**
- Ensure all assessed controls are documented
- Verify evidence supports findings
- Check severity ratings are appropriate
- Validate findings are clearly documented";
    }

    private string BuildPOAMSystemPrompt()
    {
        return @"You are an expert Plan of Action & Milestones (POA&M) author specializing in remediation planning.

**YOUR TASK:**
Generate a POA&M document that:
1. Lists all open findings from assessments
2. Defines remediation plans and milestones
3. Assigns accountability and resources
4. Tracks progress toward closure

**DOCUMENT STRUCTURE:**
- POA&M Overview
- Findings Listing with Remediation Plans
- Milestones and Timeline
- Resource Requirements
- Responsible Parties and Contacts
- Status Tracking

**KEY REQUIREMENTS:**
- Each finding has clear remediation plan
- Realistic milestones with target dates
- Resource requirements are documented
- Responsible parties are assigned
- Progress tracking mechanism is defined
- Priorities reflect risk severity

**VALIDATION:**
- Ensure all open findings are included
- Verify remediation plans are feasible
- Check milestones are realistic
- Validate responsible parties are assigned";
    }

    private string BuildDocumentVerificationSystemPrompt()
    {
        return @"You are an expert document compliance reviewer for federal security documentation.

**YOUR TASK:**
Verify and validate a security document against applicable standards:
1. NIST SP 800-18 (SSP)
2. NIST SP 800-53A (SAR)
3. FedRAMP guidelines
4. DoD RMF requirements

**VALIDATION CHECKS:**
- Document completeness
- Formatting and structure
- Content accuracy and consistency
- Evidence support and references
- Control coverage
- Compliance standard adherence

**REPORTING:**
- Identify gaps or missing sections
- Flag inconsistencies
- Recommend improvements
- Prioritize issues by severity
- Provide specific remediation guidance";
    }

    private string BuildSystemPrompt()
    {
        return @"You are the Document Agent, an expert in ATO documentation generation and compliance artifact management.

**YOUR ROLE:**
Generate, format, manage, and maintain compliance documentation for Authority to Operate (ATO) processes.

**EXPERTISE:**
- System Security Plan (SSP) authoring
- Security Assessment Report (SAR) writing
- Plan of Action & Milestones (POA&M) creation
- Control implementation narratives
- Evidence artifact management
- Document formatting and standards
- Template management
- Compliance validation

**WORKFLOW:**
- For SSP generation: Gather system architecture, controls, evidence
- For SAR generation: Use assessment results and findings
- For POA&M generation: Reference open findings and remediation plans
- For document verification: Validate against applicable standards
- For control narratives: Describe implementation and supporting evidence";
    }

    #endregion

    #region User Message Builders

    private string BuildSSPUserMessage(AgentTask task, SharedMemory memory, string assessmentData)
    {
        var context = memory.GetContext(task.ConversationId ?? "default");
        var message = task.Description;

        if (!string.IsNullOrEmpty(assessmentData))
        {
            message = $@"ASSESSMENT DATA CONTEXT:
{assessmentData}

USER REQUEST: {task.Description}";
        }

        if (context?.WorkflowState.ContainsKey("systemName") == true)
        {
            message += $"\n- System Name: {context.WorkflowState["systemName"]}";
        }

        if (context?.WorkflowState.ContainsKey("baseline") == true)
        {
            message += $"\n- Baseline: {context.WorkflowState["baseline"]}";
        }

        return message;
    }

    private string BuildSARUserMessage(AgentTask task, SharedMemory memory, string assessmentFindings)
    {
        var message = task.Description;

        if (!string.IsNullOrEmpty(assessmentFindings))
        {
            message = $@"ASSESSMENT FINDINGS:
{assessmentFindings}

USER REQUEST: {task.Description}";
        }

        return message;
    }

    private string BuildPOAMUserMessage(AgentTask task, SharedMemory memory, string findings)
    {
        var message = task.Description;

        if (!string.IsNullOrEmpty(findings))
        {
            message = $@"OPEN FINDINGS:
{findings}

USER REQUEST: {task.Description}";
        }

        return message;
    }

    private string BuildDocumentVerificationMessage(AgentTask task, SharedMemory memory)
    {
        return task.Description;
    }

    private string BuildUserMessage(AgentTask task, SharedMemory memory)
    {
        var context = memory.GetContext(task.ConversationId ?? "default");
        var message = task.Description;

        if (context?.WorkflowState.ContainsKey("lastSubscriptionId") == true)
        {
            var subscriptionId = context.WorkflowState["lastSubscriptionId"];
            message = $@"SAVED CONTEXT:
- Last scanned subscription: {subscriptionId}

USER REQUEST: {task.Description}";
        }

        return message;
    }

    #endregion

    #region Helper Methods

    private string ExtractFindingsFromContext(ConversationContext? context)
    {
        if (context == null) return "";

        if (context.WorkflowState.ContainsKey("recentFindings"))
        {
            return context.WorkflowState["recentFindings"].ToString() ?? "";
        }

        if (context.PreviousResults.Count > 0)
        {
            var recentResults = context.PreviousResults.TakeLast(3)
                .Select(r => r.Content)
                .Where(c => !string.IsNullOrEmpty(c));
            
            return string.Join("\n---\n", recentResults);
        }

        return "";
    }

    private int CountFindings(string findings)
    {
        if (string.IsNullOrEmpty(findings)) return 0;
        // Simple count of "finding" occurrences
        return findings.Split(new[] { "finding", "Finding" }, StringSplitOptions.None).Length - 1;
    }

    /// <summary>
    /// Get assessment data with fallback chain: SharedMemory (WorkflowState) → SharedMemory (PreviousResults) → Database
    /// </summary>
    private async Task<string> GetAssessmentDataAsync(
        ConversationContext? context, 
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        // Priority 1: Check SharedMemory WorkflowState (current session data)
        if (context?.WorkflowState.ContainsKey("recentAssessment") == true)
        {
            var data = context.WorkflowState["recentAssessment"].ToString();
            if (!string.IsNullOrEmpty(data))
            {
                _logger.LogInformation("📊 Assessment data retrieved from SharedMemory (WorkflowState)");
                return data;
            }
        }

        // Priority 2: Check SharedMemory PreviousResults (conversation history)
        if (context?.PreviousResults.Count > 0)
        {
            var recentResults = context.PreviousResults
                .Where(r => r.AgentType == AgentType.Compliance)
                .TakeLast(1)
                .Select(r => r.Content)
                .FirstOrDefault();
            
            if (!string.IsNullOrEmpty(recentResults))
            {
                _logger.LogInformation("📊 Assessment data retrieved from SharedMemory (PreviousResults)");
                return recentResults;
            }
        }

        // Priority 3: Query Database (cross-session persistence)
        if (_complianceEngine != null && !string.IsNullOrEmpty(subscriptionId) && subscriptionId != "default")
        {
            try
            {
                _logger.LogInformation("📊 Querying database for latest assessment (subscription: {SubscriptionId})", subscriptionId);
                
                var latestAssessment = await _complianceEngine.GetLatestAssessmentAsync(subscriptionId, cancellationToken);
                
                if (latestAssessment != null)
                {
                    // Format assessment data for document generation
                    var assessmentSummary = FormatAssessmentForDocuments(latestAssessment);
                    _logger.LogInformation("✅ Assessment data retrieved from database - {FindingsCount} findings", latestAssessment.TotalFindings);
                    
                    // Cache it in WorkflowState for future use in this session
                    if (context != null)
                    {
                        context.WorkflowState["recentAssessment"] = assessmentSummary;
                        _logger.LogInformation("💾 Cached assessment data in SharedMemory for future use");
                    }
                    
                    return assessmentSummary;
                }
                else
                {
                    _logger.LogWarning("⚠️ No assessment found in database for subscription {SubscriptionId}", subscriptionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Error retrieving assessment from database: {Error}", ex.Message);
            }
        }

        // Fallback: No assessment data available
        _logger.LogWarning("⚠️ No assessment data available (SharedMemory or Database). Document will be generated from templates only.");
        return "";
    }

    /// <summary>
    /// Format AtoComplianceAssessment for document generation
    /// </summary>
    private string FormatAssessmentForDocuments(AtoComplianceAssessment assessment)
    {
        if (assessment == null) return "";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Assessment ID: {assessment.AssessmentId}");
        sb.AppendLine($"Subscription ID: {assessment.SubscriptionId}");
        sb.AppendLine($"Assessment Date: {assessment.EndTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Compliance Score: {assessment.OverallComplianceScore:F2}%");
        sb.AppendLine();

        // Summary of findings by severity
        sb.AppendLine("## Finding Summary by Severity");
        sb.AppendLine($"- Total Findings: {assessment.TotalFindings}");
        sb.AppendLine($"- Critical: {assessment.CriticalFindings}");
        sb.AppendLine($"- High: {assessment.HighFindings}");
        sb.AppendLine($"- Medium: {assessment.MediumFindings}");
        sb.AppendLine($"- Low: {assessment.LowFindings}");
        sb.AppendLine($"- Informational: {assessment.InformationalFindings}");
        sb.AppendLine();

        // Control family results
        if (assessment.ControlFamilyResults != null && assessment.ControlFamilyResults.Any())
        {
            sb.AppendLine("## Control Family Compliance Status");
            foreach (var familyKvp in assessment.ControlFamilyResults)
            {
                var family = familyKvp.Value;
                sb.AppendLine($"### {family.ControlFamily} - {family.FamilyName}");
                sb.AppendLine($"- Compliance Score: {family.ComplianceScore:F2}%");
                sb.AppendLine($"- Passed Controls: {family.PassedControls}/{family.TotalControls}");
                
                if (family.Findings != null && family.Findings.Any())
                {
                    sb.AppendLine($"- Findings: {family.Findings.Count}");
                    foreach (var finding in family.Findings.Take(5)) // Limit to first 5 findings
                    {
                        sb.AppendLine($"  - **{finding.Title}** ({finding.Severity})");
                    }
                    if (family.Findings.Count > 5)
                    {
                        sb.AppendLine($"  - ... and {family.Findings.Count - 5} more findings");
                    }
                }
                sb.AppendLine();
            }
        }

        // Executive summary
        if (!string.IsNullOrEmpty(assessment.ExecutiveSummary))
        {
            sb.AppendLine("## Executive Summary");
            sb.AppendLine(assessment.ExecutiveSummary);
            sb.AppendLine();
        }

        // Recommendations
        if (assessment.Recommendations != null && assessment.Recommendations.Any())
        {
            sb.AppendLine("## Recommendations");
            foreach (var rec in assessment.Recommendations.Take(10)) // Limit to first 10 recommendations
            {
                sb.AppendLine($"- {rec}");
            }
            if (assessment.Recommendations.Count > 10)
            {
                sb.AppendLine($"- ... and {assessment.Recommendations.Count - 10} more recommendations");
            }
        }

        return sb.ToString();
    }

    #endregion
}
