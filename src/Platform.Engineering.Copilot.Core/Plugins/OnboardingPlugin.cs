using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Services.Onboarding.Parsing;
using Platform.Engineering.Copilot.Data.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Platform.Engineering.Copilot.Core.Plugins;

/// <summary>
/// Semantic Kernel plugin for Navy Flankspeed mission owner onboarding
/// </summary>
public class OnboardingPlugin : BaseSupervisorPlugin
{
    private readonly IOnboardingService _onboardingService;

    public OnboardingPlugin(
        ILogger<OnboardingPlugin> logger,
        Kernel kernel,
        IOnboardingService onboardingService) : base(logger, kernel)
    {
        _onboardingService = onboardingService ?? throw new ArgumentNullException(nameof(onboardingService));
    }

    [KernelFunction("capture_onboarding_requirements")]
    [Description("FIRST STEP for new mission onboarding. Captures requirements and generates review summary. Use when user says 'onboard', 'new mission', or provides mission details. DO NOT call create_environment - this function only creates a draft for review.")]
    public async Task<string> CaptureOnboardingRequirementsAsync(
        [Description("Mission name")] string missionName,
        [Description("Organization")] string organization,
        [Description(@"Additional requirements in any format:
- JSON: {""classificationLevel"": ""Secret"", ""environmentType"": ""Production"", ...}
- Bullet list: ""- Classification: Secret\n- Environment: Production\n...""
- Comma-separated: ""Classification is Secret, environment is Production, ...""
- Natural language: ""We need a Secret classification production environment in East US...""
The AI will automatically parse and extract: classificationLevel, environmentType, region, requiredServices, networkRequirements, computeRequirements, databaseRequirements, complianceFrameworks, securityControls, targetDeploymentDate, expectedGoLiveDate")] string? additionalRequirements = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Capturing onboarding requirements for mission: {MissionName}", missionName);

            // Create draft onboarding request
            var requestId = await _onboardingService.CreateDraftRequestAsync(cancellationToken);

            // Build context data from parameters
            var context = new Dictionary<string, object?>
            {
                ["missionName"] = missionName,
                ["organization"] = organization
            };

            // Parse additional requirements using multi-strategy parser
            if (!string.IsNullOrWhiteSpace(additionalRequirements))
            {
                // Create logger for parser using logger factory
                var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
                var parserLogger = loggerFactory.CreateLogger<RequirementsParser>();
                var parser = new RequirementsParser(parserLogger, _kernel);
                var parsed = await parser.ParseAsync(additionalRequirements);
                
                if (parsed == null || parsed.Count == 0)
                {
                    _logger.LogWarning("Could not parse any requirements from input: {Input}", 
                        additionalRequirements.Substring(0, Math.Min(100, additionalRequirements.Length)));
                    
                    return $@"⚠️  Could not parse requirements. Please provide in one of these formats:

**1. JSON:**
```json
{{""classificationLevel"": ""Secret"", ""environmentType"": ""Production"", ""region"": ""US Gov Virginia""}}
```

**2. Bullet list:**
```
- Classification: Secret
- Environment: Production
- Region: US Gov Virginia
```

**3. Comma-separated:**
```
Classification is Secret, environment is Production, region is US Gov Virginia
```

**4. Natural language:**
```
We need a Secret classification production environment in US Gov Virginia
```

Or I can guide you through the requirements step-by-step. Just say **'help me with requirements'**.";
                }
                
                // Log what was successfully parsed
                _logger.LogInformation("Successfully parsed {Count} requirements: {Keys}", 
                    parsed.Count, string.Join(", ", parsed.Keys));
                
                // Merge parsed data into context
                foreach (var kvp in parsed)
                {
                    context[kvp.Key] = kvp.Value;
                }
            }

            // Update draft with all requirements
            var updated = await _onboardingService.UpdateDraftAsync(requestId, context, cancellationToken);

            if (!updated)
            {
                return $"❌ Failed to capture requirements for onboarding request {requestId}. Please try again.";
            }

            // Generate detailed review summary
            var summary = new StringBuilder();
            summary.AppendLine($"# 📋 Onboarding Request Review - {missionName}");
            summary.AppendLine();
            summary.AppendLine($"**Request ID:** `{requestId}`");
            summary.AppendLine($"**Status:** Draft (Pending User Confirmation)");
            summary.AppendLine();
            
            summary.AppendLine("## Mission Details");
            summary.AppendLine($"- **Mission Name:** {missionName}");
            summary.AppendLine($"- **Organization:** {organization}");
            
            if (context.TryGetValue("classificationLevel", out var classification))
                summary.AppendLine($"- **Classification:** {classification}");
            if (context.TryGetValue("environmentType", out var envType))
                summary.AppendLine($"- **Environment:** {envType}");
            if (context.TryGetValue("region", out var region))
                summary.AppendLine($"- **Region:** {region}");
            summary.AppendLine();

            if (context.TryGetValue("requiredServices", out var services))
            {
                summary.AppendLine("## Infrastructure to be Provisioned");
                var serviceList = services?.ToString()?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                foreach (var service in serviceList)
                {
                    summary.AppendLine($"- ✅ **{service.Trim()}**");
                }
                summary.AppendLine();
            }

            if (context.TryGetValue("networkRequirements", out var network) && network != null)
            {
                summary.AppendLine("## Network Configuration");
                summary.AppendLine($"{network}");
                summary.AppendLine();
            }

            if (context.TryGetValue("computeRequirements", out var compute) && compute != null)
            {
                summary.AppendLine("## Compute Resources");
                summary.AppendLine($"{compute}");
                summary.AppendLine();
            }

            if (context.TryGetValue("databaseRequirements", out var database) && database != null)
            {
                summary.AppendLine("## Database Requirements");
                summary.AppendLine($"{database}");
                summary.AppendLine();
            }

            if (context.TryGetValue("complianceFrameworks", out var compliance) && compliance != null)
            {
                summary.AppendLine("## Compliance & Security");
                summary.AppendLine($"**Frameworks:** {compliance}");
                if (context.TryGetValue("securityControls", out var secControls) && secControls != null)
                {
                    summary.AppendLine($"**Security Controls:** {secControls}");
                }
                summary.AppendLine();
            }

            if (context.TryGetValue("targetDeploymentDate", out var deployDate) && deployDate != null)
            {
                summary.AppendLine("## Timeline");
                summary.AppendLine($"- **Target Deployment:** {deployDate}");
                if (context.TryGetValue("expectedGoLiveDate", out var goLive) && goLive != null)
                {
                    summary.AppendLine($"- **Expected Go-Live:** {goLive}");
                }
                summary.AppendLine();
            }

            summary.AppendLine("## Estimated Provisioning Time");
            summary.AppendLine($"- Infrastructure deployment: ~30-45 minutes");
            summary.AppendLine($"- Compliance configuration: ~15-20 minutes");
            summary.AppendLine($"- Total estimated time: ~1 hour");
            summary.AppendLine();

            summary.AppendLine("## Next Steps");
            summary.AppendLine();
            summary.AppendLine("⚠️ **IMPORTANT:** Please review the above configuration carefully.");
            summary.AppendLine();
            summary.AppendLine($"**To submit for platform team approval, respond with one of these phrases:**");
            summary.AppendLine("- 'Yes, proceed'");
            summary.AppendLine("- 'Confirm and submit'");
            summary.AppendLine("- 'Go ahead'");
            summary.AppendLine();
            summary.AppendLine($"_(I will automatically use request ID `{requestId}` when you confirm)_");
            summary.AppendLine();
            summary.AppendLine("ℹ️ **Note:** Your request will be submitted to the NAVWAR Platform Engineering team for review.");
            summary.AppendLine("Provisioning will begin automatically once approved by the platform team.");
            summary.AppendLine();
            summary.AppendLine("**To make changes, respond with:**");
            summary.AppendLine($"- 'Update request {requestId} with [your changes]'");
            summary.AppendLine();
            summary.AppendLine("**To cancel, respond with:**");
            summary.AppendLine($"- 'Cancel request {requestId}'");

            return summary.ToString();
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("capture onboarding requirements", ex);
        }
    }

    [KernelFunction("submit_for_approval")]
    [Description("Submit an onboarding request for platform team approval. Use this AFTER the user confirms they want to proceed. This will NOT start provisioning - it creates a pending approval request in the admin console for the platform team to review and approve/deny.")]
    public async Task<string> SubmitForApprovalAsync(
        [Description("Request ID from capture_onboarding_requirements (GUID format)")] string requestId,
        [Description("Email of the user submitting the request (typically the mission owner)")] string? submittedBy = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Submitting onboarding request {RequestId} for approval by {SubmittedBy}", 
                requestId, submittedBy ?? "unknown");

            // Get the request to show details in confirmation
            var request = await _onboardingService.GetRequestAsync(requestId, cancellationToken);
            
            if (request == null)
            {
                return $"❌ **Error:** Onboarding request `{requestId}` not found. Please verify the request ID.";
            }

            // First validate the request
            var validationErrors = await _onboardingService.ValidateForSubmissionAsync(requestId, cancellationToken);
            
            if (validationErrors.Count > 0)
            {
                var errorResponse = new StringBuilder();
                errorResponse.AppendLine("❌ **SUBMISSION BLOCKED - Required Information Missing**");
                errorResponse.AppendLine();
                errorResponse.AppendLine($"I cannot submit request `{requestId}` for approval because the following required information is missing:");
                errorResponse.AppendLine();
                foreach (var error in validationErrors)
                {
                    errorResponse.AppendLine($"❌ {error}");
                }
                errorResponse.AppendLine();
                errorResponse.AppendLine("📝 **What you need to do:**");
                errorResponse.AppendLine();
                errorResponse.AppendLine("Please provide the missing information so I can complete your onboarding request. For example:");
                errorResponse.AppendLine();
                errorResponse.AppendLine("*\"For Mission Alpha onboarding:*");
                errorResponse.AppendLine($"*- Mission owner is John Doe*");
                errorResponse.AppendLine($"*- Email is john.doe@navy.mil*");
                errorResponse.AppendLine($"*- Command is NAVSEA*");
                errorResponse.AppendLine($"*- Subscription name should be mission-alpha-prod*");
                errorResponse.AppendLine($"*- VNet CIDR should be 10.0.0.0/16\"*");
                errorResponse.AppendLine();
                errorResponse.AppendLine("Once you provide this information, I'll update the request and submit it for approval.");
                return errorResponse.ToString();
            }

            // Submit for approval
            var success = await _onboardingService.SubmitRequestAsync(requestId, submittedBy, cancellationToken);

            if (!success)
            {
                return $"❌ **Error:** Failed to submit request `{requestId}` for approval. The request may already be submitted.";
            }

            // Build success response
            var response = new StringBuilder();
            response.AppendLine("✅ **Onboarding Request Submitted for Approval**");
            response.AppendLine();
            response.AppendLine($"**Request ID:** `{requestId}`");
            response.AppendLine($"**Mission:** {request.MissionName}");
            response.AppendLine($"**Status:** Pending Platform Team Review");
            response.AppendLine($"**Submitted By:** {submittedBy ?? request.MissionOwnerEmail}");
            response.AppendLine();
            response.AppendLine("## What Happens Next?");
            response.AppendLine();
            response.AppendLine("1. **Platform Team Review** - Your request has been submitted to the NAVWAR Platform Engineering team for review");
            response.AppendLine("2. **Admin Console** - The team will review your requirements in the Admin Console");
            response.AppendLine("3. **Approval Decision** - The team will either approve or request changes");
            response.AppendLine("4. **Automatic Provisioning** - Once approved, your environment will be automatically provisioned");
            response.AppendLine("5. **Email Notification** - You'll receive an email when your request is approved or if changes are needed");
            response.AppendLine();
            response.AppendLine("📧 **You will be notified via email** when the platform team makes a decision on your request.");
            response.AppendLine();
            response.AppendLine($"💡 **Track your request:** You can check the status anytime by asking: \"What's the status of request {requestId}?\"");

            return response.ToString();
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("submit onboarding request for approval", ex);
        }
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var str = element.GetString();
                if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dateTime))
                {
                    return dateTime;
                }

                if (Guid.TryParse(str, out var guid))
                {
                    return guid.ToString();
                }

                return str;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longValue))
                {
                    return longValue;
                }

                if (element.TryGetDouble(out var doubleValue))
                {
                    return doubleValue;
                }

                return element.GetDecimal();
            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetBoolean();
            case JsonValueKind.Object:
            {
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in element.EnumerateObject())
                {
                    dict[prop.Name] = ConvertJsonElement(prop.Value);
                }
                return dict;
            }
            case JsonValueKind.Array:
            {
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(ConvertJsonElement(item));
                }
                return list;
            }
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            default:
                return element.GetRawText();
        }
    }
}
