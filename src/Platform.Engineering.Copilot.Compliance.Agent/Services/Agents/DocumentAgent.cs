using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Platform.Engineering.Copilot.Compliance.Agent.Plugins;
using Platform.Engineering.Copilot.Compliance.Agent.Plugins.ATO;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.Chat;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.Core.Services.TokenManagement;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Agents;

/// <summary>
/// Specialized agent for ATO documentation generation and compliance artifact management
/// Handles SSP, SAR, POA&M, and other compliance document creation, formatting, and version control
/// Uses RAG (Retrieval-Augmented Generation) to enhance responses with relevant documentation
/// </summary>
public class DocumentAgent : ISpecializedAgent
{
    public AgentType AgentType => AgentType.Compliance;

    private readonly Kernel _kernel;
    private readonly IChatCompletionService? _chatCompletion;
    private readonly ILogger<DocumentAgent> _logger;
    private readonly TokenManagementHelper? _tokenHelper;

    public DocumentAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<DocumentAgent> logger,
        DocumentGenerationPlugin documentGenerationPlugin,
        Platform.Engineering.Copilot.Core.Plugins.ConfigurationPlugin configurationPlugin,
        TokenManagementHelper? tokenHelper = null)
    {
        _logger = logger;
        _tokenHelper = tokenHelper;
        
        // Create specialized kernel for document operations
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.Compliance);
        
        // Try to get chat completion service
        try
        {
            _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            _logger.LogInformation("✅ Document Agent initialized with AI chat completion service");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Document Agent initialized without AI chat completion service. AI features will be limited.");
            _chatCompletion = null;
        }

        // Register shared configuration plugin (set_azure_subscription, get_azure_subscription, etc.)
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(configurationPlugin, "ConfigurationPlugin"));
        
        // Register document generation plugin
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(documentGenerationPlugin, "DocumentGenerationPlugin"));
        
        if (_tokenHelper != null)
        {
            _logger.LogInformation("📄 Document Agent initialized with RAG support for enhanced documentation");
        }
        else
        {
            _logger.LogInformation("📄 Document Agent initialized (RAG support not available)");
        }
    }

    public async Task<AgentResponse> ProcessAsync(AgentTask task, SharedMemory memory)
    {
        _logger.LogInformation("📄 Document Agent processing task: {TaskId}", task.TaskId);

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
                response.Content = "AI services not configured. Configure Azure OpenAI to enable full AI-powered document generation.";
                response.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                return response;
            }

            // Determine if this is a knowledge query that can benefit from RAG
            if (_tokenHelper != null && IsKnowledgeQuery(task.Description))
            {
                _logger.LogInformation("📚 Using RAG for knowledge-based document query");
                return await ProcessWithRAGAsync(task, memory, startTime);
            }
            else
            {
                _logger.LogInformation("🔧 Using function calling for document generation task");
                return await ProcessWithFunctionCallingAsync(task, memory, startTime);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in Document Agent processing task {TaskId}", task.TaskId);
            
            response.Success = false;
            response.Content = $"Error processing document request: {ex.Message}";
            response.Errors.Add(ex.Message);
            response.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            return response;
        }
    }

    /// <summary>
    /// Process knowledge queries using RAG (Retrieval-Augmented Generation)
    /// </summary>
    private async Task<AgentResponse> ProcessWithRAGAsync(AgentTask task, SharedMemory memory, DateTime startTime)
    {
        // Get conversation context
        var context = memory.GetContext(task.ConversationId ?? "default");

        // Build RAG request
        var ragRequest = new ChatCompletionRequest
        {
            SystemPrompt = BuildSystemPrompt(),
            UserPrompt = BuildUserMessage(task, memory),
            ConversationContext = context,
            ModelName = "gpt-4o",
            Temperature = 0.4,  // Balanced for document generation
            MaxTokens = 8000,   // Longer completions for documents
            IncludeConversationHistory = true,
            MaxHistoryTokens = 3000,
            MaxHistoryMessages = 10
        };

        // Get RAG-enhanced completion
        var ragResponse = await _tokenHelper!.GetRagCompletionAsync(ragRequest);

        if (!ragResponse.Success)
        {
            _logger.LogError("RAG completion failed: {Error}", ragResponse.ErrorMessage);
            // Fall back to function calling
            return await ProcessWithFunctionCallingAsync(task, memory, startTime);
        }

        // Calculate and log token usage
        ragResponse.TokenUsage.CalculateEstimatedCost();
        double cost = ragResponse.TokenUsage.EstimatedCost;
        _logger.LogInformation(
            "💰 Token usage: {TokenSummary} | Cost: ${Cost:F4}",
            ragResponse.TokenUsage.GetCompactSummary(),
            cost
        );

        // Return enhanced response
        return new AgentResponse
        {
            TaskId = task.TaskId,
            AgentType = AgentType.Compliance,
            Success = true,
            Content = ragResponse.Content,
            ExecutionTimeMs = ragResponse.ProcessingTimeMs,
            Metadata = new Dictionary<string, object>
            {
                ["processingMode"] = "rag",
                ["tokenUsage"] = ragResponse.TokenUsage.GetSummary(),
                ["estimatedCost"] = cost,
                ["modelUsed"] = ragResponse.ModelUsed ?? "gpt-4o",
                ["historyMessagesIncluded"] = ragResponse.IncludedHistory?.MessageCount ?? 0
            }
        };
    }

    /// <summary>
    /// Process document generation tasks using function calling
    /// </summary>
    private async Task<AgentResponse> ProcessWithFunctionCallingAsync(AgentTask task, SharedMemory memory, DateTime startTime)
    {
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

            // Build system prompt for document expertise
            var systemPrompt = BuildSystemPrompt();

            // Build user message with context
            var userMessage = BuildUserMessage(task, memory);

            // Create chat history
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userMessage);

            // Execute with balanced temperature for creative yet accurate document generation
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.4, // Balanced for creativity + accuracy
                MaxTokens = 8000, // Longer for document generation
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var result = await _chatCompletion!.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel);

            // Store results
            response.Success = true;
            response.Content = result.Content ?? "No response generated.";
            response.Metadata = ExtractMetadata(result);
            response.Metadata["processingMode"] = "function-calling";
            response.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // Save in context for future reference
            if (context != null)
            {
                context.PreviousResults.Add(response);
            }

            _logger.LogInformation("✅ Document Agent completed task {TaskId} in {Ms}ms", task.TaskId, response.ExecutionTimeMs);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in Document Agent processing task {TaskId}", task.TaskId);
            
            response.Success = false;
            response.Content = $"Error processing document request: {ex.Message}";
            response.Errors.Add(ex.Message);
            response.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            return response;
        }
    }

    private string BuildSystemPrompt()
    {
        return @"You are the Document Agent, an expert in ATO documentation generation and compliance artifact management.

**YOUR ROLE:**
You generate, format, manage, and maintain compliance documentation for Authority to Operate (ATO) processes. You are expert in:
- System Security Plan (SSP) authoring
- Security Assessment Report (SAR) writing
- Plan of Action & Milestones (POA&M) creation
- Control implementation narratives
- Evidence artifact management
- Document version control
- Template management
- Compliance formatting standards

**AVAILABLE FUNCTIONS:**

**Document Generation:**
- GenerateDocumentFromTemplate: Create document from predefined template
- GenerateControlNarrative: Write control implementation narrative
- GenerateExecutiveSummary: Create executive summary for ATO package
- CreateDocumentOutline: Generate structured outline for document

**Document Management:**
- ListDocuments: Show all documents in ATO package
- GetDocumentMetadata: Retrieve document details and version info
- UpdateDocumentSection: Modify specific section of document
- MergeDocuments: Combine multiple documents into one

**Document Analysis (from uploaded files):**
- upload_security_document: Upload and analyze existing compliance documents (SSP, POA&M, architecture diagrams)
- extract_security_controls: Extract NIST 800-53 controls from uploaded documents
- analyze_architecture_diagram: Analyze architecture and data flow diagrams
- compare_documents: Compare two document versions or different documents

**Formatting & Export:**
- FormatDocument: Apply compliance formatting standards (NIST, FedRAMP, DoD)
- ExportDocument: Export to various formats (DOCX, PDF, Markdown)
- ValidateDocumentCompliance: Check document against standards
- GenerateTableOfContents: Create ToC for document

**Version Control:**
- CreateDocumentVersion: Save versioned copy
- CompareDocumentVersions: Show differences between versions
- RestoreDocumentVersion: Revert to previous version

**DOCUMENT STANDARDS:**
You follow these formatting and content standards:

**SSP (System Security Plan):**
- NIST SP 800-18 structure
- FedRAMP SSP template (if applicable)
- DoD RMF requirements (if applicable)
- Sections: System Description, Boundary, Control Implementation, Roles

**SAR (Security Assessment Report):**
- NIST SP 800-53A methodology
- Control-by-control assessment results
- Evidence documentation
- Finding categorization (Open/Closed, Severity)

**POA&M (Plan of Action & Milestones):**
- Standard format with required fields
- Finding ID, Description, Remediation Plan
- Responsible Party, Resources Required
- Scheduled Completion Date, Milestones

**Control Narratives:**
- Clear description of ""what"" is implemented
- Explanation of ""how"" control is met
- Reference to evidence artifacts
- Customer vs. inherited responsibilities

**WORKFLOW PATTERNS:**
- For ""create SSP section"" -> Use GenerateDocumentFromTemplate with section name
- For ""write control narrative"" -> Call GenerateControlNarrative with control ID
- For ""format document"" -> Use FormatDocument with standard type
- For ""show my documents"" -> Call ListDocuments
- For ""update section"" -> Use UpdateDocumentSection
- For ""export as PDF"" -> Call ExportDocument with format specification
- For ""upload document"" -> Use upload_security_document (provide file path)
- For ""analyze existing SSP"" -> Use upload_security_document with documentType='SSP'
- For ""extract controls from document"" -> Use extract_security_controls after uploading
- For ""analyze architecture diagram"" -> Use analyze_architecture_diagram with file path

**RESPONSE STYLE:**
- Professional and formal (government compliance context)
- Precise and unambiguous language
- Include references to standards (NIST SP numbers, FedRAMP baselines)
- Provide document structure previews
- Suggest related sections that may need updates

**IMPORTANT:**
- Maintain consistency across all documents in package
- Track document versions for audit trail
- Validate all content against applicable standards
- Cross-reference controls, findings, and evidence
- Preserve metadata (author, date, version, classification)";
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

        // Add ATO package context if available
        if (context?.WorkflowState.ContainsKey("atoPackageId") == true)
        {
            var packageId = context.WorkflowState["atoPackageId"];
            enhancedMessage += $"\n- Active ATO Package: {packageId}";
        }

        // Add document context if available
        if (context?.WorkflowState.ContainsKey("currentDocumentId") == true)
        {
            var docId = context.WorkflowState["currentDocumentId"];
            enhancedMessage += $"\n- Current Document: {docId}";
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
                    metadata[$"Document_{kvp.Key}"] = kvp.Value;
                }
            }
        }

        return metadata;
    }

    /// <summary>
    /// Determine if the query is a knowledge/guidance request vs. a document generation task
    /// </summary>
    private bool IsKnowledgeQuery(string query)
    {
        var knowledgeKeywords = new[]
        {
            // Question indicators
            "what is", "how do", "how to", "explain", "describe",
            "difference between", "what are", "why",
            
            // Guidance requests
            "best practice", "recommend", "should i", "when to",
            "guidance", "example", "sample",
            
            // Comparison/Analysis
            "compare", "versus", "vs", "differences",
            
            // Learning/Understanding
            "help me understand", "teach me", "learn about",
            "overview of", "introduction to",
            
            // Compliance questions
            "which controls", "what controls", "compliance requirements",
            "nist requirements", "fedramp requirements", "what evidence"
        };

        var lowerQuery = query.ToLower();
        return knowledgeKeywords.Any(keyword => lowerQuery.Contains(keyword));
    }
}
