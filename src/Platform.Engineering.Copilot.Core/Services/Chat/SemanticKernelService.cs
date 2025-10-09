using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.SemanticParsing;
using System.Text;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Service that integrates Microsoft Semantic Kernel for advanced AI-powered query processing
/// </summary>
public class SemanticKernelService : ISemanticKernelService
{
    private readonly ILogger<SemanticKernelService> _logger;
    private readonly Kernel _kernel;
    private readonly IChatCompletionService? _chatCompletion;

    public SemanticKernelService(ILogger<SemanticKernelService> logger, IConfiguration configuration)
    {
        _logger = logger;
        
        // Initialize Semantic Kernel with OpenAI or Azure OpenAI
        var builder = Kernel.CreateBuilder();
        
        // Try Azure OpenAI first (from appsettings.json)
        var azureOpenAIEndpoint = configuration.GetValue<string>("Gateway:AzureOpenAI:Endpoint");
        var azureOpenAIApiKey = configuration.GetValue<string>("Gateway:AzureOpenAI:ApiKey");
        var azureOpenAIDeployment = configuration.GetValue<string>("Gateway:AzureOpenAI:DeploymentName");
        var useManagedIdentity = configuration.GetValue<bool>("Gateway:AzureOpenAI:UseManagedIdentity");

        // Debug logging to understand configuration loading
        _logger.LogInformation("SemanticKernelService initialization - Azure OpenAI Config:");
        _logger.LogInformation("  Endpoint: {Endpoint}", azureOpenAIEndpoint ?? "NULL");
        _logger.LogInformation("  Deployment: {Deployment}", azureOpenAIDeployment ?? "NULL");
        _logger.LogInformation("  ApiKey: {ApiKeyPresent}", !string.IsNullOrEmpty(azureOpenAIApiKey) ? "Present" : "NULL");
        _logger.LogInformation("  UseManagedIdentity: {UseManagedIdentity}", useManagedIdentity);
        
        // Test if configuration object is working at all
        var testValue = configuration.GetValue<string>("Gateway:Azure:SubscriptionId");
        _logger.LogInformation("  Test Gateway:Azure:SubscriptionId: {TestValue}", testValue ?? "NULL");
        
        // Check if Gateway section exists
        var gatewaySection = configuration.GetSection("Gateway");
        _logger.LogInformation("  Gateway section exists: {Exists}", gatewaySection.Exists());
        
        // Check if AzureOpenAI subsection exists
        var azureOpenAISection = configuration.GetSection("Gateway:AzureOpenAI");
        _logger.LogInformation("  Gateway:AzureOpenAI section exists: {Exists}", azureOpenAISection.Exists());
        
        // Debug what configuration keys are available
        _logger.LogInformation("  Available configuration keys: {Keys}", 
            string.Join(", ", configuration.AsEnumerable().Take(10).Select(kvp => kvp.Key)));
        
        // Check current working directory
        _logger.LogInformation("  Current working directory: {WorkingDirectory}", Directory.GetCurrentDirectory());
        
        // Check if appsettings.json exists in current directory
        var appsettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        _logger.LogInformation("  appsettings.json exists in current dir: {Exists}", File.Exists(appsettingsPath));
        
        // Check Platform output directory
        var platformOutputPath = Path.Combine(Directory.GetCurrentDirectory(), "src/Platform.Engineering.Copilot.Platform/bin/Debug/net8.0/appsettings.json");
        _logger.LogInformation("  appsettings.json exists in Platform output: {Exists}", File.Exists(platformOutputPath));

        // Fallback to OpenAI (from environment variable)
        var openAIApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        
        if (!string.IsNullOrEmpty(azureOpenAIEndpoint) && 
            !string.IsNullOrEmpty(azureOpenAIDeployment) &&
            (!string.IsNullOrEmpty(azureOpenAIApiKey) || useManagedIdentity))
        {
            // Use Azure OpenAI
            try
            {
                if (useManagedIdentity)
                {
                    // Use DefaultAzureCredential for managed identity
                    builder.AddAzureOpenAIChatCompletion(azureOpenAIDeployment, azureOpenAIEndpoint, new Azure.Identity.DefaultAzureCredential());
                }
                else
                {
                    builder.AddAzureOpenAIChatCompletion(azureOpenAIDeployment, azureOpenAIEndpoint, azureOpenAIApiKey!);
                }
                _kernel = builder.Build();
                _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
                _logger.LogInformation("Initialized Semantic Kernel with Azure OpenAI: {Endpoint}", azureOpenAIEndpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure OpenAI, falling back to OpenAI");
                builder = Kernel.CreateBuilder(); // Reset builder
                _chatCompletion = null;
            }
        }
        else if (!string.IsNullOrEmpty(openAIApiKey) && openAIApiKey != "demo-key")
        {
            // Use OpenAI
            try
            {
                builder.AddOpenAIChatCompletion("gpt-4", openAIApiKey);
                _kernel = builder.Build();
                _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
                _logger.LogInformation("Initialized Semantic Kernel with OpenAI");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize OpenAI");
                _chatCompletion = null;
            }
        }
        else
        {
            _chatCompletion = null;
        }
        
        // Always create kernel, even if no AI service is available
        if (_kernel == null)
        {
            _kernel = builder.Build();
        }
        
        if (_chatCompletion == null)
        {
            _logger.LogWarning("No AI service configured. Semantic Kernel will operate in fallback mode without AI completion. Configure either Azure OpenAI in appsettings.json or set OPENAI_API_KEY environment variable.");
        }
    }

    public async Task<string> ProcessQueryAsync(string query, string context = "", CancellationToken cancellationToken = default)
    {
        try
        {
            if (_chatCompletion == null)
            {
                // Fallback response when no AI service is available
                return $"I understand you're asking about: '{query}'. While I can't provide AI-enhanced responses without an API key, I can help you with platform engineering tasks using rule-based analysis. {context}";
            }

            var prompt = $@"
You are an expert platform engineering assistant. Analyze the following query and provide structured insights.

Query: {query}
Context: {context}

Please provide:
1. The primary intent of the query
2. Key entities mentioned
3. Suggested actions
4. Any clarifying questions needed

Format your response as clear, actionable guidance.";

            var result = await _chatCompletion.GetChatMessageContentAsync(prompt, cancellationToken: cancellationToken);
            return result.Content ?? "Unable to process query";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query with Semantic Kernel: {Query}", query);
            return $"I can help you with platform engineering tasks. Query received: '{query}'. {context}";
        }
    }

    public async Task<string> GenerateToolSelectionReasoningAsync(string query, IEnumerable<ToolSchema> availableTools, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_chatCompletion == null)
            {
                // Fallback: Simple rule-based tool selection
                var bestTool = availableTools.FirstOrDefault(t => 
                    t.Keywords.Any(k => query.ToLowerInvariant().Contains(k.ToLowerInvariant())));
                
                if (bestTool != null)
                {
                    return JsonSerializer.Serialize(new {
                        selectedTools = new[] {
                            new {
                                toolName = bestTool.Name,
                                relevanceScore = 0.7,
                                reasoning = $"Selected based on keyword matching with query: {query}",
                                expectedParameters = bestTool.Parameters.Keys.ToArray()
                            }
                        },
                        overallReasoning = "Rule-based tool selection without AI enhancement"
                    });
                }
                return JsonSerializer.Serialize(new { selectedTools = new object[0], overallReasoning = "No matching tools found" });
            }

            var toolsJson = JsonSerializer.Serialize(availableTools.Select(t => new { t.Name, t.Description, t.Category, t.Keywords }), new JsonSerializerOptions { WriteIndented = true });
            
            var prompt = $@"
You are a platform engineering tool selection expert. Given a user query and available tools, select the most appropriate tool(s) and explain your reasoning.

User Query: {query}

Available Tools:
{toolsJson}

Please:
1. Identify the 1-3 most relevant tools for this query
2. Explain why each tool is appropriate
3. Rank them by relevance (1 = most relevant)
4. Identify any potential parameter requirements

Format as JSON with this structure:
{{
  ""selectedTools"": [
    {{
      ""toolName"": ""tool_name"",
      ""relevanceScore"": 0.95,
      ""reasoning"": ""explanation"",
      ""expectedParameters"": [""param1"", ""param2""]
    }}
  ],
  ""overallReasoning"": ""summary explanation""
}}";

            var result = await _chatCompletion.GetChatMessageContentAsync(prompt, cancellationToken: cancellationToken);
            return result.Content ?? "{}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating tool selection reasoning: {Query}", query);
            return $"{{\"error\": \"{ex.Message}\"}}";
        }
    }

    public async Task<Dictionary<string, object>> GenerateParameterSuggestionsAsync(string query, ToolSchema toolSchema, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_chatCompletion == null)
            {
                // Enhanced fallback: Pattern-based parameter extraction
                var extracted = new Dictionary<string, object>();
                
                // Special handling for azure_discover_resources
                if (toolSchema.Name == "azure_discover_resources")
                {
                    _logger.LogInformation("DEBUGGING: Processing azure_discover_resources query: {Query}", query);
                    
                    // Extract resource group names
                    var rgMatches = System.Text.RegularExpressions.Regex.Matches(query, @"resource-group\s+([a-zA-Z0-9\-_]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (rgMatches.Count > 0)
                    {
                        var resourceGroups = rgMatches.Select(m => m.Groups[1].Value).ToArray();
                        extracted["resourceGroupNames"] = resourceGroups;
                        _logger.LogInformation("DEBUGGING: Extracted resource groups: {ResourceGroups}", string.Join(", ", resourceGroups));
                    }
                    else
                    {
                        _logger.LogInformation("DEBUGGING: No resource groups found in query");
                    }
                    
                    // Extract subscription ID
                    var subMatches = System.Text.RegularExpressions.Regex.Matches(query, @"subscription\s+([a-fA-F0-9\-]{36})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (subMatches.Count > 0)
                    {
                        extracted["subscriptionId"] = subMatches[0].Groups[1].Value;
                        _logger.LogInformation("DEBUGGING: Extracted subscription ID: {SubscriptionId}", subMatches[0].Groups[1].Value);
                    }
                    else
                    {
                        _logger.LogInformation("DEBUGGING: No subscription ID found in query");
                    }
                    
                    // Extract resource types
                    var typePatterns = new[]
                    {
                        @"storage\s+accounts?", @"virtual\s+machines?", @"app\s+services?",
                        @"key\s+vaults?", @"sql\s+databases?", @"cosmos\s+db", @"redis\s+cache"
                    };
                    foreach (var pattern in typePatterns)
                    {
                        if (System.Text.RegularExpressions.Regex.IsMatch(query, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            var resourceType = pattern switch
                            {
                                var p when p.Contains("storage") => "Microsoft.Storage/storageAccounts",
                                var p when p.Contains("virtual") => "Microsoft.Compute/virtualMachines",
                                var p when p.Contains("app") => "Microsoft.Web/sites",
                                var p when p.Contains("key") => "Microsoft.KeyVault/vaults",
                                var p when p.Contains("sql") => "Microsoft.Sql/servers",
                                var p when p.Contains("cosmos") => "Microsoft.DocumentDB/databaseAccounts",
                                var p when p.Contains("redis") => "Microsoft.Cache/redis",
                                _ => null
                            };
                            if (resourceType != null)
                            {
                                extracted["resourceTypes"] = new[] { resourceType };
                                break;
                            }
                        }
                    }
                }
                
                // Special handling for cost_monitoring
                if (toolSchema.Name == "cost_monitoring")
                {
                    _logger.LogInformation("DEBUGGING: Processing cost_monitoring query: {Query}", query);
                    
                    // Extract subscription ID using the same pattern as CostMonitoringTool
                    var subMatches = System.Text.RegularExpressions.Regex.Matches(query, @"subscription\s+([a-fA-F0-9\-]{36})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (subMatches.Count > 0)
                    {
                        extracted["subscriptionId"] = subMatches[0].Groups[1].Value;
                        _logger.LogInformation("DEBUGGING: Extracted subscription ID from SemanticKernelService: {SubscriptionId}", subMatches[0].Groups[1].Value);
                    }
                    else
                    {
                        _logger.LogInformation("DEBUGGING: No subscription ID found in query by SemanticKernelService");
                    }
                    
                    // Set the query parameter for natural language processing by the tool
                    extracted["query"] = query;
                    
                    // Determine action based on keywords
                    var lowerQuery = query.ToLowerInvariant();
                    if (lowerQuery.Contains("optim") || lowerQuery.Contains("recommend") || lowerQuery.Contains("save"))
                    {
                        extracted["action"] = "optimization";
                    }
                    else if (lowerQuery.Contains("budget"))
                    {
                        extracted["action"] = "budgets";
                    }
                    else if (lowerQuery.Contains("forecast"))
                    {
                        extracted["action"] = "forecasting";
                    }
                    else
                    {
                        extracted["action"] = "breakdown";
                    }
                    
                    // Default scope and timeRange
                    extracted["scope"] = "subscription";
                    extracted["timeRange"] = "last30days";
                }
                
                // General fallback: Simple parameter extraction using examples
                foreach (var (paramName, paramSchema) in toolSchema.Parameters)
                {
                    if (!extracted.ContainsKey(paramName))
                    {
                        foreach (var example in paramSchema.Examples)
                        {
                            if (query.ToLowerInvariant().Contains(example.ToLowerInvariant()))
                            {
                                extracted[paramName] = example;
                                break;
                            }
                        }
                    }
                }
                
                _logger.LogInformation("DEBUGGING: Final extracted parameters for {ToolName}: {Parameters}", 
                    toolSchema.Name, string.Join(", ", extracted.Select(kvp => $"{kvp.Key}={kvp.Value}")));
                
                return extracted;
            }

            var parametersJson = JsonSerializer.Serialize(toolSchema.Parameters, new JsonSerializerOptions { WriteIndented = true });
            
            var prompt = $@"
You are a parameter extraction expert. Extract parameter values from a natural language query based on the tool schema.

Query: {query}
Tool: {toolSchema.Name}
Description: {toolSchema.Description}

Parameter Schema:
{parametersJson}

Please extract parameter values from the query and format as JSON. For parameters not found in the query, use null.
Only include parameters that can be confidently extracted. Use the parameter names exactly as specified in the schema.

Example response format:
{{
  ""resourceGroupName"": ""my-rg"",
  ""subscriptionId"": null,
  ""location"": ""eastus""
}}";

            var result = await _chatCompletion.GetChatMessageContentAsync(prompt, cancellationToken: cancellationToken);
            
            try
            {
                var parametersDict = JsonSerializer.Deserialize<Dictionary<string, object>>(result.Content ?? "{}");
                return parametersDict ?? new Dictionary<string, object>();
            }
            catch (JsonException)
            {
                _logger.LogWarning("Failed to parse parameter suggestions as JSON: {Content}", result.Content);
                return new Dictionary<string, object>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating parameter suggestions: {Query}", query);
            return new Dictionary<string, object>();
        }
    }

    public async Task<string> FormatResponseAsync(object result, string originalQuery, string toolName, CancellationToken cancellationToken = default)
    {
        try
        {
            var serializedResult = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            
            // Check if result is already formatted markdown (from ATO tools or onboarding tools)
            // ATO tools and onboarding tools return McpToolResult with pre-formatted markdown content
            if (result is McpToolResult mcpResult && 
                mcpResult.Content is string contentStr &&
                !string.IsNullOrEmpty(contentStr) &&
                (toolName.StartsWith("ato_") || 
                 toolName.Contains("_onboarding") || 
                 toolName.Contains("_provide_info") || 
                 toolName.Contains("_review_") || 
                 toolName.Contains("_submit_") ||
                 contentStr.Contains("##") || 
                 contentStr.Contains("✅") ||
                 contentStr.Contains("**")))
            {
                // Return the already-formatted markdown directly without AI reformatting
                return contentStr;
            }
            
            // Check if result contains markdown content in a dictionary (from onboarding tools)
            if (result is McpToolResult mcpResultDict && 
                mcpResultDict.Content is Dictionary<string, object?> contentDict &&
                contentDict.TryGetValue("markdown", out var markdownObj) &&
                markdownObj is string markdownStr &&
                !string.IsNullOrEmpty(markdownStr))
            {
                // Return the pre-formatted markdown directly
                return markdownStr;
            }
            
            if (_chatCompletion == null)
            {
                // Enhanced fallback: Cost monitoring specific formatting
                if (toolName == "cost_monitoring" && TryFormatCostResult(result, out var costMarkdown))
                {
                    return costMarkdown;
                }
                
                // Enhanced fallback: Detect template generation and format appropriately
                if (toolName.Contains("generate_") && TryExtractTemplateInfo(result, out var templateInfo))
                {
                    var language = templateInfo.TemplateType?.ToLowerInvariant() switch
                    {
                        "bicep" => "bicep",
                        "terraform" => "hcl",
                        "helm" => "yaml",
                        _ => "yaml"
                    };

                    var markdown = $"✅ **Task Completed**\n\n";
                    markdown += $"**Result**: Generated {templateInfo.TemplateType} template for {templateInfo.ResourceType}\n\n";
                    
                    if (!string.IsNullOrEmpty(templateInfo.FileName))
                    {
                        markdown += $"**File Name**: `{templateInfo.FileName}`\n\n";
                    }

                    if (!string.IsNullOrEmpty(templateInfo.Location) && templateInfo.Location != templateInfo.ResourceType)
                    {
                        markdown += $"**Location**: {templateInfo.Location}\n";
                    }
                    
                    if (!string.IsNullOrEmpty(templateInfo.Sku) && templateInfo.Sku != templateInfo.ResourceType)
                    {
                        markdown += $"**SKU**: {templateInfo.Sku}\n";
                    }

                    if (!string.IsNullOrEmpty(templateInfo.Template))
                    {
                        markdown += $"\n**Template Content**:\n```{language}\n{templateInfo.Template}\n```\n\n";
                        markdown += $"💡 **Next Steps**: Copy the template above to a `.{templateInfo.TemplateType}` file and customize the parameters as needed.";
                    }
                    
                    return markdown;
                }
                
                // Fallback: Simple formatting for non-template tools
                return $"✅ **Task Completed**\n\n**Result**:\n```json\n{serializedResult}\n```";
            }
            
            var prompt = $@"
You are a platform engineering assistant. Format the following tool execution result into a clear, user-friendly response.

Original Query: {originalQuery}
Tool Used: {toolName}
Raw Result:
{serializedResult}

Please format this into:
1. A clear summary of what was accomplished
2. Key findings or results
3. Any recommendations or next steps
4. Format technical details in a readable way (use markdown formatting)

Make it conversational and helpful, as if explaining to a colleague.";

            var result_formatted = await _chatCompletion.GetChatMessageContentAsync(prompt, cancellationToken: cancellationToken);
            return result_formatted.Content ?? "Result processed successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting response: {ToolName}", toolName);
            return $"Tool executed successfully. Raw result: {JsonSerializer.Serialize(result)}";
        }
    }

    private bool TryExtractTemplateInfo(object result, out TemplateInfo templateInfo)
    {
        templateInfo = new TemplateInfo();
        
        try
        {
            // Try to extract template information using reflection first
            var resultType = result.GetType();
            var templateTypeProperty = resultType.GetProperty("templateType");
            var templateProperty = resultType.GetProperty("template");
            var fileNameProperty = resultType.GetProperty("fileName");
            var resourceTypeProperty = resultType.GetProperty("resourceType");
            var locationProperty = resultType.GetProperty("location");
            var skuProperty = resultType.GetProperty("sku");

            if (templateTypeProperty != null) templateInfo.TemplateType = templateTypeProperty.GetValue(result)?.ToString();
            if (templateProperty != null) templateInfo.Template = templateProperty.GetValue(result)?.ToString();
            if (fileNameProperty != null) templateInfo.FileName = fileNameProperty.GetValue(result)?.ToString();
            if (resourceTypeProperty != null) templateInfo.ResourceType = resourceTypeProperty.GetValue(result)?.ToString();
            if (locationProperty != null) templateInfo.Location = locationProperty.GetValue(result)?.ToString();
            if (skuProperty != null) templateInfo.Sku = skuProperty.GetValue(result)?.ToString();

            // If reflection worked and we have template content, return true
            if (!string.IsNullOrEmpty(templateInfo.Template))
            {
                return true;
            }

            // Fallback: Try JSON parsing
            var jsonString = JsonSerializer.Serialize(result);
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);

            if (jsonElement.ValueKind == JsonValueKind.Object)
            {
                if (jsonElement.TryGetProperty("templateType", out var templateTypeProp))
                    templateInfo.TemplateType = templateTypeProp.GetString();
                if (jsonElement.TryGetProperty("template", out var templateProp))
                    templateInfo.Template = templateProp.GetString();
                if (jsonElement.TryGetProperty("fileName", out var fileNameProp))
                    templateInfo.FileName = fileNameProp.GetString();
                if (jsonElement.TryGetProperty("resourceType", out var resourceTypeProp))
                    templateInfo.ResourceType = resourceTypeProp.GetString();
                if (jsonElement.TryGetProperty("location", out var locationProp))
                    templateInfo.Location = locationProp.GetString();
                if (jsonElement.TryGetProperty("sku", out var skuProp))
                    templateInfo.Sku = skuProp.GetString();

                return !string.IsNullOrEmpty(templateInfo.Template);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract template information from result");
        }

        return false;
    }

    private bool TryFormatCostResult(object result, out string markdown)
    {
        markdown = string.Empty;
        
        try
        {
            // Handle both string and object results
            JsonDocument document;
            JsonElement root;
            
            if (result is string jsonString)
            {
                // Result is already a JSON string
                document = JsonDocument.Parse(jsonString);
                root = document.RootElement;
            }
            else
            {
                // Result is an object, serialize it first
                var json = JsonSerializer.Serialize(result);
                document = JsonDocument.Parse(json);
                root = document.RootElement;
            }

            if (!root.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
            {
                // Handle failed cost results with a user-friendly error message
                var errorSb = new StringBuilder();
                errorSb.AppendLine("❌ **Azure Cost Analysis Failed**\n");
                
                if (root.TryGetProperty("error", out var errorProp))
                {
                    var error = errorProp.GetString();
                    errorSb.AppendLine($"**Error**: {error}\n");
                    
                    if (error?.Contains("No subscription ID provided") == true)
                    {
                        errorSb.AppendLine("💡 **Solution**: Please specify your Azure subscription ID in the query.");
                        errorSb.AppendLine("**Example**: \"Analyze costs for subscription 12345678-1234-1234-1234-123456789012\"");
                    }
                }
                else
                {
                    errorSb.AppendLine("**Error**: Cost analysis request failed");
                }
                
                markdown = errorSb.ToString();
                document.Dispose();
                return true;
            }

            var sb = new StringBuilder();
            sb.AppendLine("💰 **Azure Cost Analysis Results**\n");

            // Total cost and subscription info
            if (root.TryGetProperty("totalCost", out var totalCostProp) && 
                root.TryGetProperty("currency", out var currencyProp))
            {
                var totalCost = totalCostProp.GetDecimal();
                var currency = currencyProp.GetString();
                sb.AppendLine($"**Total Cost**: {currency} ${totalCost:N0}");
            }

            if (root.TryGetProperty("subscriptionId", out var subIdProp))
            {
                var subId = subIdProp.GetString();
                if (!string.IsNullOrEmpty(subId) && subId != "Not specified")
                {
                    sb.AppendLine($"**Subscription**: `{subId}`");
                }
            }

            if (root.TryGetProperty("timeRange", out var timeRangeProp))
            {
                var timeRange = timeRangeProp.GetString();
                sb.AppendLine($"**Time Range**: {timeRange}");
            }

            if (root.TryGetProperty("totalResources", out var totalResourcesProp))
            {
                var totalResources = totalResourcesProp.GetInt32();
                sb.AppendLine($"**Total Resources**: {totalResources}\n");
            }

            // Cost breakdown by service
            if (root.TryGetProperty("breakdown", out var breakdownProp) &&
                breakdownProp.TryGetProperty("byService", out var byServiceProp))
            {
                sb.AppendLine("## 📊 **Cost by Service**");
                foreach (var service in byServiceProp.EnumerateArray())
                {
                    if (service.TryGetProperty("service", out var serviceProp) &&
                        service.TryGetProperty("cost", out var costProp) &&
                        service.TryGetProperty("resourceCount", out var countProp))
                    {
                        var serviceName = serviceProp.GetString()?.Split('/').LastOrDefault() ?? "Unknown";
                        var cost = costProp.GetDecimal();
                        var count = countProp.GetInt32();
                        sb.AppendLine($"- **{serviceName}**: ${cost} ({count} resources)");
                    }
                }
                sb.AppendLine();
            }

            // Cost breakdown by resource group
            if (root.TryGetProperty("breakdown", out var breakdown2Prop) &&
                breakdown2Prop.TryGetProperty("byResourceGroup", out var byRgProp))
            {
                sb.AppendLine("## 🏗️ **Cost by Resource Group**");
                foreach (var rg in byRgProp.EnumerateArray())
                {
                    if (rg.TryGetProperty("resourceGroup", out var rgProp) &&
                        rg.TryGetProperty("cost", out var costProp) &&
                        rg.TryGetProperty("resourceCount", out var countProp))
                    {
                        var rgName = rgProp.GetString();
                        var cost = costProp.GetDecimal();
                        var count = countProp.GetInt32();
                        sb.AppendLine($"- **{rgName}**: ${cost} ({count} resources)");
                    }
                }
                sb.AppendLine();
            }

            // Add optimization recommendations
            sb.AppendLine("## 💡 **Optimization Recommendations**");
            sb.AppendLine("- Review high-cost services like Cognitive Services and Machine Learning");
            sb.AppendLine("- Consider rightsizing storage accounts based on actual usage");
            sb.AppendLine("- Evaluate if all resources in resource groups are actively used");
            sb.AppendLine("- Set up cost alerts to monitor spending trends");

            markdown = sb.ToString();
            document.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to format cost result");
            return false;
        }
    }

    public async Task<TemplateIntent> AnalyzeTemplateIntentAsync(string userRequest, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_chatCompletion == null)
            {
                // Fallback to rule-based analysis when no AI service is available
                return AnalyzeTemplateIntentRuleBased(userRequest);
            }

            var prompt = $@"
You are a platform engineering template analysis expert. Analyze the following user request and extract structured template requirements.

User Request: {userRequest}

Please analyze and provide a JSON response with the following structure:
{{
  ""serviceType"": ""webapp|function-app|storage-account|key-vault|container-app|aks-cluster|sql-database|cosmos-db|microservice|etc"",
  ""securityRequirements"": [""authentication"", ""encryption"", ""zero-trust""],
  ""infrastructureComponents"": [""core-service"", ""security"", ""networking"", ""monitoring""],
  ""monitoringRequirements"": [""azure-monitor"", ""application-insights""],
  ""scalingRequirements"": {{
    ""autoScaling"": true,
    ""minReplicas"": 1,
    ""maxReplicas"": 10,
    ""targetCpuUtilization"": 70
  }},
  ""databaseRequirements"": [""sql-database"", ""cosmos-db""],
  ""networkingRequirements"": {{
    ""publicAccess"": true,
    ""privateNetwork"": false,
    ""loadBalancer"": true,
    ""customDomain"": false
  }},
  ""deploymentTier"": ""development|staging|production|enterprise""
}}

Focus on extracting the most appropriate service type and requirements based on the user's description.";

            var result = await _chatCompletion.GetChatMessageContentAsync(prompt, cancellationToken: cancellationToken);
            
            try
            {
                var intentJson = result.Content ?? "{}";
                var jsonDoc = JsonDocument.Parse(intentJson);
                var root = jsonDoc.RootElement;

                var intent = new TemplateIntent
                {
                    RawRequest = userRequest,
                    ServiceType = root.TryGetProperty("serviceType", out var serviceTypeProp) ? serviceTypeProp.GetString() ?? "microservice" : "microservice",
                    SecurityRequirements = ExtractArrayFromJson(root, "securityRequirements"),
                    InfrastructureComponents = ExtractArrayFromJson(root, "infrastructureComponents"),
                    MonitoringRequirements = ExtractArrayFromJson(root, "monitoringRequirements"),
                    DatabaseRequirements = ExtractArrayFromJson(root, "databaseRequirements"),
                    ScalingRequirements = ExtractScalingConfigFromJson(root),
                    NetworkingRequirements = ExtractNetworkConfigFromJson(root),
                    DeploymentTier = root.TryGetProperty("deploymentTier", out var tierProp) ? tierProp.GetString() ?? "standard" : "standard"
                };

                jsonDoc.Dispose();
                return intent;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse AI template analysis result, falling back to rule-based analysis");
                return AnalyzeTemplateIntentRuleBased(userRequest);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing template intent with AI: {UserRequest}", userRequest);
            return AnalyzeTemplateIntentRuleBased(userRequest);
        }
    }

    private TemplateIntent AnalyzeTemplateIntentRuleBased(string userRequest)
    {
        _logger.LogInformation("Analyzing template intent using rule-based approach: {UserRequest}", userRequest);
        
        var intent = new TemplateIntent
        {
            RawRequest = userRequest,
            ServiceType = ExtractServiceTypeRuleBased(userRequest),
            SecurityRequirements = ExtractSecurityRequirementsRuleBased(userRequest),
            InfrastructureComponents = ExtractInfrastructureComponentsRuleBased(userRequest),
            MonitoringRequirements = ExtractMonitoringRequirementsRuleBased(userRequest),
            ScalingRequirements = ExtractScalingRequirementsRuleBased(userRequest),
            DatabaseRequirements = ExtractDatabaseRequirementsRuleBased(userRequest),
            NetworkingRequirements = ExtractNetworkingRequirementsRuleBased(userRequest),
            DeploymentTier = ExtractDeploymentTierRuleBased(userRequest)
        };

        _logger.LogInformation("Extracted template intent: ServiceType={ServiceType}, Security={Security}, Components={Components}", 
            intent.ServiceType, string.Join(",", intent.SecurityRequirements), string.Join(",", intent.InfrastructureComponents));

        return intent;
    }

    private string ExtractServiceTypeRuleBased(string request)
    {
        var patterns = new Dictionary<string, string[]>
        {
            ["api"] = new[] { "api", "rest api", "web api", "service api", "microservice api" },
            ["webapp"] = new[] { "web app", "website", "web application", "frontend app" },
            ["function-app"] = new[] { "function app", "azure function", "serverless function", "lambda function", "data processing" },
            ["app-service"] = new[] { "app service", "azure app service", "web service", "hosted web app", "web application" },
            ["container-app"] = new[] { "container app", "azure container app", "containerized app", "auto-scales" },
            ["storage-account"] = new[] { "storage account", "blob storage", "azure storage", "s3 bucket", "object storage" },
            ["key-vault"] = new[] { "key vault", "azure key vault", "aws secrets manager", "secret store", "secrets vault" },
            ["aks-cluster"] = new[] { "aks cluster", "kubernetes cluster", "k8s cluster", "aks", "eks cluster" },
            ["sql-database"] = new[] { "sql database", "azure sql", "sql server", "rds", "managed database" },
            ["cosmos-db"] = new[] { "cosmos db", "azure cosmos", "nosql database", "document database" },
            ["service-bus"] = new[] { "service bus", "azure service bus", "message bus", "pub/sub" },
            ["event-hub"] = new[] { "event hub", "azure event hub", "kafka", "event streaming" },
            ["logic-app"] = new[] { "logic app", "azure logic app", "workflow", "automation" },
            ["microservice"] = new[] { "microservice", "micro service", "service" },
            ["worker"] = new[] { "worker", "background service", "job processor", "queue worker" },
            ["database"] = new[] { "database", "db service", "data service" },
            ["cache"] = new[] { "cache", "redis", "memcached", "caching service" },
            ["queue"] = new[] { "queue", "message queue", "event bus", "messaging service" },
            ["gateway"] = new[] { "gateway", "api gateway", "proxy service", "load balancer" },
            ["function"] = new[] { "lambda function for event", "aws lambda", "serverless", "lambda" }
        };

        var requestLower = request.ToLowerInvariant();
        
        foreach (var (serviceType, keywords) in patterns)
        {
            if (keywords.Any(keyword => requestLower.Contains(keyword)))
            {
                return serviceType;
            }
        }

        return "microservice"; // Default
    }

    private List<string> ExtractSecurityRequirementsRuleBased(string request)
    {
        var requirements = new List<string>();
        var requestLower = request.ToLowerInvariant();

        var securityPatterns = new Dictionary<string, string[]>
        {
            ["zero-trust"] = new[] { "zero trust", "zero-trust", "secure", "security hardened", "high security" },
            ["authentication"] = new[] { "auth", "authentication", "login", "identity" },
            ["authorization"] = new[] { "authorization", "rbac", "role-based", "permissions" },
            ["encryption"] = new[] { "encryption", "encrypted", "tls", "ssl", "https" },
            ["network-policies"] = new[] { "network policy", "network isolation", "firewall rules" },
            ["pod-security"] = new[] { "pod security", "security context", "security policy" },
            ["secrets-management"] = new[] { "secrets", "key vault", "secret management" },
            ["vulnerability-scanning"] = new[] { "vulnerability", "security scan", "cve scan" }
        };

        foreach (var (requirement, keywords) in securityPatterns)
        {
            if (keywords.Any(keyword => requestLower.Contains(keyword)))
            {
                requirements.Add(requirement);
            }
        }

        return requirements.Any() ? requirements : new List<string> { "standard" };
    }

    private List<string> ExtractInfrastructureComponentsRuleBased(string request)
    {
        var components = new List<string> { "core-service" }; // Always include core service
        var requestLower = request.ToLowerInvariant();

        var componentPatterns = new Dictionary<string, string[]>
        {
            ["security"] = new[] { "security", "auth", "authentication", "authorization", "secure" },
            ["networking"] = new[] { "network", "load balancer", "ingress", "proxy", "dns" },
            ["monitoring"] = new[] { "monitor", "observability", "logging", "metrics", "alerts" },
            ["scaling"] = new[] { "scale", "autoscale", "hpa", "replica", "elastic" },
            ["storage"] = new[] { "storage", "persistent", "volume", "disk", "database" },
            ["infrastructure"] = new[] { "infrastructure", "iac", "bicep", "terraform", "arm" }
        };

        foreach (var (component, keywords) in componentPatterns)
        {
            if (keywords.Any(keyword => requestLower.Contains(keyword)))
            {
                components.Add(component);
            }
        }

        return components.Distinct().ToList();
    }

    private List<string> ExtractMonitoringRequirementsRuleBased(string request)
    {
        var requirements = new List<string>();
        var requestLower = request.ToLowerInvariant();

        var monitoringPatterns = new Dictionary<string, string[]>
        {
            ["azure-monitor"] = new[] { "azure monitor", "monitor", "observability" },
            ["application-insights"] = new[] { "application insights", "app insights", "telemetry" },
            ["prometheus"] = new[] { "prometheus", "prom", "metrics" },
            ["grafana"] = new[] { "grafana", "dashboard", "visualization" },
            ["elk-stack"] = new[] { "elk", "elasticsearch", "logstash", "kibana" }
        };

        foreach (var (requirement, keywords) in monitoringPatterns)
        {
            if (keywords.Any(keyword => requestLower.Contains(keyword)))
            {
                requirements.Add(requirement);
            }
        }

        return requirements;
    }

    private ScalingConfig ExtractScalingRequirementsRuleBased(string request)
    {
        var config = new ScalingConfig();
        var requestLower = request.ToLowerInvariant();

        config.AutoScaling = new[] { "autoscale", "auto-scale", "elastic", "dynamic scaling", "hpa" }
            .Any(keyword => requestLower.Contains(keyword));

        if (config.AutoScaling)
        {
            config.MinReplicas = ExtractNumberFromPattern(request, @"min.*replicas?", 1);
            config.MaxReplicas = ExtractNumberFromPattern(request, @"max.*replicas?", 10);
            config.TargetCpuUtilization = ExtractNumberFromPattern(request, @"cpu.*(\d+)%?", 70);
        }

        return config;
    }

    private List<string> ExtractDatabaseRequirementsRuleBased(string request)
    {
        var requirements = new List<string>();
        var requestLower = request.ToLowerInvariant();

        var dbPatterns = new Dictionary<string, string[]>
        {
            ["sql-database"] = new[] { "sql", "mysql", "postgresql", "sql server", "azure sql" },
            ["cosmos-db"] = new[] { "cosmos", "nosql", "document database", "mongodb" },
            ["redis"] = new[] { "redis", "cache", "in-memory database" },
            ["sqlite"] = new[] { "sqlite", "embedded database" }
        };

        foreach (var (db, keywords) in dbPatterns)
        {
            if (keywords.Any(keyword => requestLower.Contains(keyword)))
            {
                requirements.Add(db);
            }
        }

        return requirements;
    }

    private NetworkConfig ExtractNetworkingRequirementsRuleBased(string request)
    {
        var config = new NetworkConfig();
        var requestLower = request.ToLowerInvariant();

        config.PublicAccess = new[] { "public", "internet-facing", "external access", "ingress" }
            .Any(keyword => requestLower.Contains(keyword));

        config.PrivateNetwork = new[] { "private", "internal", "vpc", "private network" }
            .Any(keyword => requestLower.Contains(keyword));

        config.LoadBalancer = new[] { "load balancer", "load balancing", "lb", "nginx" }
            .Any(keyword => requestLower.Contains(keyword));

        config.CustomDomain = new[] { "custom domain", "domain name", "dns", "certificate" }
            .Any(keyword => requestLower.Contains(keyword));

        return config;
    }

    private string ExtractDeploymentTierRuleBased(string request)
    {
        var requestLower = request.ToLowerInvariant();

        var tierPatterns = new Dictionary<string, string[]>
        {
            ["development"] = new[] { "dev", "development", "testing", "sandbox" },
            ["staging"] = new[] { "staging", "pre-prod", "uat", "acceptance" },
            ["production"] = new[] { "prod", "production", "live", "critical" },
            ["enterprise"] = new[] { "enterprise", "high availability", "mission critical" }
        };

        foreach (var (tier, keywords) in tierPatterns)
        {
            if (keywords.Any(keyword => requestLower.Contains(keyword)))
            {
                return tier;
            }
        }

        return "standard";
    }

    private List<string> ExtractArrayFromJson(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var arrayProp) && arrayProp.ValueKind == JsonValueKind.Array)
        {
            return arrayProp.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
        }
        return new List<string>();
    }

    private ScalingConfig ExtractScalingConfigFromJson(JsonElement root)
    {
        if (root.TryGetProperty("scalingRequirements", out var scalingProp) && scalingProp.ValueKind == JsonValueKind.Object)
        {
            return new ScalingConfig
            {
                AutoScaling = scalingProp.TryGetProperty("autoScaling", out var autoProp) && autoProp.GetBoolean(),
                MinReplicas = scalingProp.TryGetProperty("minReplicas", out var minProp) ? minProp.GetInt32() : 1,
                MaxReplicas = scalingProp.TryGetProperty("maxReplicas", out var maxProp) ? maxProp.GetInt32() : 10,
                TargetCpuUtilization = scalingProp.TryGetProperty("targetCpuUtilization", out var cpuProp) ? cpuProp.GetInt32() : 70
            };
        }
        return new ScalingConfig();
    }

    private NetworkConfig ExtractNetworkConfigFromJson(JsonElement root)
    {
        if (root.TryGetProperty("networkingRequirements", out var networkProp) && networkProp.ValueKind == JsonValueKind.Object)
        {
            return new NetworkConfig
            {
                PublicAccess = networkProp.TryGetProperty("publicAccess", out var publicProp) && publicProp.GetBoolean(),
                PrivateNetwork = networkProp.TryGetProperty("privateNetwork", out var privateProp) && privateProp.GetBoolean(),
                LoadBalancer = networkProp.TryGetProperty("loadBalancer", out var lbProp) && lbProp.GetBoolean(),
                CustomDomain = networkProp.TryGetProperty("customDomain", out var domainProp) && domainProp.GetBoolean()
            };
        }
        return new NetworkConfig();
    }

    private int ExtractNumberFromPattern(string text, string pattern, int defaultValue)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, pattern + @".*?(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : defaultValue;
    }

    private class TemplateInfo
    {
        public string? TemplateType { get; set; }
        public string? Template { get; set; }
        public string? FileName { get; set; }
        public string? ResourceType { get; set; }
        public string? Location { get; set; }
        public string? Sku { get; set; }
    }
}