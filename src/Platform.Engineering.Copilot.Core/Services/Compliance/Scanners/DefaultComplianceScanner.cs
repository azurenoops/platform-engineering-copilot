using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Compliance;

/// <summary>
/// Default scanner for control families without specific implementations
/// </summary>
public class DefaultComplianceScanner : IComplianceScanner
{
    private readonly ILogger _logger;

    public DefaultComplianceScanner(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<AtoFinding>> ScanControlAsync(
        string subscriptionId, 
        NistControl control, 
        CancellationToken cancellationToken = default)
    {
        return await ScanControlAsync(subscriptionId, null, control, cancellationToken);
    }

    /// <summary>
    /// Resource group-scoped scanning with optional RG parameter
    /// </summary>
    public async Task<List<AtoFinding>> ScanControlAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control,
        CancellationToken cancellationToken = default)
    {
        var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
        _logger.LogDebug("Running default scan for control {ControlId} in {Scope} in subscription {SubscriptionId}", 
            control.Id, scope, subscriptionId);

        var findings = new List<AtoFinding>();

        // Basic compliance check - simulate finding issues 10% of the time
        if (Random.Shared.Next(100) < 10)
        {
            var resourceId = string.IsNullOrEmpty(resourceGroupName) 
                ? $"/subscriptions/{subscriptionId}" 
                : $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}";
            
            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = resourceId,
                ResourceType = string.IsNullOrEmpty(resourceGroupName) ? "Subscription" : "ResourceGroup",
                ResourceName = string.IsNullOrEmpty(resourceGroupName) ? "Subscription" : resourceGroupName,
                Title = "Control Implementation Gap",
                FindingType = AtoFindingType.Compliance,
                Severity = AtoFindingSeverity.Low,
                Description = $"Potential implementation gap for control {control.Id}: {control.Title}",
                Recommendation = "Review control requirements and ensure proper implementation",
                ComplianceStatus = AtoComplianceStatus.NonCompliant,
                AffectedNistControls = new List<string> { control.Id ?? "CM-1" },
                ComplianceFrameworks = new List<string> { "NIST-800-53" },
                DetectedAt = DateTime.UtcNow
            });
        }

        await Task.Delay(100, cancellationToken); // Simulate scan work
        return findings;
    }
}
