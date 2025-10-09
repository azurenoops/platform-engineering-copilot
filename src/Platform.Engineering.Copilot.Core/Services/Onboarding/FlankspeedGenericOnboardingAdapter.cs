using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models.Onboarding;
using Platform.Engineering.Copilot.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Services.Onboarding;

/// <summary>
/// Adapter that wraps IOnboardingService to implement IGenericOnboardingService for Flankspeed.
/// Provides workflow configuration and validates requests according to Flankspeed requirements.
/// </summary>
public class FlankspeedGenericOnboardingAdapter : IGenericOnboardingService<OnboardingRequest>
{
    private readonly IOnboardingService _onboardingService;
    private readonly OnboardingWorkflowConfig _workflowConfig;

    public FlankspeedGenericOnboardingAdapter(IOnboardingService onboardingService)
    {
        _onboardingService = onboardingService ?? throw new ArgumentNullException(nameof(onboardingService));
        _workflowConfig = FlankspeedWorkflowConfig.GetConfiguration();
    }

    public OnboardingWorkflowConfig GetWorkflowConfig() => _workflowConfig;

    public async Task<OnboardingRequest> CreateDraftAsync(
        string userEmail, 
        CancellationToken cancellationToken = default)
    {
        var requestId = await _onboardingService.CreateDraftRequestAsync(cancellationToken);

        var request = await _onboardingService.GetRequestAsync(requestId, cancellationToken);
        return request ?? throw new InvalidOperationException($"Failed to retrieve created request {requestId}");
    }

    public async Task<OnboardingRequest> UpdateDraftAsync(
        string requestId, 
        Dictionary<string, object> updates, 
        CancellationToken cancellationToken = default)
    {
        // Map field IDs to database field names
        var mappedUpdates = MapFieldIdsToDatabaseFields(updates);
        
        await _onboardingService.UpdateDraftAsync(requestId, mappedUpdates, cancellationToken);
        
        var request = await _onboardingService.GetRequestAsync(requestId, cancellationToken);
        return request ?? throw new InvalidOperationException($"Request {requestId} not found after update");
    }

    public async Task<OnboardingRequest?> GetDraftAsync(
        string requestId, 
        CancellationToken cancellationToken = default)
    {
        return await _onboardingService.GetRequestAsync(requestId, cancellationToken);
    }

    public async Task<List<OnboardingRequest>> GetPendingRequestsByUserAsync(
        string userEmail, 
        CancellationToken cancellationToken = default)
    {
        var requests = await _onboardingService.GetRequestsByOwnerAsync(userEmail, cancellationToken);
        return requests.Where(r => r.Status == OnboardingStatus.Draft || r.Status == OnboardingStatus.PendingReview).ToList();
    }

    public async Task<OnboardingValidationResult> ValidateRequestAsync(
        OnboardingRequest request, 
        CancellationToken cancellationToken = default)
    {
        var result = new OnboardingValidationResult
        {
            IsValid = true,
            CurrentPhase = DetermineCurrentPhase(request),
            CompletionPercentage = (int)CalculateCompletionPercentage(request)
        };

        // Validate mission details (reject both null/empty AND placeholder values)
        if (string.IsNullOrWhiteSpace(request.MissionName) || request.MissionName == "Draft - To Be Provided")
        {
            result.Errors.Add(new ValidationError { FieldId = "missionName", Message = "Mission name is required" });
            result.IsValid = false;
        }

        if (string.IsNullOrWhiteSpace(request.MissionOwner) || request.MissionOwner == "Draft - To Be Provided")
        {
            result.Errors.Add(new ValidationError { FieldId = "missionOwner", Message = "Mission owner name is required" });
            result.IsValid = false;
        }

        if (string.IsNullOrWhiteSpace(request.MissionOwnerEmail) || request.MissionOwnerEmail == "draft@navy.mil")
        {
            result.Errors.Add(new ValidationError { FieldId = "missionOwnerEmail", Message = "Mission owner email is required" });
            result.IsValid = false;
        }

        if (string.IsNullOrWhiteSpace(request.Command) || request.Command == "Draft - To Be Provided")
        {
            result.Errors.Add(new ValidationError { FieldId = "command", Message = "Command is required" });
            result.IsValid = false;
        }

        if (string.IsNullOrWhiteSpace(request.ClassificationLevel))
        {
            result.Errors.Add(new ValidationError { FieldId = "classificationLevel", Message = "Classification level is required" });
            result.IsValid = false;
        }

        // Validate technical requirements (reject placeholder values)
        if (string.IsNullOrWhiteSpace(request.RequestedSubscriptionName) || request.RequestedSubscriptionName == "Draft - To Be Provided")
        {
            result.Errors.Add(new ValidationError { FieldId = "subscriptionName", Message = "Subscription name is required" });
            result.IsValid = false;
        }

        if (string.IsNullOrWhiteSpace(request.RequestedVNetCidr))
        {
            result.Errors.Add(new ValidationError { FieldId = "vnetCidr", Message = "VNet CIDR is required" });
            result.IsValid = false;
        }

        if (request.RequiredServices == null || !request.RequiredServices.Any())
        {
            result.Errors.Add(new ValidationError { FieldId = "requiredServices", Message = "At least one required service must be specified" });
            result.IsValid = false;
        }

        if (request.EstimatedUserCount <= 0)
        {
            result.Errors.Add(new ValidationError { FieldId = "estimatedUserCount", Message = "Estimated user count must be greater than 0" });
            result.IsValid = false;
        }

        if (request.EstimatedDataVolumeTB <= 0)
        {
            result.Errors.Add(new ValidationError { FieldId = "dataVolumeTB", Message = "Data volume must be greater than 0" });
            result.IsValid = false;
        }

        // Build missing fields list
        result.MissingRequiredFields = result.Errors.Select(e => e.FieldId).ToList();

        return await Task.FromResult(result);
    }

    public async Task<OnboardingRequest> SubmitForApprovalAsync(
        string requestId, 
        CancellationToken cancellationToken = default)
    {
        await _onboardingService.SubmitRequestAsync(requestId, cancellationToken);
        
        var request = await _onboardingService.GetRequestAsync(requestId, cancellationToken);
        return request ?? throw new InvalidOperationException($"Request {requestId} not found after submission");
    }

    public async Task<bool> CancelDraftAsync(
        string requestId, 
        CancellationToken cancellationToken = default)
    {
        await _onboardingService.CancelRequestAsync(requestId, "Cancelled by user", cancellationToken);
        return true;
    }

    private string DetermineCurrentPhase(OnboardingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MissionName) || 
            string.IsNullOrWhiteSpace(request.MissionOwner) ||
            string.IsNullOrWhiteSpace(request.Command))
        {
            return "mission_details";
        }

        if (string.IsNullOrWhiteSpace(request.RequestedSubscriptionName) ||
            string.IsNullOrWhiteSpace(request.RequestedVNetCidr) ||
            request.RequiredServices == null || !request.RequiredServices.Any())
        {
            return "technical_requirements";
        }

        if (string.IsNullOrWhiteSpace(request.BusinessJustification))
        {
            return "business_justification";
        }

        return "review";
    }

    private decimal CalculateCompletionPercentage(OnboardingRequest request)
    {
        var totalFields = 12; // Total required fields
        var completedFields = 0;

        if (!string.IsNullOrWhiteSpace(request.MissionName)) completedFields++;
        if (!string.IsNullOrWhiteSpace(request.MissionOwner)) completedFields++;
        if (!string.IsNullOrWhiteSpace(request.MissionOwnerEmail)) completedFields++;
        if (!string.IsNullOrWhiteSpace(request.Command)) completedFields++;
        if (!string.IsNullOrWhiteSpace(request.ClassificationLevel)) completedFields++;
        if (!string.IsNullOrWhiteSpace(request.RequestedSubscriptionName)) completedFields++;
        if (!string.IsNullOrWhiteSpace(request.RequestedVNetCidr)) completedFields++;
        if (request.RequiredServices != null && request.RequiredServices.Any()) completedFields++;
        if (request.EstimatedUserCount > 0) completedFields++;
        if (request.EstimatedDataVolumeTB > 0) completedFields++;
        if (!string.IsNullOrWhiteSpace(request.Region)) completedFields++;
        if (!string.IsNullOrWhiteSpace(request.BusinessJustification)) completedFields++;

        return (decimal)completedFields / totalFields * 100;
    }

    /// <summary>
    /// Maps workflow field IDs to database field names using the workflow configuration
    /// </summary>
    private Dictionary<string, object> MapFieldIdsToDatabaseFields(Dictionary<string, object> fieldUpdates)
    {
        var mappedUpdates = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var (fieldId, value) in fieldUpdates)
        {
            // Find the field definition in the workflow config
            var fieldDef = _workflowConfig.Phases
                .SelectMany(p => p.Fields)
                .FirstOrDefault(f => f.FieldId.Equals(fieldId, StringComparison.OrdinalIgnoreCase));

            if (fieldDef != null)
            {
                // Use DatabaseFieldName if specified, otherwise use FieldId
                var databaseFieldName = fieldDef.DatabaseFieldName ?? fieldDef.FieldId;
                
                // Convert to PascalCase for C# property names
                var propertyName = ConvertToPascalCase(databaseFieldName);
                mappedUpdates[propertyName] = value;
            }
            else
            {
                // Field not in config, use as-is (convert to PascalCase)
                var propertyName = ConvertToPascalCase(fieldId);
                mappedUpdates[propertyName] = value;
            }
        }

        return mappedUpdates;
    }

    /// <summary>
    /// Converts a field name to PascalCase for C# property matching
    /// </summary>
    private string ConvertToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Split by underscores or camelCase boundaries
        var words = System.Text.RegularExpressions.Regex.Split(input, @"[_\s]|(?<=[a-z])(?=[A-Z])")
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w => char.ToUpper(w[0]) + w.Substring(1).ToLower());

        return string.Join("", words);
    }
}
