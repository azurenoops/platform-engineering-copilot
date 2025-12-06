using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Mcp.Tools;
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
        // Process chat request through multi-agent orchestrator
        app.MapPost("/mcp/chat", async (HttpContext context, PlatformEngineeringCopilotTools chatTool) =>
        {
            try
            {
                var requestBody = await JsonSerializer.DeserializeAsync<ChatRequest>(
                    context.Request.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (requestBody == null || string.IsNullOrEmpty(requestBody.Message))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { error = "Message is required" });
                    return;
                }

                var logger = context.RequestServices.GetRequiredService<ILogger<McpHttpBridge>>();
                logger.LogInformation("HTTP: Processing chat request for conversation: {ConversationId}", 
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
                    var result = await chatTool.ProcessRequestAsync(
                        requestBody.Message,
                        requestBody.ConversationId,
                        requestBody.Context,
                        context.RequestAborted);

                    await context.Response.WriteAsJsonAsync(result);
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
                context.RequestServices.GetRequiredService<ILogger<McpHttpBridge>>()
                    .LogError(ex, "Error processing chat request");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        });

        // Health check
        app.MapGet("/health", () => Results.Ok(new 
        { 
            status = "healthy", 
            mode = "dual (http+stdio)",
            server = "Platform Engineering Copilot MCP",
            version = "1.0.0"
        }));

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
                    templateCount = await dbContext.EnvironmentTemplates.CountAsync();
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
