using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Data.Context;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Mcp.Server;

/// <summary>
/// HTTP bridge for MCP server - exposes MCP tools via REST endpoints
/// Provides a generic HTTP interface that any web application can use
/// 
/// Dual-mode operation:
/// 1. Stdio mode: GitHub Copilot/Claude spawn this as subprocess, use stdin/stdout
/// 2. HTTP mode: Any web app calls HTTP endpoints
/// </summary>
public class McpHttpBridge
{
    private readonly ILogger<McpHttpBridge> _logger;

    public McpHttpBridge(ILogger<McpHttpBridge> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Configure HTTP endpoints for MCP tools using WebApplication minimal APIs
    /// </summary>
    public void MapHttpEndpoints(WebApplication app)
    {
        // Debug test endpoint
        app.MapGet("/test", () => Results.Ok(new { message = "Test endpoint working", timestamp = DateTime.UtcNow }));

        // Process chat request through multi-agent orchestrator via McpServer
        app.MapPost("/mcp", async (HttpContext context, McpServer mcpServer) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<McpHttpBridge>>();
            
            try
            {
                logger.LogInformation("📨 Starting to deserialize chat request");
                var requestBody = await JsonSerializer.DeserializeAsync<ChatRequest>(
                    context.Request.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (requestBody == null || string.IsNullOrEmpty(requestBody.Message))
                {
                    logger.LogWarning("⚠️ Invalid request: message is null or empty");
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { error = "Message is required" });
                    return;
                }

                logger.LogInformation("📨 Deserialized chat request | Message: {Message} | ConvId: {ConvId}", 
                    requestBody.Message.Substring(0, Math.Min(50, requestBody.Message.Length)), 
                    requestBody.ConversationId ?? "new");

                // Process file attachments if present
                List<string>? uploadedFilePaths = null;
                if (requestBody.Attachments != null && requestBody.Attachments.Any())
                {
                    logger.LogInformation("Processing {Count} file attachments", requestBody.Attachments.Count);
                    uploadedFilePaths = await ProcessFileAttachmentsAsync(requestBody.Attachments, logger);
                    
                    // Add file paths to context
                    requestBody.Context ??= new Dictionary<string, object>();
                    requestBody.Context["uploaded_files"] = uploadedFilePaths;
                    
                    // Enhance message with file information
                    var fileInfo = string.Join(", ", uploadedFilePaths.Select(Path.GetFileName));
                    requestBody.Message = $"[Uploaded files: {fileInfo}]\n\n{requestBody.Message}";
                }

                try
                {
                    logger.LogInformation("🔄 Processing message through McpServer orchestrator");
                    var result = await mcpServer.ProcessChatRequestAsync(
                        requestBody.Message,
                        requestBody.ConversationId,
                        requestBody.Context,
                        context.RequestAborted);

                    logger.LogInformation("✅ Got result from orchestrator | Success: {Success}", result.Success);
                    await context.Response.WriteAsJsonAsync(result);
                    logger.LogInformation("✅ Successfully wrote response to HTTP client");
                }
                finally
                {
                    // Clean up uploaded files
                    if (uploadedFilePaths != null)
                    {
                        foreach (var filePath in uploadedFilePaths)
                        {
                            try
                            {
                                if (File.Exists(filePath))
                                {
                                    File.Delete(filePath);
                                    logger.LogDebug("Cleaned up temporary file: {FilePath}", filePath);
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Failed to delete temporary file: {FilePath}", filePath);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ EXCEPTION in /mcp/chat handler | Type: {ExType} | Message: {Message}", ex.GetType().Name, ex.Message);
                
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "application/json";
                    
                    try
                    {
                        logger.LogInformation("Writing error response to client");
                        await context.Response.WriteAsJsonAsync(new { error = ex.Message, type = ex.GetType().Name });
                        logger.LogInformation("✅ Successfully wrote error response");
                    }
                    catch (Exception writeEx)
                    {
                        logger.LogError(writeEx, "❌ Failed to write JSON error response");
                        try
                        {
                            await context.Response.WriteAsync($"{{\"error\":\"Internal Server Error\",\"details\":\"{writeEx.Message}\"}}");
                        }
                        catch (Exception plainEx)
                        {
                            logger.LogError(plainEx, "❌ Failed to write plain text error response");
                        }
                    }
                }
                else
                {
                    logger.LogError("⚠️ Response already started, cannot write error response");
                }
            }
        });

        // Alias for /mcp endpoint (Chat app uses /mcp/chat)
        app.MapPost("/mcp/chat", async (HttpContext context, McpServer mcpServer) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<McpHttpBridge>>();
            
            try
            {
                logger.LogInformation("📨 Starting to deserialize chat request (via /mcp/chat alias)");
                var requestBody = await JsonSerializer.DeserializeAsync<ChatRequest>(
                    context.Request.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (requestBody == null || string.IsNullOrEmpty(requestBody.Message))
                {
                    logger.LogWarning("⚠️ Invalid request: message is null or empty");
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { error = "Message is required" });
                    return;
                }

                logger.LogInformation("📨 Processing chat request | Message: {Message} | ConvId: {ConvId}", 
                    requestBody.Message.Substring(0, Math.Min(50, requestBody.Message.Length)), 
                    requestBody.ConversationId ?? "new");

                var result = await mcpServer.ProcessChatRequestAsync(
                    requestBody.Message,
                    requestBody.ConversationId,
                    requestBody.Context,
                    context.RequestAborted);

                logger.LogInformation("✅ Got result from orchestrator | Success: {Success}", result.Success);
                await context.Response.WriteAsJsonAsync(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ EXCEPTION in /mcp/chat handler | Type: {ExType} | Message: {Message}", ex.GetType().Name, ex.Message);
                
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsJsonAsync(new { error = ex.Message, type = ex.GetType().Name });
                }
            }
        });

        // Health check
        app.MapGet("/health", () => Results.Ok(new 
        { 
            status = "healthy", 
            mode = "dual (http+stdio)",
            server = "Platform Engineering Copilot MCP",
            version = "0.9.0"
        }));

        // List available MCP tools
        app.MapGet("/mcp/tools", (HttpContext context) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<McpHttpBridge>>();
            logger.LogInformation("📋 Listing available MCP tools");

            var tools = new[]
            {
                // Compliance Tools
                new { name = "run_compliance_assessment", category = "Compliance", description = "Run NIST 800-53/FedRAMP/DoD IL compliance assessment against Azure subscription" },
                new { name = "get_control_family_details", category = "Compliance", description = "Get detailed findings and recommendations for a specific NIST control family" },
                new { name = "generate_compliance_document", category = "Compliance", description = "Generate SSP, SAR, or POA&M compliance documents" },
                new { name = "collect_evidence", category = "Compliance", description = "Collect evidence artifacts for compliance controls" },
                new { name = "execute_remediation", category = "Compliance", description = "Execute remediation for a single compliance finding (requires finding_id)" },
                new { name = "batch_remediation", category = "Compliance", description = "Execute batch remediation for multiple findings by severity (e.g., 'start remediation for high-priority issues')" },
                new { name = "validate_remediation", category = "Compliance", description = "Validate that a remediation was successfully applied" },
                new { name = "generate_remediation_plan", category = "Compliance", description = "Generate prioritized remediation plan for findings" },
                new { name = "get_assessment_audit_log", category = "Compliance", description = "Get audit trail of compliance assessments" },
                new { name = "get_compliance_history", category = "Compliance", description = "Get compliance history and trends over time" },
                new { name = "get_compliance_status", category = "Compliance", description = "Get current compliance status summary" },
                new { name = "get_defender_findings", category = "Compliance", description = "Get Microsoft Defender for Cloud findings, secure score, and security recommendations mapped to NIST controls" },
                
                // Discovery Tools
                new { name = "discover_azure_resources", category = "Discovery", description = "Discover Azure resources across subscriptions using Resource Graph" },
                new { name = "get_resource_details", category = "Discovery", description = "Get detailed information about a specific Azure resource" },
                new { name = "list_subscriptions", category = "Discovery", description = "List available Azure subscriptions" },
                new { name = "get_resource_health", category = "Discovery", description = "Get health status of Azure resources" },
                new { name = "map_resource_dependencies", category = "Discovery", description = "Map dependencies between Azure resources" },
                new { name = "search_resources_by_tag", category = "Discovery", description = "Search for Azure resources by tag key or key-value pairs" },
                new { name = "list_resource_groups", category = "Discovery", description = "List all resource groups with resource counts, locations, and tags" },
                new { name = "get_resource_group_summary", category = "Discovery", description = "Get comprehensive summary and analysis of a specific resource group with resource breakdown, tag analysis, and optimization insights" },
                
                // Infrastructure Tools
                new { name = "generate_infrastructure_template", category = "Infrastructure", description = "Generate Bicep/Terraform/ARM templates for Azure resources" },
                new { name = "get_template_files", category = "Infrastructure", description = "Retrieve generated infrastructure templates and their files. Use when reviewing or showing template details" },
                new { name = "provision_infrastructure", category = "Infrastructure", description = "Provision Azure resources using generated templates" },
                new { name = "analyze_scaling", category = "Infrastructure", description = "Analyze resource scaling requirements and recommendations" },
                new { name = "delete_resource_group", category = "Infrastructure", description = "Delete Azure resource groups with safety checks" },
                new { name = "generate_arc_onboarding_script", category = "Infrastructure", description = "Generate Azure Arc onboarding scripts for hybrid resources" },
                
                // Cost Management Tools
                new { name = "analyze_azure_costs", category = "CostManagement", description = "Analyze Azure costs by resource, service, or time period" },
                new { name = "forecast_costs", category = "CostManagement", description = "Get cost forecasts and budget recommendations" },
                new { name = "detect_cost_anomalies", category = "CostManagement", description = "Detect unusual spending patterns and anomalies" },
                new { name = "get_optimization_recommendations", category = "CostManagement", description = "Get cost optimization recommendations" },
                new { name = "manage_budgets", category = "CostManagement", description = "Create and manage Azure budgets" },
                new { name = "model_cost_scenario", category = "CostManagement", description = "Model what-if cost scenarios" },
                
                // Knowledge Base Tools
                new { name = "explain_nist_control", category = "KnowledgeBase", description = "Get detailed explanation of NIST 800-53 controls" },
                new { name = "search_nist_controls", category = "KnowledgeBase", description = "Search NIST controls by keyword or requirement" },
                new { name = "explain_stig", category = "KnowledgeBase", description = "Get STIG implementation guidance for Azure" },
                new { name = "search_stigs", category = "KnowledgeBase", description = "Search STIGs by keyword or platform" },
                new { name = "explain_rmf", category = "KnowledgeBase", description = "Explain RMF process steps and requirements" },
                new { name = "explain_impact_level", category = "KnowledgeBase", description = "Explain FedRAMP/DoD impact levels (Low/Moderate/High)" },
                new { name = "get_fedramp_template_guidance", category = "KnowledgeBase", description = "Get FedRAMP template guidance and requirements" },
                
                // Configuration Tools
                new { name = "configure_subscription", category = "Configuration", description = "Configure the active Azure subscription for operations" }
            };

            var grouped = tools.GroupBy(t => t.category).Select(g => new
            {
                category = g.Key,
                count = g.Count(),
                tools = g.Select(t => new { t.name, t.description })
            });

            return Results.Ok(new
            {
                totalTools = tools.Length,
                categories = grouped
            });
        });

        // Debug endpoint to check configuration
        app.MapGet("/mcp/debug/config", (HttpContext context) =>
        {
            try
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<McpHttpBridge>>();
                var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
                
                logger.LogInformation("🔍 Checking configuration for Azure OpenAI...");
                
                var endpoint = configuration.GetValue<string>("Gateway:AzureOpenAI:Endpoint");
                var deployment = configuration.GetValue<string>("Gateway:AzureOpenAI:DeploymentName");
                var apiKey = configuration.GetValue<string>("Gateway:AzureOpenAI:ApiKey");
                var useManagedId = configuration.GetValue<bool>("Gateway:AzureOpenAI:UseManagedIdentity");
                var chatDeployment = configuration.GetValue<string>("Gateway:AzureOpenAI:ChatDeploymentName");
                var embeddingDeployment = configuration.GetValue<string>("Gateway:AzureOpenAI:EmbeddingDeploymentName");
                
                var result = new
                {
                    azureOpenAI = new
                    {
                        endpoint = endpoint ?? "[NOT SET]",
                        deploymentName = deployment ?? "[NOT SET]",
                        chatDeploymentName = chatDeployment ?? "[NOT SET]",
                        embeddingDeploymentName = embeddingDeployment ?? "[NOT SET]",
                        apiKeyPresent = !string.IsNullOrEmpty(apiKey),
                        apiKeyLength = apiKey?.Length ?? 0,
                        useManagedIdentity = useManagedId
                    },
                    environmentVariables = new
                    {
                        gateway_AzureOpenAI_Endpoint = System.Environment.GetEnvironmentVariable("Gateway__AzureOpenAI__Endpoint") ?? "[NOT SET]",
                        gateway_AzureOpenAI_DeploymentName = System.Environment.GetEnvironmentVariable("Gateway__AzureOpenAI__DeploymentName") ?? "[NOT SET]",
                        gateway_AzureOpenAI_ApiKey_Present = !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("Gateway__AzureOpenAI__ApiKey")),
                        gateway_AzureOpenAI_ChatDeploymentName = System.Environment.GetEnvironmentVariable("Gateway__AzureOpenAI__ChatDeploymentName") ?? "[NOT SET]"
                    }
                };
                
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        // Get templates by conversation ID - for VS Code extension to retrieve generated templates
        app.MapGet("/mcp/templates/{conversationId}", async (HttpContext context, string conversationId) =>
        {
            try
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<McpHttpBridge>>();
                var templateStorage = context.RequestServices.GetRequiredService<ITemplateStorageService>();
                
                logger.LogInformation("📥 Fetching templates for conversation: {ConversationId}", conversationId);
                
                // Get templates by conversation ID (stored in metadata)
                var templates = await templateStorage.GetTemplatesByConversationIdAsync(conversationId);
                
                if (templates == null || !templates.Any())
                {
                    logger.LogWarning("No templates found for conversation: {ConversationId}", conversationId);
                    return Results.NotFound(new { error = "No templates found for this conversation" });
                }
                
                // Return templates with their files
                var result = templates.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    description = t.Description,
                    templateType = t.TemplateType,
                    createdAt = t.CreatedAt,
                    files = t.Files != null 
                        ? t.Files.Select(f => (object)new
                            {
                                fileName = f.FileName,
                                content = f.Content,
                                fileType = f.FileType
                            }).ToList()
                        : new List<object>()
                }).ToList();
                
                logger.LogInformation("✅ Found {Count} template(s) for conversation: {ConversationId}", 
                    result.Count, conversationId);
                
                return Results.Ok(new { success = true, templates = result });
            }
            catch (Exception ex)
            {
                context.RequestServices.GetRequiredService<ILogger<McpHttpBridge>>()
                    .LogError(ex, "Error fetching templates for conversation: {ConversationId}", conversationId);
                return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
            }
        });

        // Get latest template - for VS Code extension to retrieve most recently generated template
        app.MapGet("/mcp/templates/latest", async (HttpContext context) =>
        {
            try
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<McpHttpBridge>>();
                var templateStorage = context.RequestServices.GetRequiredService<ITemplateStorageService>();
                
                logger.LogInformation("📥 Fetching latest template");
                
                var template = await templateStorage.GetLatestTemplateAsync();
                
                if (template == null)
                {
                    logger.LogWarning("No templates found");
                    return Results.NotFound(new { error = "No templates found" });
                }
                
                var result = new
                {
                    id = template.Id,
                    name = template.Name,
                    description = template.Description,
                    templateType = template.TemplateType,
                    createdAt = template.CreatedAt,
                    files = template.Files != null 
                        ? template.Files.Select(f => (object)new
                            {
                                fileName = f.FileName,
                                content = f.Content,
                                fileType = f.FileType
                            }).ToList()
                        : new List<object>()
                };
                
                logger.LogInformation("✅ Found latest template: {TemplateName}", template.Name);
                
                return Results.Ok(new { success = true, template = result });
            }
            catch (Exception ex)
            {
                context.RequestServices.GetRequiredService<ILogger<McpHttpBridge>>()
                    .LogError(ex, "Error fetching latest template");
                return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
            }
        });

        _logger.LogInformation("✅ MCP HTTP Bridge endpoints configured");
        _logger.LogInformation("   POST   /mcp/chat - Process chat request");
        _logger.LogInformation("   GET    /health - Health check");
        _logger.LogInformation("   GET    /mcp/templates/{conversationId} - Get templates by conversation");
        _logger.LogInformation("   GET    /mcp/debug/db - Database debug info");

        // Debug endpoint to check database connectivity and table status
        app.MapGet("/mcp/debug/db", async (HttpContext context) =>
        {
            try
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<McpHttpBridge>>();
                var dbContext = context.RequestServices.GetRequiredService<PlatformEngineeringCopilotContext>();
                
                logger.LogInformation("🔍 Checking database connectivity and tables...");
                
                // Check connectivity
                bool canConnect = await dbContext.Database.CanConnectAsync();
                
                // Get table counts
                int templateCount = 0;
                int fileCount = 0;
                string errorMsg = "";
                
                try
                {
                    templateCount = await dbContext.InfrastructureTemplates.CountAsync();
                    fileCount = await dbContext.TemplateFiles.CountAsync();
                }
                catch (Exception ex)
                {
                    errorMsg = ex.Message;
                    logger.LogError(ex, "❌ Error querying tables");
                }
                
                var result = new
                {
                    canConnect,
                    provider = dbContext.Database.ProviderName,
                    templateCount,
                    fileCount,
                    error = errorMsg,
                    connectionString = MaskConnectionString(dbContext.Database.GetConnectionString())
                };
                
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        // Debug endpoint to test direct template insertion
        app.MapPost("/mcp/debug/test-save", async (HttpContext context) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<McpHttpBridge>>();
            var templateStorage = context.RequestServices.GetRequiredService<ITemplateStorageService>();
            
            try
            {
                logger.LogInformation("🧪 Testing direct template save...");
                
                var testTemplate = new
                {
                    Name = $"debug-test-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
                    Description = "Test template for debugging",
                    TemplateType = "storage",
                    Version = "1.0.0",
                    Format = "bicep",
                    Content = "// Test bicep content",
                    Files = new Dictionary<string, string>
                    {
                        ["main.bicep"] = "// Test bicep content\nparam location string = 'usgovvirginia'"
                    },
                    CreatedBy = "debug-endpoint",
                    AzureService = "storage",
                    Tags = new Dictionary<string, string>
                    {
                        ["conversationId"] = "debug-endpoint-test",
                        ["testRun"] = DateTime.UtcNow.ToString("o")
                    }
                };
                
                logger.LogInformation("📝 Calling StoreTemplateAsync...");
                var result = await templateStorage.StoreTemplateAsync($"debug-test-{DateTime.UtcNow:yyyyMMdd-HHmmss}", testTemplate);
                
                logger.LogInformation("✅ StoreTemplateAsync completed. Result ID: {Id}", result?.Id);
                
                return Results.Ok(new { success = true, templateId = result?.Id, templateName = result?.Name });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Error in test-save");
                return Results.Json(new { success = false, error = ex.Message, stackTrace = ex.StackTrace }, statusCode: 500);
            }
        });
        _logger.LogInformation("   GET    /mcp/templates/latest - Get latest generated template");
    }

    /// <summary>
    /// Process file attachments by saving base64-encoded content to temp directory
    /// </summary>
    private static async Task<List<string>> ProcessFileAttachmentsAsync(
        List<FileAttachment> attachments,
        ILogger logger)
    {
        var uploadedPaths = new List<string>();
        var tempDir = Path.Combine(Path.GetTempPath(), "mcp-attachments", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        foreach (var attachment in attachments)
        {
            try
            {
                // Validate file size (max 50MB)
                if (attachment.Size > 50 * 1024 * 1024)
                {
                    logger.LogWarning("Skipping attachment {FileName} - size {Size} bytes exceeds 50MB limit",
                        attachment.FileName, attachment.Size);
                    continue;
                }

                // Decode base64 content
                byte[] fileBytes;
                try
                {
                    fileBytes = Convert.FromBase64String(attachment.Base64Content);
                }
                catch (FormatException ex)
                {
                    logger.LogError(ex, "Failed to decode base64 content for {FileName}", attachment.FileName);
                    continue;
                }

                // Sanitize filename
                var sanitizedFileName = Path.GetFileName(attachment.FileName);
                var filePath = Path.Combine(tempDir, sanitizedFileName);

                // Write file to disk
                await File.WriteAllBytesAsync(filePath, fileBytes);

                uploadedPaths.Add(filePath);
                logger.LogInformation("Saved attachment: {FileName} ({Size} bytes) to {Path}",
                    sanitizedFileName, fileBytes.Length, filePath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing attachment: {FileName}", attachment.FileName);
            }
        }

        return uploadedPaths;
    }

    private static string MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "(not configured)";
        
        // Mask password in connection string for security
        var masked = System.Text.RegularExpressions.Regex.Replace(
            connectionString, 
            @"Password=[^;]+", 
            "Password=****",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return masked;
    }
}

// Generic request model
public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public Dictionary<string, object>? Context { get; set; }
    public List<FileAttachment>? Attachments { get; set; }
}

// File attachment model (base64-encoded)
public class FileAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string Base64Content { get; set; } = string.Empty;
    public long Size { get; set; }
}
