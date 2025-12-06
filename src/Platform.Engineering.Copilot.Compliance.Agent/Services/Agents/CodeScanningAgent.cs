using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Models.Agents;
using System.Text.RegularExpressions;
using Platform.Engineering.Copilot.Compliance.Agent.Plugins;
using Platform.Engineering.Copilot.Compliance.Agent.Plugins.Code;
using Platform.Engineering.Copilot.Core.Services.Agents;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Agents;

/// <summary>
/// Specialized agent for code security scanning, vulnerability detection, and static code analysis.
/// Uses AI-driven analysis to interpret complex scanning requests and automatically invoke appropriate tools.
/// Provides intelligent code quality assessment, security vulnerability detection, and compliance analysis.
/// </summary>
public class CodeScanningAgent : ISpecializedAgent
{
    public AgentType AgentType => AgentType.Compliance;

    private readonly Kernel _kernel;
    private readonly IChatCompletionService? _chatCompletion;
    private readonly ILogger<CodeScanningAgent> _logger;

    public CodeScanningAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<CodeScanningAgent> logger,
        CodeScanningPlugin codeScanningPlugin,
        Platform.Engineering.Copilot.Core.Plugins.ConfigurationPlugin configurationPlugin)
    {
        _logger = logger;
        
        // Create specialized kernel for code scanning operations
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.Compliance);
        
        // Try to get chat completion service - make it optional for basic functionality
        try
        {
            _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            _logger.LogInformation("✅ Code Scanning Agent initialized with AI chat completion service");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Code Scanning Agent initialized without AI chat completion service. AI features will be limited.");
            _chatCompletion = null;
        }

        // Register shared configuration plugin (set_azure_subscription, get_azure_subscription, etc.)
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(configurationPlugin, "ConfigurationPlugin"));
        
        // Register code scanning plugin
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(codeScanningPlugin, "CodeScanningPlugin"));

        _logger.LogInformation("✅ Code Scanning Agent initialized with specialized kernel");
    }

    public async Task<AgentResponse> ProcessAsync(AgentTask task, SharedMemory memory)
    {
        _logger.LogInformation("🔍 Code Scanning Agent processing task: {TaskId}", task.TaskId);

        var startTime = DateTime.UtcNow;
        var response = new AgentResponse
        {
            TaskId = task.TaskId,
            Success = false,
            Content = string.Empty,
            ExecutionTimeMs = 0,
            Metadata = new Dictionary<string, object>()
        };

        try
        {
            _logger.LogInformation("🔍 Processing code scanning request: {Description}", task.Description);

            // Check if AI chat completion is available
            if (_chatCompletion == null)
            {
                _logger.LogWarning("⚠️ AI chat completion service not available. Returning basic response for task: {TaskId}", task.TaskId);
                
                response.Success = true;
                response.Content = "AI services not configured. Basic code scanning functionality available through direct tool calls only. " +
                                 "Configure Azure OpenAI to enable full AI-powered code analysis and intelligent scanning.";
                response.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                return response;
            }

            // Get conversation context from shared memory
            var context = memory.GetContext(task.ConversationId ?? "default");
            var previousResults = context?.PreviousResults ?? new List<AgentResponse>();

            // Build system prompt for code scanning expertise
            var systemPrompt = BuildCodeScanningSystemPrompt();

            // Build user message with context (including workspace metadata from SharedMemory)
            var userMessage = BuildUserMessage(task, previousResults, memory);

            // Create chat history
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userMessage);

            // Execute with moderate temperature for balanced precision and creativity
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.3, // Balanced temperature for code analysis
                MaxTokens = 4000,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var result = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel);

            // 🔍 DIAGNOSTIC: Log what the LLM actually did
            _logger.LogInformation("🔍 CodeScanningAgent DIAGNOSTIC:");
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

            // Extract code scanning metadata
            var metadata = ExtractCodeScanningMetadata(result, task);
            response.Metadata = metadata;

            // Extract security score if mentioned
            var securityScore = ExtractSecurityScore(result.Content);
            response.Metadata["SecurityScore"] = securityScore;

            // Determine risk level based on analysis
            var riskLevel = DetermineRiskLevel(result.Content);
            response.Metadata["RiskLevel"] = riskLevel;

            // Store result in shared memory for other agents
            memory.AddAgentCommunication(
                task.ConversationId ?? "default",
                AgentType.Compliance,
                AgentType.Orchestrator,
                $"Code scanning completed. Security Score: {securityScore}/100, Risk Level: {riskLevel}",
                metadata);

            _logger.LogInformation("✅ Code Scanning Agent completed processing for task: {TaskId}", task.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing code scanning request: {Description}", task.Description);
            response.Content = $"Code scanning analysis failed: {ex.Message}";
            response.Metadata["error"] = ex.ToString();
        }

            response.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
        return response;
    }

    /// <summary>
    /// Build sophisticated system prompt for code scanning expertise
    /// </summary>
    private string BuildCodeScanningSystemPrompt()
    {
        return @"# Code Security & Analysis Expert Agent

You are an elite cybersecurity and code analysis expert with deep knowledge in:

## 🔒 Security Expertise
- OWASP Top 10 vulnerabilities and mitigation strategies
- Static Application Security Testing (SAST) methodologies
- Dynamic Application Security Testing (DAST) techniques
- Dependency and supply chain security analysis
- Secret detection and credential management
- Infrastructure as Code (IaC) security best practices
- Pull Request (PR) security reviews and compliance validation

## 📐 Compliance Frameworks
- NIST 800-53 security controls implementation
- STIG (Security Technical Implementation Guide) requirements
- SOC2 Type II audit requirements
- ISO 27001 information security standards
- PCI DSS for payment card industry
- FISMA compliance for federal systems

## 🛠 Technical Analysis
- Multi-language code analysis (C#, TypeScript, Python, Java, Go, etc.)
- Container and Docker security scanning
- Kubernetes security configuration analysis
- CI/CD pipeline security integration
- Cloud security posture management (AWS, Azure, GCP)
- GitHub Pull Request integration for automated IaC compliance reviews

## 🎯 Your Mission
Analyze user requests to determine the most appropriate code scanning approach:

1. **Intelligent Tool Selection**: Choose the right combination of scanning tools based on context
2. **Adaptive Analysis**: Adjust scan depth and focus areas based on project type and requirements
3. **Risk Assessment**: Provide contextual risk analysis and prioritized remediation guidance
4. **Compliance Mapping**: Map findings to relevant compliance frameworks automatically
5. **PR Integration**: For PR-related requests, delegate to PullRequestReviewPlugin for automated reviews

## 🚀 Available Tools
Use these tools strategically based on user needs:

**Code Analysis Tools:**
- `ScanCodebaseForComplianceAsync`: Comprehensive repository-wide security and compliance analysis
- `AnalyzeFileForSecurityAsync`: Deep-dive security analysis of specific files
- `GenerateComplianceReportAsync`: Generate detailed compliance reports with remediation plans

**Pull Request Review Tools (delegate to PullRequestReviewPlugin):**
- For PR reviews: Suggest using the PullRequestReviewPlugin for automated GitHub PR compliance reviews
- Supports Bicep, Terraform, ARM templates, Kubernetes YAML
- Provides inline comments with NIST/STIG/DoD instruction references
- Phase 1 compliant: Advisory only, no auto-merge

## 📋 Response Guidelines
- Always provide actionable, specific recommendations
- Include risk levels and business impact context
- Suggest remediation priorities based on severity and compliance requirements
- Reference relevant security standards and best practices
- Format responses in clear, structured markdown
- For PR review requests, explain that automated PR integration is available via PullRequestReviewPlugin

Be proactive in suggesting comprehensive analysis approaches even if the user request is basic.
Think holistically about security - consider not just code vulnerabilities but also configuration, dependencies, and deployment security.
When users ask about PR reviews or automated code reviews, inform them about the PR review capability.";
    }

    /// <summary>
    /// Build comprehensive user message with context and workspace information
    /// </summary>
    private string BuildUserMessage(AgentTask task, List<AgentResponse> previousResults, SharedMemory memory)
    {
        var messageBuilder = new System.Text.StringBuilder();
        
        messageBuilder.AppendLine($"# Code Scanning Request");
        messageBuilder.AppendLine($"**User Request**: {task.Description}");
        messageBuilder.AppendLine($"**Task ID**: {task.TaskId}");
        messageBuilder.AppendLine($"**Timestamp**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        messageBuilder.AppendLine();

        // Add workspace context if available
        var context = memory.GetContext(task.ConversationId ?? "default");
        if (context.MessageHistory.Any())
        {
            messageBuilder.AppendLine("## 📁 Conversation Context");
            messageBuilder.AppendLine($"Messages in conversation: {context.MessageHistory.Count}");
            messageBuilder.AppendLine();
        }

        // Add previous analysis results for context
        if (previousResults.Any())
        {
            messageBuilder.AppendLine("## 📊 Previous Analysis Context");
            foreach (var result in previousResults.TakeLast(3))
            {
                messageBuilder.AppendLine($"- **{result.AgentType}**: {(string.IsNullOrEmpty(result.Content) ? "No content" : result.Content.Substring(0, Math.Min(150, result.Content.Length)))}...");
            }
            messageBuilder.AppendLine();
        }

        // Add deployment context if available
        var deploymentMetadata = memory.GetDeploymentMetadata(task.ConversationId ?? "default");
        if (deploymentMetadata != null && deploymentMetadata.Any())
        {
            messageBuilder.AppendLine("## 🚀 Deployment Context");
            foreach (var kvp in deploymentMetadata)
            {
                messageBuilder.AppendLine($"- **{kvp.Key}**: {kvp.Value}");
            }
            messageBuilder.AppendLine();
        }

        messageBuilder.AppendLine("## 🎯 Analysis Instructions");
        messageBuilder.AppendLine("Based on the user request and context:");
        messageBuilder.AppendLine("1. Determine the most appropriate scanning approach");
        messageBuilder.AppendLine("2. Select and configure the right analysis tools");
        messageBuilder.AppendLine("3. Provide comprehensive security and compliance assessment");
        messageBuilder.AppendLine("4. Include specific, actionable remediation recommendations");

        return messageBuilder.ToString();
    }

    /// <summary>
    /// Extract sophisticated metadata from AI analysis results
    /// </summary>
    private Dictionary<string, object> ExtractCodeScanningMetadata(ChatMessageContent result, AgentTask task)
    {
        var metadata = new Dictionary<string, object>
        {
            ["agent_type"] = AgentType.Compliance.ToString(),
            ["task_id"] = task.TaskId,
            ["analysis_timestamp"] = DateTime.UtcNow,
            ["tools_invoked"] = "CodeScanningPlugin functions via AI",
            ["ai_model_used"] = result.ModelId ?? "unknown"
        };

        // Extract scan type from content
        var content = result.Content ?? "";
        if (content.Contains("Repository") || content.Contains("Codebase"))
            metadata["scan_type"] = "repository_scan";
        else if (content.Contains("File") && content.Contains("Analysis"))
            metadata["scan_type"] = "file_analysis";
        else if (content.Contains("Compliance") && content.Contains("Report"))
            metadata["scan_type"] = "compliance_report";
        else
            metadata["scan_type"] = "general_analysis";

        // Extract mentioned frameworks
        var frameworks = new List<string>();
        if (content.Contains("NIST")) frameworks.Add("NIST-800-53");
        if (content.Contains("STIG")) frameworks.Add("STIG");
        if (content.Contains("SOC2")) frameworks.Add("SOC2");
        if (content.Contains("OWASP")) frameworks.Add("OWASP");
        if (frameworks.Any()) metadata["frameworks_analyzed"] = frameworks;

        // Extract vulnerability categories mentioned
        var vulnTypes = new List<string>();
        if (content.Contains("SQL injection", StringComparison.OrdinalIgnoreCase)) vulnTypes.Add("SQL Injection");
        if (content.Contains("XSS", StringComparison.OrdinalIgnoreCase)) vulnTypes.Add("Cross-Site Scripting");
        if (content.Contains("authentication", StringComparison.OrdinalIgnoreCase)) vulnTypes.Add("Authentication Issues");
        if (content.Contains("secret", StringComparison.OrdinalIgnoreCase)) vulnTypes.Add("Secret Exposure");
        if (vulnTypes.Any()) metadata["vulnerability_categories"] = vulnTypes;

        // Extract function calls from result items
        if (result.Items != null && result.Items.Any())
        {
            var functionCalls = result.Items
                .Where(item => item != null)
                .Select(item => item.GetType().Name)
                .ToList();
            metadata["ai_function_calls"] = functionCalls;
        }

        return metadata;
    }

    /// <summary>
    /// Extract security score from analysis content
    /// </summary>
    private int ExtractSecurityScore(string? content)
    {
        if (string.IsNullOrEmpty(content)) return 50; // Default neutral score

        // Look for explicit scores
        var scoreRegex = new Regex(@"(?:security\s+score|score)[:\s]*(\d+)(?:%|\s|$)", RegexOptions.IgnoreCase);
        var match = scoreRegex.Match(content);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var score))
        {
            return Math.Max(0, Math.Min(100, score));
        }

        // Calculate score based on risk indicators
        var riskIndicators = 0;
        var positiveIndicators = 0;

        // Risk factors (decrease score)
        if (content.Contains("critical", StringComparison.OrdinalIgnoreCase)) riskIndicators += 30;
        if (content.Contains("high risk", StringComparison.OrdinalIgnoreCase)) riskIndicators += 20;
        if (content.Contains("vulnerability", StringComparison.OrdinalIgnoreCase)) riskIndicators += 15;
        if (content.Contains("exposed", StringComparison.OrdinalIgnoreCase)) riskIndicators += 10;
        if (content.Contains("insecure", StringComparison.OrdinalIgnoreCase)) riskIndicators += 10;

        // Positive factors (increase score)
        if (content.Contains("secure", StringComparison.OrdinalIgnoreCase)) positiveIndicators += 15;
        if (content.Contains("compliant", StringComparison.OrdinalIgnoreCase)) positiveIndicators += 10;
        if (content.Contains("best practice", StringComparison.OrdinalIgnoreCase)) positiveIndicators += 10;
        if (content.Contains("no issues", StringComparison.OrdinalIgnoreCase)) positiveIndicators += 20;

        var calculatedScore = 70 - riskIndicators + positiveIndicators; // Start from 70 (neutral-good)
        return Math.Max(0, Math.Min(100, calculatedScore));
    }

    /// <summary>
    /// Determine risk level based on analysis content and security score
    /// </summary>
    private string DetermineRiskLevel(string? content)
    {
        if (string.IsNullOrEmpty(content)) return "Medium";

        var securityScore = ExtractSecurityScore(content);
        
        // Check for explicit risk level mentions
        if (content.Contains("critical risk", StringComparison.OrdinalIgnoreCase) || 
            content.Contains("high risk", StringComparison.OrdinalIgnoreCase) ||
            securityScore < 40)
            return "High";

        if (content.Contains("low risk", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("minimal risk", StringComparison.OrdinalIgnoreCase) ||
            securityScore > 80)
            return "Low";

        return "Medium";
    }
}
