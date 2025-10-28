using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models.Onboarding;
using Platform.Engineering.Copilot.Core.Models.EnvironmentManagement;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Data.Context;
using Platform.Engineering.Copilot.Data.Entities;
using Platform.Engineering.Copilot.Core.Services.Notifications;
using Platform.Engineering.Copilot.Core.Models.Notifications;

namespace Platform.Engineering.Copilot.Core.Services.Onboarding;

/// <summary>
/// Service for managing Navy Flankspeed mission owner onboarding requests
/// Handles the complete lifecycle from draft creation through template-based provisioning
/// Uses infrastructure service templates and environment management for standardized deployments
/// </summary>
public class FlankspeedOnboardingService : IOnboardingService
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ILogger<FlankspeedOnboardingService> _logger;
    private readonly IEnvironmentManagementEngine _environmentEngine;
    private readonly ITemplateStorageService _templateStorage;
    private readonly IEmailService _emailService;
    private readonly ISlackService _slackService;
    private readonly IDynamicTemplateGenerator _templateGenerator;
    private readonly ITeamsNotificationService _teamsNotificationService;

    public FlankspeedOnboardingService(
        PlatformEngineeringCopilotContext context,
        ILogger<FlankspeedOnboardingService> logger,
        IEnvironmentManagementEngine environmentEngine,
        ITemplateStorageService templateStorage,
        IEmailService emailService,
        ISlackService slackService,
        IDynamicTemplateGenerator templateGenerator,
        ITeamsNotificationService teamsNotificationService)
    {
        _context = context;
        _logger = logger;
        _environmentEngine = environmentEngine;
        _templateStorage = templateStorage;
        _emailService = emailService;
        _slackService = slackService;
        _templateGenerator = templateGenerator;
        _teamsNotificationService = teamsNotificationService;
    }

    #region Request Management

    public async Task<string> CreateDraftRequestAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating new draft onboarding request");

        var request = new OnboardingRequest
        {
            Status = OnboardingStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };

        _context.OnboardingRequests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created draft onboarding request {RequestId}", request.Id);
        return request.Id;
    }

    public async Task<bool> UpdateDraftAsync(
        string requestId, 
        object updates, 
        CancellationToken cancellationToken = default)
    {
        var request = await _context.OnboardingRequests
            .FindAsync(new[] { requestId }, cancellationToken);

        if (request == null)
        {
            _logger.LogWarning("Onboarding request {RequestId} not found", requestId);
            return false;
        }

        if (request.Status != OnboardingStatus.Draft)
        {
            _logger.LogWarning("Cannot update request {RequestId} in status {Status}", 
                requestId, request.Status);
            return false;
        }

        // Update properties from the updates object
        // Handle both Dictionary<string, object> and regular objects
        if (updates is Dictionary<string, object> dictionary)
        {
            foreach (var kvp in dictionary)
            {
                // Use case-insensitive property lookup with BindingFlags
                var requestProp = typeof(OnboardingRequest).GetProperty(
                    kvp.Key, 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.Instance | 
                    System.Reflection.BindingFlags.IgnoreCase);
                    
                if (requestProp != null && requestProp.CanWrite)
                {
                    try
                    {
                        var value = kvp.Value;
                        
                        // Convert string to List<string> if needed
                        if (requestProp.PropertyType == typeof(List<string>) && value is string stringValue)
                        {
                            // Split by common delimiters: comma, semicolon, pipe, newline
                            value = stringValue
                                .Split(new[] { ',', ';', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim())
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();
                            _logger.LogDebug("Converted string to List<string> for property {PropertyName}: {Count} items", 
                                kvp.Key, ((List<string>)value).Count);
                        }
                        
                        requestProp.SetValue(request, value);
                        _logger.LogDebug("Set property {PropertyName} to value {Value}", kvp.Key, value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set property {PropertyName} to value {Value}", kvp.Key, kvp.Value);
                    }
                }
                else
                {
                    _logger.LogWarning("Property {PropertyName} not found or not writable on OnboardingRequest", kvp.Key);
                }
            }
        }
        else
        {
            // Legacy: Handle regular objects via reflection
            var properties = updates.GetType().GetProperties();
            foreach (var prop in properties)
            {
                var requestProp = typeof(OnboardingRequest).GetProperty(prop.Name);
                if (requestProp != null && requestProp.CanWrite)
                {
                    try
                    {
                        var value = prop.GetValue(updates);
                        
                        // Convert string to List<string> if needed
                        if (requestProp.PropertyType == typeof(List<string>) && value is string stringValue)
                        {
                            value = stringValue
                                .Split(new[] { ',', ';', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim())
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();
                            _logger.LogDebug("Converted string to List<string> for property {PropertyName}: {Count} items", 
                                prop.Name, ((List<string>)value).Count);
                        }
                        
                        requestProp.SetValue(request, value);
                        _logger.LogDebug("Set property {PropertyName} to value {Value}", prop.Name, value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set property {PropertyName}", prop.Name);
                    }
                }
            }
        }

        request.LastUpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated draft onboarding request {RequestId}", requestId);
        return true;
    }

    public async Task<List<string>> ValidateForSubmissionAsync(string requestId, CancellationToken cancellationToken = default)
    {
        var request = await _context.OnboardingRequests
            .FindAsync(new[] { requestId }, cancellationToken);

        if (request == null)
        {
            return new List<string> { $"Request {requestId} not found" };
        }

        var errors = new List<string>();
        
        if (string.IsNullOrWhiteSpace(request.MissionName))
            errors.Add("Mission name is required");

        if (string.IsNullOrWhiteSpace(request.MissionOwner))
            errors.Add("Mission owner name is required");

        if (string.IsNullOrWhiteSpace(request.MissionOwnerEmail))
            errors.Add("Mission owner email is required");

        if (string.IsNullOrWhiteSpace(request.Command))
            errors.Add("Command/Organization is required");

        if (string.IsNullOrWhiteSpace(request.RequestedSubscriptionName))
            errors.Add("Subscription name is required");

        if (string.IsNullOrWhiteSpace(request.RequestedVNetCidr))
            errors.Add("VNet CIDR is required");

        return errors;
    }

    public async Task<bool> SubmitRequestAsync(string requestId, string? submittedBy = null, CancellationToken cancellationToken = default)
    {
        var request = await _context.OnboardingRequests
            .FindAsync(new[] { requestId }, cancellationToken);

        if (request == null)
        {
            _logger.LogWarning("Onboarding request {RequestId} not found", requestId);
            return false;
        }

        if (request.Status != OnboardingStatus.Draft)
        {
            _logger.LogWarning("Request {RequestId} is already submitted (status: {Status})", 
                requestId, request.Status);
            return false;
        }

        // Validate required fields
        var validationErrors = await ValidateForSubmissionAsync(requestId, cancellationToken);
        if (validationErrors.Count > 0)
        {
            _logger.LogWarning("Request {RequestId} validation failed: {Errors}", 
                requestId, string.Join(", ", validationErrors));
            return false;
        }

        request.Status = OnboardingStatus.PendingReview;
        request.SubmittedForApprovalAt = DateTime.UtcNow;
        request.SubmittedBy = submittedBy ?? request.MissionOwnerEmail;
        request.LastUpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Submitted onboarding request {RequestId} for review by {SubmittedBy}", 
            requestId, request.SubmittedBy);
        
        // Send notification to NNWC platform team
        try
        {
            var details = $@"Mission Owner: {request.MissionOwner} ({request.MissionOwnerEmail})
Organization: {request.Command}
Environment Type: {string.Join(", ", request.RequiredServices ?? new List<string>())}
Classification: {request.ClassificationLevel}
Estimated Monthly Cost: ${request.EstimatedMonthlyCost:N2}
Submitted By: {request.SubmittedBy}
Submitted At: {request.SubmittedForApprovalAt:yyyy-MM-dd HH:mm:ss UTC}

Review this request in the Admin Console at: /admin/onboarding/{requestId}";

            await _emailService.SendNNWCTeamNotificationAsync(
                request.MissionName,
                requestId,
                "Pending Review",
                details,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send NNWC team notification for request {RequestId}", requestId);
            // Don't fail the submission if email fails
        }
        
        return true;
    }

    public async Task<OnboardingRequest?> GetRequestAsync(
        string requestId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.OnboardingRequests
            .FindAsync(new[] { requestId }, cancellationToken);
    }

    public async Task<List<OnboardingRequest>> GetPendingRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.OnboardingRequests
            .Where(r => r.Status == OnboardingStatus.PendingReview || 
                       r.Status == OnboardingStatus.UnderReview)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OnboardingRequest>> GetRequestsByOwnerAsync(
        string email, 
        CancellationToken cancellationToken = default)
    {
        return await _context.OnboardingRequests
            .Where(r => r.MissionOwnerEmail == email)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CancelRequestAsync(
        string requestId, 
        string reason, 
        CancellationToken cancellationToken = default)
    {
        var request = await _context.OnboardingRequests
            .FindAsync(new[] { requestId }, cancellationToken);

        if (request == null)
        {
            _logger.LogWarning("Onboarding request {RequestId} not found", requestId);
            return false;
        }

        if (request.IsTerminalState())
        {
            _logger.LogWarning("Cannot cancel request {RequestId} in terminal state {Status}", 
                requestId, request.Status);
            return false;
        }

        request.Status = OnboardingStatus.Cancelled;
        request.RejectionReason = reason;
        request.LastUpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cancelled onboarding request {RequestId}: {Reason}", requestId, reason);
        return true;
    }

    #endregion

    #region Approval Workflow

    public async Task<ProvisioningResult> ApproveRequestAsync(
        string requestId,
        string approvedBy,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.OnboardingRequests
            .FindAsync(new[] { requestId }, cancellationToken);

        if (request == null)
        {
            _logger.LogWarning("Onboarding request {RequestId} not found", requestId);
            return new ProvisioningResult
            {
                Success = false,
                Message = "Request not found"
            };
        }

        if (!request.CanBeApproved())
        {
            _logger.LogWarning("Request {RequestId} cannot be approved (status: {Status})", 
                requestId, request.Status);
            return new ProvisioningResult
            {
                Success = false,
                Message = $"Request cannot be approved in current status: {request.Status}"
            };
        }

        // Update approval information
        request.Status = OnboardingStatus.Approved;
        request.ApprovedBy = approvedBy;
        request.ApprovedAt = DateTime.UtcNow;
        request.ApprovalComments = comments;
        request.ReviewedAt = DateTime.UtcNow;
        request.LastUpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Approved onboarding request {RequestId} by {ApprovedBy}", 
            requestId, approvedBy);

        // Send approval notifications
        await SendApprovalNotificationsAsync(request, cancellationToken);

        // Start provisioning asynchronously
        var jobId = await StartProvisioningAsync(request, cancellationToken);

        return new ProvisioningResult
        {
            Success = true,
            JobId = jobId,
            Message = "Request approved. Provisioning started."
        };
    }

    public async Task<bool> RejectRequestAsync(
        string requestId,
        string rejectedBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.OnboardingRequests
            .FindAsync(new[] { requestId }, cancellationToken);

        if (request == null)
        {
            _logger.LogWarning("Onboarding request {RequestId} not found", requestId);
            return false;
        }

        if (!request.CanBeRejected())
        {
            _logger.LogWarning("Request {RequestId} cannot be rejected (status: {Status})", 
                requestId, request.Status);
            return false;
        }

        request.Status = OnboardingStatus.Rejected;
        request.RejectedBy = rejectedBy;
        request.RejectedAt = DateTime.UtcNow;
        request.RejectionReason = reason;
        request.ReviewedAt = DateTime.UtcNow;
        request.LastUpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Rejected onboarding request {RequestId} by {RejectedBy}: {Reason}", 
            requestId, rejectedBy, reason);

        // Send rejection notifications
        await SendRejectionNotificationsAsync(request, cancellationToken);

        return true;
    }

    #endregion

    #region Provisioning

    private async Task<string> StartProvisioningAsync(
        OnboardingRequest request,
        CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString();
        request.ProvisioningJobId = jobId;
        request.Status = OnboardingStatus.Provisioning;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Starting provisioning job {JobId} for request {RequestId}",
            jobId, request.Id);

        // Run provisioning in background
        _ = Task.Run(async () =>
        {
            try
            {
                await ProvisionResourcesAsync(request, jobId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Provisioning failed for job {JobId}", jobId);
                await HandleProvisioningFailureAsync(request, ex, cancellationToken);
            }
        }, cancellationToken);

        return jobId;
    }

    private async Task ProvisionResourcesAsync(
        OnboardingRequest request,
        string jobId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🚀 Starting auto-generation and provisioning for Flankspeed request {RequestId}", 
            request.Id);

        try
        {
            // Send initial approval notification
            await _teamsNotificationService.SendOnboardingApprovedNotificationAsync(
                request.MissionName,
                request.MissionOwner,
                request.Command,
                request.Id,
                cancellationToken);

            // ============================================================
            // STEP 1: AUTO-GENERATE INFRASTRUCTURE TEMPLATE
            // ============================================================
            _logger.LogInformation("📝 Step 1/4: Auto-generating infrastructure template from onboarding requirements");
            
            await _teamsNotificationService.SendTemplateGenerationStartedNotificationAsync(
                request.MissionName,
                request.Id,
                cancellationToken);
            
            var templateRequest = BuildTemplateRequestFromOnboarding(request);
            var generationResult = await _templateGenerator.GenerateTemplateAsync(templateRequest, cancellationToken);

            if (!generationResult.Success)
            {
                await _teamsNotificationService.SendDeploymentFailedNotificationAsync(
                    request.MissionName,
                    request.Id,
                    "Template Generation",
                    generationResult.ErrorMessage ?? "Unknown error",
                    cancellationToken);
                throw new Exception($"Template generation failed: {generationResult.ErrorMessage}");
            }

            _logger.LogInformation("✅ Generated {FileCount} infrastructure files", generationResult.Files.Count);
            _logger.LogInformation("   Components: {Components}", string.Join(", ", generationResult.GeneratedComponents));

            await _teamsNotificationService.SendTemplateGenerationCompletedNotificationAsync(
                request.MissionName,
                request.Id,
                generationResult.Files.Count,
                generationResult.Summary ?? "Template generation completed",
                cancellationToken);

            // ============================================================
            // STEP 2: AUDIT LOG GENERATED TEMPLATE (NO STORAGE FOR ONBOARDING)
            // ============================================================
            _logger.LogInformation("� Step 2/5: Logging template audit trail");

            var templateId = Guid.NewGuid();
            var templateName = $"{request.MissionName.ToLower().Replace(" ", "-")}-infrastructure";
            
            // Audit log: Template generated for onboarding (not saved to storage)
            var auditLog = new
            {
                RequestId = request.Id,
                MissionName = request.MissionName,
                Classification = request.ClassificationLevel,
                TemplateId = templateId,
                TemplateName = templateName,
                GeneratedAt = DateTime.UtcNow,
                FileCount = generationResult.Files.Count,
                Components = generationResult.GeneratedComponents,
                Summary = generationResult.Summary,
                Purpose = "Onboarding - Direct deployment without template storage"
            };
            
            _logger.LogInformation("📝 AUDIT: Onboarding template generated {@AuditLog}", auditLog);
            
            // Log template audit (Teams notification handled by existing methods)
            _logger.LogInformation("✅ Template audit logged - ready for direct deployment");

            // ============================================================
            // STEP 3: PRE-DEPLOYMENT VALIDATION
            // ============================================================
            _logger.LogInformation("🔍 Step 3/5: Validating deployment readiness");
            
            // Log validation start
            _logger.LogInformation("🔍 Validating {Mission} deployment prerequisites", request.MissionName);
            
            var validationErrors = new List<string>();
            
            // Validate Bicep template syntax
            var mainBicepFile = generationResult.Files.FirstOrDefault(f => f.Key.EndsWith(".bicep") || f.Key.Contains("main"));
            if (string.IsNullOrEmpty(mainBicepFile.Key) || string.IsNullOrWhiteSpace(mainBicepFile.Value))
            {
                validationErrors.Add("No valid Bicep entry point found");
            }
            
            // Validate required parameters
            if (string.IsNullOrWhiteSpace(request.Region))
            {
                validationErrors.Add("Azure region not specified");
            }
            
            if (string.IsNullOrWhiteSpace(request.RequestedVNetCidr))
            {
                validationErrors.Add("VNet CIDR range not specified");
            }
            
            // Validate subscription/resource group
            var tempResourceGroupName = $"{request.MissionName.ToLower().Replace(" ", "-")}-rg";
            if (tempResourceGroupName.Length > 90)
            {
                validationErrors.Add($"Resource group name too long (max 90 chars): {tempResourceGroupName}");
            }
            
            // Validate classification-specific requirements
            if (request.ClassificationLevel.Contains("SECRET", StringComparison.OrdinalIgnoreCase))
            {
                if (!request.ComplianceFrameworks.Contains("DoD IL5") && 
                    !request.ComplianceFrameworks.Contains("IL5"))
                {
                    validationErrors.Add("SECRET classification requires DoD IL5 compliance");
                }
                
                if (!request.Region.Contains("gov", StringComparison.OrdinalIgnoreCase))
                {
                    validationErrors.Add("SECRET classification requires Azure Government region");
                }
            }
            
            // Check for validation failures
            if (validationErrors.Any())
            {
                var errorMessage = string.Join("; ", validationErrors);
                _logger.LogError("❌ Pre-deployment validation failed: {Errors}", errorMessage);
                
                await _teamsNotificationService.SendDeploymentFailedNotificationAsync(
                    request.MissionName,
                    request.Id,
                    "Pre-Deployment Validation",
                    errorMessage,
                    cancellationToken);
                
                throw new InvalidOperationException($"Deployment validation failed: {errorMessage}");
            }
            
            _logger.LogInformation("✅ Pre-deployment validation passed");
            _logger.LogInformation("   Files: {Count}, Region: {Region}, Classification: {Classification}",
                generationResult.Files.Count, request.Region, request.ClassificationLevel);

            // ============================================================
            // STEP 4: DEPLOY INFRASTRUCTURE TO AZURE
            // ============================================================
            _logger.LogInformation("🔧 Step 4/5: Deploying infrastructure to Azure");

            var environmentName = $"{request.MissionName.ToLower().Replace(" ", "-")}-env";
            var resourceGroupName = $"{request.MissionName.ToLower().Replace(" ", "-")}-rg";
            var region = request.Region?.ToLower() ?? "usgovvirginia";

            await _teamsNotificationService.SendDeploymentStartedNotificationAsync(
                request.MissionName,
                request.Id,
                "Production",
                cancellationToken);
            
            // Enhanced log with deployment details
            _logger.LogInformation("🚀 Deploying {Mission} infrastructure to {Region}",
                request.MissionName, region);
            _logger.LogInformation("   Environment: {Env}, RG: {RG}", environmentName, resourceGroupName);
            _logger.LogInformation("   Files: {Count}, Components: {Components}",
                generationResult.Files.Count, string.Join(", ", generationResult.GeneratedComponents));
            _logger.LogInformation("   Services: {Services}",
                string.Join(", ", request.RequiredServices));

            // Build environment request from generated template (NO STORAGE LOOKUP)
            var environmentRequest = new EnvironmentCreationRequest
            {
                Name = environmentName,
                Type = EnvironmentType.Unknown,
                ResourceGroup = resourceGroupName,
                Location = region,
                SubscriptionId = request.ProvisionedSubscriptionId ?? request.RequestedSubscriptionName,
                
                // DIRECT DEPLOYMENT: Pass generated files directly (no template ID lookup)
                // Convert Dictionary<string, string> to main content + additional files
                TemplateContent = generationResult.Files.Values.FirstOrDefault(), // Main template
                TemplateFiles = generationResult.Files.Select(f => new ServiceTemplateFile
                {
                    FileName = f.Key,
                    Content = f.Value,
                    FileType = Path.GetExtension(f.Key).TrimStart('.'),
                    IsEntryPoint = f.Key.Contains("main", StringComparison.OrdinalIgnoreCase)
                }).ToList(),
                
                TemplateParameters = new Dictionary<string, string>
                {
                    { "vnetName", $"{request.MissionName.ToLower().Replace(" ", "-")}-vnet" },
                    { "vnetAddressSpace", request.RequestedVNetCidr ?? "10.0.0.0/16" },
                    { "missionName", request.MissionName },
                    { "missionOwner", request.MissionOwner },
                    { "command", request.Command },
                    { "classification", request.ClassificationLevel }
                },
                Tags = new Dictionary<string, string>
                {
                    { "MissionName", request.MissionName },
                    { "MissionOwner", request.MissionOwner },
                    { "Command", request.Command },
                    { "Classification", request.ClassificationLevel },
                    { "RequestId", request.Id },
                    { "JobId", jobId },
                    { "DeploymentType", "Onboarding-Direct" } // Mark as direct deployment
                }
            };

            _logger.LogInformation("🔧 Deploying infrastructure directly from generated template (no storage lookup)");
            _logger.LogInformation("   Main template size: {Size} bytes", 
                environmentRequest.TemplateContent?.Length ?? 0);
            _logger.LogInformation("   Additional files: {Count}", 
                environmentRequest.TemplateFiles?.Count ?? 0);

            var deploymentResult = await _environmentEngine.CreateEnvironmentAsync(
                environmentRequest, 
                cancellationToken);

            if (!deploymentResult.Success)
            {
                await _teamsNotificationService.SendDeploymentCompletedNotificationAsync(
                    request.MissionName,
                    request.Id,
                    "Production",
                    resourceGroupName,
                    request.ProvisionedSubscriptionId ?? request.RequestedSubscriptionName ?? "N/A",
                    success: false,
                    errorMessage: deploymentResult.ErrorMessage,
                    cancellationToken);
                throw new Exception($"Infrastructure deployment failed: {deploymentResult.ErrorMessage}");
            }

            _logger.LogInformation("✅ Infrastructure deployed successfully");
            _logger.LogInformation("   Environment: {EnvironmentName}", environmentName);
            _logger.LogInformation("   Resource Group: {ResourceGroup}", deploymentResult.ResourceGroup);
            _logger.LogInformation("   Resources Created: {Count}", deploymentResult.CreatedResources?.Count ?? 0);

            // Enhanced log with resource details
            if (deploymentResult.CreatedResources?.Any() == true)
            {
                _logger.LogInformation("📦 Created Resources:");
                foreach (var resource in deploymentResult.CreatedResources.Take(10)) // Log first 10
                {
                    _logger.LogInformation("   • {Resource}", resource);
                }
                if (deploymentResult.CreatedResources.Count > 10)
                {
                    _logger.LogInformation("   ... and {More} more", deploymentResult.CreatedResources.Count - 10);
                }
            }

            await _teamsNotificationService.SendDeploymentCompletedNotificationAsync(
                request.MissionName,
                request.Id,
                "Production",
                deploymentResult.ResourceGroup ?? resourceGroupName,
                request.ProvisionedSubscriptionId ?? request.RequestedSubscriptionName ?? "N/A",
                success: true,
                errorMessage: null,
                cancellationToken: cancellationToken);

            // ============================================================
            // STEP 5: UPDATE REQUEST WITH RESULTS & AUDIT
            // ============================================================
            _logger.LogInformation("📊 Step 5/5: Recording deployment results and audit trail");

            request.ProvisionedResourceGroupId = deploymentResult.ResourceGroup;
            request.ProvisionedResources["EnvironmentName"] = environmentName;
            request.ProvisionedResources["EnvironmentId"] = deploymentResult.EnvironmentId;
            request.ProvisionedResources["DeploymentId"] = deploymentResult.DeploymentId ?? "N/A";
            request.ProvisionedResources["TemplateAuditId"] = templateId.ToString(); // Audit reference only
            request.ProvisionedResources["TemplateNotStored"] = "true"; // Mark that template wasn't saved
            request.ProvisionedResources["DeploymentMethod"] = "Direct-From-Generation";
            request.ProvisionedResources["GeneratedFileCount"] = generationResult.Files.Count.ToString();

            // Store all created resources
            if (deploymentResult.CreatedResources != null && deploymentResult.CreatedResources.Any())
            {
                for (int i = 0; i < deploymentResult.CreatedResources.Count; i++)
                {
                    request.ProvisionedResources[$"Resource{i + 1}"] = deploymentResult.CreatedResources[i];
                }
            }

            // Mark provisioning as complete
            request.Status = OnboardingStatus.Completed;
            request.ProvisionedAt = DateTime.UtcNow;
            request.CompletedAt = DateTime.UtcNow;
            request.LastUpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            // Final audit log with complete deployment details
            var completionAudit = new
            {
                RequestId = request.Id,
                MissionName = request.MissionName,
                Status = "Completed",
                CompletedAt = DateTime.UtcNow,
                EnvironmentName = environmentName,
                EnvironmentId = deploymentResult.EnvironmentId,
                ResourceGroup = deploymentResult.ResourceGroup,
                ResourceCount = deploymentResult.CreatedResources?.Count ?? 0,
                Resources = deploymentResult.CreatedResources,
                TemplateAuditId = templateId,
                TemplateFileCount = generationResult.Files.Count,
                DeploymentMethod = "Direct-Onboarding-NoStorage",
                Region = region,
                Classification = request.ClassificationLevel,
                Duration = (DateTime.UtcNow - request.CreatedAt).TotalMinutes
            };
            
            _logger.LogInformation("📝 AUDIT: Onboarding deployment completed {@CompletionAudit}", completionAudit);

            _logger.LogInformation("🎉 Successfully provisioned environment for request {RequestId}", request.Id);
            _logger.LogInformation("   📝 Generated Files: {FileCount}", generationResult.Files.Count);
            _logger.LogInformation("   🔧 Deployed Resources: {ResourceCount}", deploymentResult.CreatedResources?.Count ?? 0);
            _logger.LogInformation("   ⏱️ Total Duration: {Duration:F1} minutes", completionAudit.Duration);
            _logger.LogInformation("   💾 Template Storage: Skipped (direct deployment for onboarding)");

            // Send enhanced provisioning complete notifications
            await SendProvisioningCompleteNotificationsAsync(request, cancellationToken);
            
            // Log final summary with detailed metrics
            _logger.LogInformation("📊 Deployment Metrics:");
            _logger.LogInformation("   📧 Mission Owner: {Owner} ({Email})",
                request.MissionOwner, request.MissionOwnerEmail);
            _logger.LogInformation("   🏢 Command: {Command}", request.Command);
            _logger.LogInformation("   🔒 Classification: {Classification}", request.ClassificationLevel);
            _logger.LogInformation("   🌍 Environment: {Env} in {RG}",
                environmentName, deploymentResult.ResourceGroup);
            _logger.LogInformation("   📦 Resources: {Count} deployed", deploymentResult.CreatedResources?.Count ?? 0);
            _logger.LogInformation("   ⏱️ Duration: {Duration:F1} minutes", completionAudit.Duration);
            _logger.LogInformation("   � Storage: Direct deployment (no template saved)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Auto-generation/provisioning failed for request {RequestId}: {Error}",
                request.Id, ex.Message);

            // Send Teams failure notification
            await _teamsNotificationService.SendDeploymentFailedNotificationAsync(
                request.MissionName,
                request.Id,
                "Infrastructure Provisioning",
                ex.Message,
                cancellationToken);

            // Update request status to Failed
            request.Status = OnboardingStatus.Failed;
            request.ProvisioningError = $"{ex.GetType().Name}: {ex.Message}";
            request.LastUpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            // Send failure notifications
            await SendProvisioningFailedNotificationsAsync(
                request, 
                "Auto-generation and deployment", 
                ex.ToString(), 
                false,
                cancellationToken);
            
            throw; // Re-throw to be handled by background job handler
        }
    }

    /// <summary>
    /// Determines template type from compute platform
    /// </summary>
    private string DetermineTemplateType(ComputePlatform platform)
    {
        return platform switch
        {
            ComputePlatform.AKS => "Kubernetes",
            ComputePlatform.AppService => "AppService",
            ComputePlatform.ContainerApps => "ContainerApps",
            ComputePlatform.Network => "NetworkFoundation",
            _ => "Infrastructure"
        };
    }

    /// <summary>
    /// Determines file type from file path
    /// Examples: main.bicep → Bicep, deployment.yaml → Kubernetes, main.tf → Terraform
    /// </summary>
    private string DetermineFileType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();

        return extension switch
        {
            ".bicep" => "Bicep",
            ".tf" => "Terraform",
            ".yaml" or ".yml" => fileName.Contains("kubernetes") || fileName.Contains("k8s") ? "Kubernetes" : "YAML",
            ".json" => fileName.Contains("arm") ? "ARM" : "JSON",
            ".ps1" => "PowerShell",
            ".sh" => "Shell",
            ".md" => "Markdown",
            _ => "Other"
        };
    }

    private async Task HandleProvisioningFailureAsync(
        OnboardingRequest request,
        Exception ex,
        CancellationToken cancellationToken)
    {
        request.Status = OnboardingStatus.Failed;
        request.ProvisioningError = ex.Message;
        request.LastUpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogError("Provisioning failed for request {RequestId}: {Error}",
            request.Id, ex.Message);

        // TODO: Send failure notification to NNWC team
    }

    public async Task<ProvisioningStatus> GetProvisioningStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.OnboardingRequests
            .FirstOrDefaultAsync(r => r.ProvisioningJobId == jobId, cancellationToken);

        if (request == null)
        {
            return new ProvisioningStatus
            {
                JobId = jobId,
                Status = "NotFound",
                ErrorMessage = "Job not found"
            };
        }

        var status = request.Status switch
        {
            OnboardingStatus.Provisioning => "InProgress",
            OnboardingStatus.Completed => "Completed",
            OnboardingStatus.Failed => "Failed",
            _ => "Unknown"
        };

        var percentComplete = request.Status switch
        {
            OnboardingStatus.Approved => 0,
            OnboardingStatus.Provisioning => 50,
            OnboardingStatus.Completed => 100,
            OnboardingStatus.Failed => 0,
            _ => 0
        };

        return new ProvisioningStatus
        {
            JobId = jobId,
            RequestId = request.Id,
            Status = status,
            PercentComplete = percentComplete,
            CurrentStep = request.Status.ToString(),
            ProvisionedResources = request.ProvisionedResources,
            ErrorMessage = request.ProvisioningError
        };
    }

    public async Task<List<OnboardingRequest>> GetProvisioningRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.OnboardingRequests
            .Where(r => r.Status == OnboardingStatus.Provisioning)
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region Statistics & Reporting

    public async Task<OnboardingStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var allRequests = await _context.OnboardingRequests.ToListAsync(cancellationToken);

        var stats = new OnboardingStats
        {
            TotalRequests = allRequests.Count,
            PendingReview = allRequests.Count(r => r.Status == OnboardingStatus.PendingReview || 
                                                   r.Status == OnboardingStatus.UnderReview),
            Approved = allRequests.Count(r => r.Status == OnboardingStatus.Approved),
            Rejected = allRequests.Count(r => r.Status == OnboardingStatus.Rejected),
            InProvisioning = allRequests.Count(r => r.Status == OnboardingStatus.Provisioning),
            Completed = allRequests.Count(r => r.Status == OnboardingStatus.Completed),
            Failed = allRequests.Count(r => r.Status == OnboardingStatus.Failed)
        };

        // Calculate average approval time
        var approvedRequests = allRequests
            .Where(r => r.ReviewedAt.HasValue)
            .ToList();

        if (approvedRequests.Any())
        {
            stats.AverageApprovalTimeHours = approvedRequests
                .Average(r => (r.ReviewedAt!.Value - r.CreatedAt).TotalHours);
        }

        // Calculate average provisioning time
        var completedRequests = allRequests
            .Where(r => r.CompletedAt.HasValue && r.ReviewedAt.HasValue)
            .ToList();

        if (completedRequests.Any())
        {
            stats.AverageProvisioningTimeHours = completedRequests
                .Average(r => (r.CompletedAt!.Value - r.ReviewedAt!.Value).TotalHours);
        }

        // Calculate success rate
        var totalProcessed = stats.Completed + stats.Failed;
        if (totalProcessed > 0)
        {
            stats.SuccessRate = (double)stats.Completed / totalProcessed * 100;
        }

        return stats;
    }

    public async Task<List<OnboardingRequest>> GetHistoryAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.OnboardingRequests
            .Where(r => r.CreatedAt >= startDate && r.CreatedAt <= endDate)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region Validation

    private bool ValidateRequest(OnboardingRequest request, out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.MissionName))
            errors.Add("Mission name is required");

        if (string.IsNullOrWhiteSpace(request.MissionOwner))
            errors.Add("Mission owner is required");

        if (string.IsNullOrWhiteSpace(request.MissionOwnerEmail))
            errors.Add("Mission owner email is required");

        if (string.IsNullOrWhiteSpace(request.Command))
            errors.Add("Command is required");

        if (string.IsNullOrWhiteSpace(request.RequestedSubscriptionName))
            errors.Add("Subscription name is required");

        if (string.IsNullOrWhiteSpace(request.RequestedVNetCidr))
            errors.Add("VNet CIDR is required");

        // Business justification is optional for initial onboarding
        // It can be added later during the approval process if needed
        // if (string.IsNullOrWhiteSpace(request.BusinessJustification))
        //     errors.Add("Business justification is required");

        return errors.Count == 0;
    }

    #endregion

    #region Notification Helpers

    private async Task SendApprovalNotificationsAsync(
        OnboardingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Send email to mission owner
            var emailRequest = new ApprovalEmailRequest
            {
                RecipientEmail = request.MissionOwnerEmail,
                RecipientName = request.MissionOwner,
                MissionName = request.MissionName,
                RequestId = request.Id,
                ApprovedBy = request.ApprovedBy ?? "NNWC Team",
                ApprovalComments = request.ApprovalComments ?? string.Empty,
                Timestamp = request.ReviewedAt ?? DateTime.UtcNow
            };

            var emailResult = await _emailService.SendApprovalNotificationAsync(emailRequest, cancellationToken);
            
            if (!emailResult.Success)
            {
                _logger.LogWarning("Failed to send approval email to {Email}: {Error}",
                    request.MissionOwnerEmail, emailResult.ErrorMessage);
            }

            // Send Slack notification to NNWC team
            var slackRequest = new SlackApprovalRequest
            {
                MissionName = request.MissionName,
                RequestId = request.Id,
                MissionOwner = request.MissionOwner,
                ApprovedBy = request.ApprovedBy ?? "NNWC Team",
                ClassificationLevel = request.ClassificationLevel,
                Timestamp = request.ReviewedAt ?? DateTime.UtcNow
            };

            var slackResult = await _slackService.SendOnboardingApprovedAsync(slackRequest, cancellationToken);
            
            if (!slackResult.Success)
            {
                _logger.LogWarning("Failed to send approval Slack notification: {Error}",
                    slackResult.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending approval notifications for request {RequestId}", request.Id);
            // Don't fail the approval process due to notification errors
        }
    }

    private async Task SendRejectionNotificationsAsync(
        OnboardingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Send email to mission owner
            var emailRequest = new RejectionEmailRequest
            {
                RecipientEmail = request.MissionOwnerEmail,
                RecipientName = request.MissionOwner,
                MissionName = request.MissionName,
                RequestId = request.Id,
                RejectedBy = request.RejectedBy ?? "NNWC Team",
                RejectionReason = request.RejectionReason ?? "Additional review required",
                Timestamp = request.ReviewedAt ?? DateTime.UtcNow
            };

            var emailResult = await _emailService.SendRejectionNotificationAsync(emailRequest, cancellationToken);
            
            if (!emailResult.Success)
            {
                _logger.LogWarning("Failed to send rejection email to {Email}: {Error}",
                    request.MissionOwnerEmail, emailResult.ErrorMessage);
            }

            // Send Slack notification to NNWC team
            var slackRequest = new SlackRejectionRequest
            {
                MissionName = request.MissionName,
                RequestId = request.Id,
                MissionOwner = request.MissionOwner,
                RejectedBy = request.RejectedBy ?? "NNWC Team",
                RejectionReason = request.RejectionReason ?? "Additional review required",
                Timestamp = request.ReviewedAt ?? DateTime.UtcNow
            };

            var slackResult = await _slackService.SendOnboardingRejectedAsync(slackRequest, cancellationToken);
            
            if (!slackResult.Success)
            {
                _logger.LogWarning("Failed to send rejection Slack notification: {Error}",
                    slackResult.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending rejection notifications for request {RequestId}", request.Id);
            // Don't fail the rejection process due to notification errors
        }
    }

    private async Task SendProvisioningCompleteNotificationsAsync(
        OnboardingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var provisioningDuration = request.ProvisionedAt.HasValue && request.ReviewedAt.HasValue
                ? request.ProvisionedAt.Value - request.ReviewedAt.Value
                : TimeSpan.Zero;

            var environmentName = request.ProvisionedResources.GetValueOrDefault("EnvironmentName", "N/A");
            var deploymentId = request.ProvisionedResources.GetValueOrDefault("DeploymentId", "N/A");
            var portalUrl = $"https://portal.azure.us/#@/resource/subscriptions/{request.ProvisionedSubscriptionId}/overview";

            // Send email to mission owner
            var emailRequest = new ProvisioningCompleteEmailRequest
            {
                RecipientEmail = request.MissionOwnerEmail,
                RecipientName = request.MissionOwner,
                MissionName = request.MissionName,
                RequestId = request.Id,
                SubscriptionId = request.ProvisionedSubscriptionId ?? string.Empty,
                SubscriptionName = request.RequestedSubscriptionName,
                ResourceGroupName = request.ProvisionedResourceGroupId ?? string.Empty,
                VirtualNetworkName = environmentName,
                VirtualNetworkCidr = request.RequestedVNetCidr,
                Subnets = new List<SubnetInfo>(), // Subnets info available in Environment details
                NetworkSecurityGroupName = "See Environment details",
                AzurePortalUrl = portalUrl,
                ClassificationLevel = request.ClassificationLevel,
                DDoSProtectionEnabled = request.ClassificationLevel == "SECRET" || request.ClassificationLevel == "TOP SECRET",
                Tags = new Dictionary<string, string>
                {
                    { "MissionName", request.MissionName },
                    { "Command", request.Command },
                    { "Classification", request.ClassificationLevel },
                    { "EnvironmentName", environmentName },
                    { "DeploymentId", deploymentId }
                },
                Timestamp = request.ProvisionedAt ?? DateTime.UtcNow
            };

            var emailResult = await _emailService.SendProvisioningCompleteNotificationAsync(emailRequest, cancellationToken);
            
            if (!emailResult.Success)
            {
                _logger.LogWarning("Failed to send provisioning complete email to {Email}: {Error}",
                    request.MissionOwnerEmail, emailResult.ErrorMessage);
            }

            // Send Slack notification to NNWC team
            var slackRequest = new SlackProvisioningCompleteRequest
            {
                MissionName = request.MissionName,
                RequestId = request.Id,
                MissionOwner = request.MissionOwner,
                SubscriptionId = request.ProvisionedSubscriptionId ?? string.Empty,
                ResourceGroupName = request.ProvisionedResourceGroupId ?? string.Empty,
                VirtualNetworkName = environmentName,
                SubnetCount = 0, // Subnet details available in Environment view
                ClassificationLevel = request.ClassificationLevel,
                ProvisioningDuration = provisioningDuration,
                Timestamp = request.ProvisionedAt ?? DateTime.UtcNow
            };

            var slackResult = await _slackService.SendProvisioningCompleteAsync(slackRequest, cancellationToken);
            
            if (!slackResult.Success)
            {
                _logger.LogWarning("Failed to send provisioning complete Slack notification: {Error}",
                    slackResult.ErrorMessage);
            }

            _logger.LogInformation("📧 Provisioning complete notifications sent for request {RequestId}. " +
                "Mission owner can view full environment details in the Admin Console Environments view.", 
                request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending provisioning complete notifications for request {RequestId}", request.Id);
            // Don't fail the provisioning completion due to notification errors
        }
    }

    private async Task SendProvisioningFailedNotificationsAsync(
        OnboardingRequest request,
        string failedStep,
        string errorDetails,
        bool autoRollbackCompleted,
        CancellationToken cancellationToken)
    {
        try
        {
            var supportTicketUrl = "https://support.navy.mil/create-ticket";

            // Send email to mission owner
            var emailRequest = new ProvisioningFailedEmailRequest
            {
                RecipientEmail = request.MissionOwnerEmail,
                RecipientName = request.MissionOwner,
                MissionName = request.MissionName,
                RequestId = request.Id,
                FailureReason = request.ProvisioningError ?? "Unknown error occurred",
                ErrorDetails = errorDetails,
                FailedStep = failedStep,
                FailureTimestamp = DateTime.UtcNow,
                SupportTicketUrl = supportTicketUrl,
                AutoRollbackCompleted = autoRollbackCompleted,
                Timestamp = DateTime.UtcNow
            };

            var emailResult = await _emailService.SendProvisioningFailedNotificationAsync(emailRequest, cancellationToken);
            
            if (!emailResult.Success)
            {
                _logger.LogWarning("Failed to send provisioning failed email to {Email}: {Error}",
                    request.MissionOwnerEmail, emailResult.ErrorMessage);
            }

            // Send high-priority Slack notification to NNWC team
            var slackRequest = new SlackProvisioningFailedRequest
            {
                MissionName = request.MissionName,
                RequestId = request.Id,
                MissionOwner = request.MissionOwner,
                FailureReason = request.ProvisioningError ?? "Unknown error occurred",
                FailedStep = failedStep,
                AutoRollbackCompleted = autoRollbackCompleted,
                Timestamp = DateTime.UtcNow
            };

            var slackResult = await _slackService.SendProvisioningFailedAsync(slackRequest, cancellationToken);
            
            if (!slackResult.Success)
            {
                _logger.LogWarning("Failed to send provisioning failed Slack notification: {Error}",
                    slackResult.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending provisioning failed notifications for request {RequestId}", request.Id);
            // Don't fail further due to notification errors
        }
    }

    #endregion

    #region Template Generation Helpers

    /// <summary>
    /// Builds a TemplateGenerationRequest from an OnboardingRequest
    /// Maps Navy Flankspeed requirements to template generation specification
    /// </summary>
    private TemplateGenerationRequest BuildTemplateRequestFromOnboarding(OnboardingRequest request)
    {
        var templateRequest = new TemplateGenerationRequest
        {
            ServiceName = request.MissionName ?? "navy-flankspeed-network",
            Description = request.BusinessJustification ?? "Navy Flankspeed secure network infrastructure",
            TemplateType = "infrastructure",
            
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Bicep, // Navy prefers Azure Bicep
                Provider = CloudProvider.Azure,
                Region = request.Region ?? "eastus",
                ComputePlatform = DetermineComputePlatform(request.RequiredServicesJson),
                IncludeNetworking = true,
                
                NetworkConfig = new NetworkingConfiguration
                {
                    Mode = NetworkMode.CreateNew,
                    VNetName = $"vnet-{request.MissionName ?? "flankspeed"}",
                    VNetAddressSpace = request.RequestedVNetCidr ?? "10.0.0.0/16",
                    EnableNetworkSecurityGroup = true,
                    EnablePrivateDns = true,
                    EnablePrivateEndpoint = true,
                    
                    Subnets = BuildSubnetsFromRequest(request),
                    
                    NsgRules = new List<NetworkSecurityRule>
                    {
                        new NetworkSecurityRule
                        {
                            Name = "Allow-HTTPS-Inbound",
                            Priority = 100,
                            Direction = "Inbound",
                            Access = "Allow",
                            Protocol = "Tcp",
                            SourcePortRange = "*",
                            DestinationPortRange = "443",
                            SourceAddressPrefix = "*", // Will be restricted by Azure policies
                            DestinationAddressPrefix = "*",
                            Description = "Allow HTTPS traffic from authorized sources"
                        },
                        new NetworkSecurityRule
                        {
                            Name = "Deny-All-Inbound",
                            Priority = 4096,
                            Direction = "Inbound",
                            Access = "Deny",
                            Protocol = "*",
                            SourcePortRange = "*",
                            DestinationPortRange = "*",
                            SourceAddressPrefix = "*",
                            DestinationAddressPrefix = "*",
                            Description = "Default deny all inbound traffic"
                        }
                    }
                },
                
                // Zero Trust Security Configuration
                EnablePrivateCluster = ShouldEnablePrivateCluster(request),
                AuthorizedIPRanges = DetermineAuthorizedIPRanges(request),
                EnableWorkloadIdentity = ShouldEnableWorkloadIdentity(request),
                EnableAzurePolicy = true, // Always enabled for compliance
                EnableImageCleaner = ShouldEnableImageCleaner(request),
                HttpsOnly = true, // Always enforce HTTPS
                MinTlsVersion = DetermineTlsVersion(request),
                EnableManagedIdentity = true, // Always use managed identities
                EnableDefender = ShouldEnableDefender(request),
                EnableOIDCIssuer = ShouldEnableWorkloadIdentity(request), // OIDC required for workload identity
                EnableAzureRBAC = true, // Always use Azure RBAC
                NetworkPolicy = DetermineNetworkPolicy(request),
                
                Tags = new Dictionary<string, string>
                {
                    { "Mission", request.MissionName ?? "Navy Flankspeed" },
                    { "Classification", request.ClassificationLevel ?? "IL5" },
                    { "Owner", request.MissionOwner ?? "NNWC" },
                    { "Environment", "Production" },
                    { "ComplianceFramework", "RMF" }
                }
            },

            Databases = ParseDatabaseRequirements(request.MissionName, request.RequiredServicesJson),

            Deployment = new DeploymentSpec
            {
                Replicas = EstimateReplicasFromUserCount(request.EstimatedUserCount),
                AutoScaling = true,
                MinReplicas = 2,
                MaxReplicas = 10,
                TargetCpuPercent = 70,
                TargetMemoryPercent = 80
            },
            
            Security = new SecuritySpec
            {
                NetworkPolicies = true,
                PodSecurityPolicies = true,
                ServiceAccount = true,
                RBAC = true,
                TLS = true,
                SecretsManagement = true,
                ComplianceStandards = new List<string> { "RMF", "NIST-800-53", "IL5" }
            },
            
            Observability = new ObservabilitySpec
            {
                Prometheus = true,
                ApplicationInsights = true,
                StructuredLogging = true,
                DistributedTracing = true
            }
        };

        return templateRequest;
    }

    /// <summary>
    /// Determines the compute platform from requested services string
    /// Examples: "AKS with SQL Server", "Azure App Service with Redis", "Network Only"
    /// </summary>
    private ComputePlatform DetermineComputePlatform(string? requestedServices)
    {
        if (string.IsNullOrWhiteSpace(requestedServices))
            return ComputePlatform.AKS; // Default for Navy Flankspeed

        var services = requestedServices.ToLowerInvariant();

        // Check for specific platforms
        if (services.Contains("aks") || services.Contains("kubernetes"))
            return ComputePlatform.AKS;
        
        if (services.Contains("app service") || services.Contains("webapp"))
            return ComputePlatform.AppService;
        
        if (services.Contains("container apps"))
            return ComputePlatform.ContainerApps;
        
        if (services.Contains("network only") || services.Contains("networking only"))
            return ComputePlatform.Network;

        // Default to AKS for containerized workloads
        return ComputePlatform.AKS;
    }

    /// <summary>
    /// Parses database requirements from requested services string
    /// Examples: "AKS with SQL Server and Redis", "PostgreSQL", "Azure SQL"
    /// </summary>
    private List<DatabaseSpec> ParseDatabaseRequirements(string missionName, string? requestedServices)
    {
        var databases = new List<DatabaseSpec>();

        if (string.IsNullOrWhiteSpace(requestedServices))
            return databases;

        var services = requestedServices.ToLowerInvariant();

        // Check for SQL Server / Azure SQL
        if (services.Contains("sql server") || services.Contains("azure sql"))
        {
            databases.Add(new DatabaseSpec
            {
                Name = $"{missionName}-sqldb",
                Type = DatabaseType.AzureSQL,
                Version = "12.0",
                Tier = DatabaseTier.Standard,
                StorageGB = 256,
                HighAvailability = true,
                BackupEnabled = true,
                RetentionDays = 30,
                Location = DatabaseLocation.Cloud
            });
        }

        // Check for PostgreSQL
        if (services.Contains("postgres"))
        {
            databases.Add(new DatabaseSpec
            {
                Name = $"{missionName}-postgres",
                Type = DatabaseType.PostgreSQL,
                Version = "14",
                Tier = DatabaseTier.Standard,
                StorageGB = 256,
                HighAvailability = true,
                BackupEnabled = true,
                RetentionDays = 30,
                Location = DatabaseLocation.Cloud
            });
        }

        // Check for Redis
        if (services.Contains("redis"))
        {
            databases.Add(new DatabaseSpec
            {
                Name = $"{missionName}-redis",
                Type = DatabaseType.Redis,
                Version = "6.0",
                Tier = DatabaseTier.Premium,
                StorageGB = 32,
                HighAvailability = true,
                BackupEnabled = true,
                RetentionDays = 7,
                Location = DatabaseLocation.Cloud
            });
        }

        // Check for MongoDB
        if (services.Contains("mongo"))
        {
            databases.Add(new DatabaseSpec
            {
                Name = $"{missionName}-mongodb",
                Type = DatabaseType.MongoDB,
                Version = "5.0",
                Tier = DatabaseTier.Standard,
                StorageGB = 256,
                HighAvailability = true,
                BackupEnabled = true,
                RetentionDays = 30,
                Location = DatabaseLocation.Cloud
            });
        }

        return databases;
    }

    /// <summary>
    /// Estimates replica count based on user load
    /// Formula: 1 replica per 500 users, minimum 2, maximum 10
    /// </summary>
    private int EstimateReplicasFromUserCount(int? estimatedUsers)
    {
        if (!estimatedUsers.HasValue || estimatedUsers.Value <= 0)
            return 3; // Default to 3 for high availability

        // 1 replica per 500 users
        int replicas = Math.Max(2, (estimatedUsers.Value / 500) + 1);
        
        // Cap at 10 replicas
        return Math.Min(replicas, 10);
    }

    /// <summary>
    /// Calculates subnet CIDR from VNet CIDR
    /// Example: VNet 10.0.0.0/16 → Subnet 0: 10.0.0.0/24, Subnet 1: 10.0.1.0/24, etc.
    /// </summary>
    private string CalculateSubnetCidr(string vnetCidr, int subnetIndex)
    {
        try
        {
            // Parse VNet CIDR (e.g., "10.0.0.0/16")
            var parts = vnetCidr.Split('/');
            if (parts.Length != 2)
                return $"10.0.{subnetIndex}.0/24"; // Fallback

            var ipParts = parts[0].Split('.');
            if (ipParts.Length != 4)
                return $"10.0.{subnetIndex}.0/24"; // Fallback

            // Assume /24 subnets within the VNet
            // Increment the third octet for each subnet
            return $"{ipParts[0]}.{ipParts[1]}.{subnetIndex}.0/24";
        }
        catch
        {
            // Fallback to safe default
            return $"10.0.{subnetIndex}.0/24";
        }
    }

    /// <summary>
    /// Builds subnet configurations based on onboarding request requirements
    /// Creates subnets dynamically based on requested services and infrastructure needs
    /// </summary>
    private List<SubnetConfiguration> BuildSubnetsFromRequest(OnboardingRequest request)
    {
        var subnets = new List<SubnetConfiguration>();
        var vnetCidr = request.RequestedVNetCidr ?? "10.0.0.0/16";
        var subnetIndex = 0;

        // Always create application subnet for primary workloads
        subnets.Add(new SubnetConfiguration
        {
            Name = "application-subnet",
            AddressPrefix = CalculateSubnetCidr(vnetCidr, subnetIndex++),
            Purpose = SubnetPurpose.Application,
            EnableServiceEndpoints = true,
            ServiceEndpoints = new List<string> { "Microsoft.Storage", "Microsoft.KeyVault" }
        });

        // Add private endpoints subnet if required (always for IL5/SECRET+)
        var requiresPrivateEndpoints = request.ClassificationLevel == "IL5" || 
                                      request.ClassificationLevel == "SECRET" || 
                                      request.ClassificationLevel == "TOP SECRET";
        
        if (requiresPrivateEndpoints)
        {
            subnets.Add(new SubnetConfiguration
            {
                Name = "privateendpoints-subnet",
                AddressPrefix = CalculateSubnetCidr(vnetCidr, subnetIndex++),
                Purpose = SubnetPurpose.PrivateEndpoints
            });
        }

        // Parse required services to determine additional subnets
        var requiredServices = request.RequiredServicesJson?.ToLowerInvariant() ?? "";

        // Database subnet if SQL, PostgreSQL, or other databases requested
        if (requiredServices.Contains("sql") || 
            requiredServices.Contains("postgres") || 
            requiredServices.Contains("mysql") ||
            requiredServices.Contains("database"))
        {
            subnets.Add(new SubnetConfiguration
            {
                Name = "database-subnet",
                AddressPrefix = CalculateSubnetCidr(vnetCidr, subnetIndex++),
                Purpose = SubnetPurpose.Database,
                EnableServiceEndpoints = true,
                ServiceEndpoints = new List<string> { "Microsoft.Sql" }
            });
        }

        // AKS-specific subnets
        if (requiredServices.Contains("aks") || requiredServices.Contains("kubernetes"))
        {
            // AKS requires Application Gateway subnet for ingress
            subnets.Add(new SubnetConfiguration
            {
                Name = "appgateway-subnet",
                AddressPrefix = CalculateSubnetCidr(vnetCidr, subnetIndex++),
                Purpose = SubnetPurpose.ApplicationGateway
            });
        }

        // Azure App Service requires delegation
        if (requiredServices.Contains("app service") || requiredServices.Contains("webapp"))
        {
            // Update application subnet with delegation
            var appSubnet = subnets.First(s => s.Purpose == SubnetPurpose.Application);
            appSubnet.Delegation = "Microsoft.Web/serverFarms";
        }

        return subnets;
    }

    /// <summary>
    /// Determines if private cluster should be enabled based on classification level
    /// Only applicable to AKS clusters
    /// </summary>
    private bool ShouldEnablePrivateCluster(OnboardingRequest request)
    {
        var requiredServices = request.RequiredServicesJson?.ToLowerInvariant() ?? "";
        
        // Only relevant for AKS/Kubernetes
        if (requiredServices.Contains("aks") && requiredServices.Contains("kubernetes"))
        {
            // Private cluster required for classified AKS workloads
            return request.ClassificationLevel == "SECRET" || 
                request.ClassificationLevel == "TOP SECRET" ||
                request.ClassificationLevel == "IL5" ||
                request.ClassificationLevel == "IL6";
        }  

        return false;
    }

    /// <summary>
    /// Determines authorized IP ranges for cluster access
    /// Only applicable to AKS clusters
    /// Returns null for maximum security (private only), or specific ranges if provided
    /// </summary>
    private string? DetermineAuthorizedIPRanges(OnboardingRequest request)
    {
        var requiredServices = request.RequiredServicesJson?.ToLowerInvariant() ?? "";
        
        // Only relevant for AKS/Kubernetes
        if (!requiredServices.Contains("aks") && !requiredServices.Contains("kubernetes"))
        {
            return null;
        }

        // For classified systems, no public IP access allowed
        if (request.ClassificationLevel == "SECRET" || request.ClassificationLevel == "TOP SECRET")
        {
            return null; // No public access
        }

        // For IL5, might have specific IP ranges from the request
        // This would come from a future field on OnboardingRequest
        return null; // Default to private-only until IP ranges are explicitly provided
    }

    /// <summary>
    /// Determines if workload identity should be enabled
    /// Modern authentication method - enabled for AKS/Kubernetes and App Service
    /// </summary>
    private bool ShouldEnableWorkloadIdentity(OnboardingRequest request)
    {
        var requiredServices = request.RequiredServicesJson?.ToLowerInvariant() ?? "";
        
        // Enable for AKS/Kubernetes workloads
        if (requiredServices.Contains("aks") || requiredServices.Contains("kubernetes"))
        {
            return true;
        }

        // Enable for App Service with managed identity
        if (requiredServices.Contains("app service") || requiredServices.Contains("webapp"))
        {
            return true;
        }

        // Not applicable to other platforms
        return false;
    }

    /// <summary>
    /// Determines if image cleaner should be enabled
    /// Only applicable to AKS - removes unused container images to reduce attack surface
    /// </summary>
    private bool ShouldEnableImageCleaner(OnboardingRequest request)
    {
        var requiredServices = request.RequiredServicesJson?.ToLowerInvariant() ?? "";
        
        // Only enable for AKS/Kubernetes platforms
        return requiredServices.Contains("aks") || 
               requiredServices.Contains("kubernetes");
    }

    /// <summary>
    /// Determines TLS version based on classification level
    /// Higher classifications require newer TLS versions
    /// </summary>
    private string DetermineTlsVersion(OnboardingRequest request)
    {
        // SECRET and TOP SECRET require TLS 1.3
        if (request.ClassificationLevel == "SECRET" || request.ClassificationLevel == "TOP SECRET")
        {
            return "1.3";
        }

        // IL5/IL6 require at least TLS 1.2
        if (request.ClassificationLevel == "IL5" || request.ClassificationLevel == "IL6")
        {
            return "1.2";
        }

        // Default to TLS 1.2 (minimum acceptable)
        return "1.2";
    }

    /// <summary>
    /// Determines if Microsoft Defender for Cloud should be enabled
    /// Required for classified workloads
    /// </summary>
    private bool ShouldEnableDefender(OnboardingRequest request)
    {
        // Always enable for classified systems
        if (request.ClassificationLevel == "SECRET" || 
            request.ClassificationLevel == "TOP SECRET" ||
            request.ClassificationLevel == "IL5" ||
            request.ClassificationLevel == "IL6")
        {
            return true;
        }

        // Enable by default for all production workloads
        return true;
    }

    /// <summary>
    /// Determines network policy engine for Kubernetes
    /// Azure or Calico based on requirements
    /// </summary>
    private string? DetermineNetworkPolicy(OnboardingRequest request)
    {
        var requiredServices = request.RequiredServicesJson?.ToLowerInvariant() ?? "";
        
        // Only relevant for AKS/Kubernetes
        if (!requiredServices.Contains("aks") && !requiredServices.Contains("kubernetes"))
        {
            return null;
        }

        // Use Azure Network Policy for classified workloads (better Azure integration)
        if (request.ClassificationLevel == "SECRET" || 
            request.ClassificationLevel == "TOP SECRET" ||
            request.ClassificationLevel == "IL5" ||
            request.ClassificationLevel == "IL6")
        {
            return "azure";
        }

        // Default to Azure Network Policy
        return "azure";
    }

    #endregion
}
