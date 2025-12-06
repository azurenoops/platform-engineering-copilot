using Platform.Engineering.Copilot.Core.Constants;

namespace Platform.Engineering.Copilot.Core.Helpers;

/// <summary>
/// Static helper methods for compliance operations.
/// Consolidates common formatting and lookup methods used across compliance services.
/// </summary>
public static class ComplianceHelpers
{
    /// <summary>
    /// NIST 800-53 control family names mapped by code.
    /// Uses ComplianceConstants for the codes but maintains display names here.
    /// </summary>
    private static readonly Dictionary<string, string> ControlFamilyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { ComplianceConstants.ControlFamilies.AccessControl, "Access Control" },
        { ComplianceConstants.ControlFamilies.AwarenessTraining, "Awareness and Training" },
        { ComplianceConstants.ControlFamilies.AuditAccountability, "Audit and Accountability" },
        { ComplianceConstants.ControlFamilies.SecurityAssessment, "Security Assessment and Authorization" },
        { ComplianceConstants.ControlFamilies.ConfigurationManagement, "Configuration Management" },
        { ComplianceConstants.ControlFamilies.ContingencyPlanning, "Contingency Planning" },
        { ComplianceConstants.ControlFamilies.IdentificationAuthentication, "Identification and Authentication" },
        { ComplianceConstants.ControlFamilies.IncidentResponse, "Incident Response" },
        { ComplianceConstants.ControlFamilies.Maintenance, "Maintenance" },
        { ComplianceConstants.ControlFamilies.MediaProtection, "Media Protection" },
        { ComplianceConstants.ControlFamilies.PhysicalEnvironmental, "Physical and Environmental Protection" },
        { ComplianceConstants.ControlFamilies.Planning, "Planning" },
        { ComplianceConstants.ControlFamilies.ProgramManagement, "Program Management" },
        { ComplianceConstants.ControlFamilies.PersonnelSecurity, "Personnel Security" },
        { ComplianceConstants.ControlFamilies.RiskAssessment, "Risk Assessment" },
        { ComplianceConstants.ControlFamilies.SystemServicesAcquisition, "System and Services Acquisition" },
        { ComplianceConstants.ControlFamilies.SystemCommunications, "System and Communications Protection" },
        { ComplianceConstants.ControlFamilies.SystemInformationIntegrity, "System and Information Integrity" }
    };

    /// <summary>
    /// Azure resource type display names with icons.
    /// Uses ComplianceConstants for resource type identifiers.
    /// </summary>
    private static readonly Dictionary<string, string> ResourceTypeDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { ComplianceConstants.AzureResourceTypes.PolicyAssignment, "📋 Azure Policy Assignments" },
        { ComplianceConstants.AzureResourceTypes.DiagnosticSettings, "📊 Diagnostic Settings" },
        { ComplianceConstants.AzureResourceTypes.NetworkSecurityGroup, "🔒 Network Security Groups" },
        { ComplianceConstants.AzureResourceTypes.VirtualMachine, "💻 Virtual Machines" },
        { ComplianceConstants.AzureResourceTypes.StorageAccount, "💾 Storage Accounts" },
        { ComplianceConstants.AzureResourceTypes.KeyVault, "🔑 Key Vaults" },
        { ComplianceConstants.AzureResourceTypes.SqlServer, "🗄️ SQL Servers" },
        { ComplianceConstants.AzureResourceTypes.AppService, "🌐 App Services" },
        { ComplianceConstants.AzureResourceTypes.AksCluster, "☸️ AKS Clusters" },
        { ComplianceConstants.AzureResourceTypes.VirtualNetwork, "🌐 Virtual Networks" },
        { ComplianceConstants.AzureResourceTypes.LoadBalancer, "⚖️ Load Balancers" },
        { ComplianceConstants.AzureResourceTypes.ApplicationGateway, "🚪 Application Gateways" },
        { ComplianceConstants.AzureResourceTypes.LogAnalyticsWorkspace, "📈 Log Analytics Workspaces" },
        { ComplianceConstants.AzureResourceTypes.SecurityContact, "👥 Security Contacts" },
        { ComplianceConstants.AzureResourceTypes.ResourceGroup, "📁 Resource Groups" },
        { ComplianceConstants.AzureResourceTypes.Subscription, "🏢 Subscription" },
        { ComplianceConstants.AzureResourceTypes.CosmosDb, "🌍 Cosmos DB" },
        { ComplianceConstants.AzureResourceTypes.ServiceBus, "📨 Service Bus" },
        { ComplianceConstants.AzureResourceTypes.EventHub, "📡 Event Hub" },
        { ComplianceConstants.AzureResourceTypes.RedisCache, "⚡ Redis Cache" },
        { ComplianceConstants.AzureResourceTypes.ContainerRegistry, "📦 Container Registry" }
    };

    /// <summary>
    /// Gets the human-readable name for a NIST control family code.
    /// </summary>
    /// <param name="familyCode">Two-letter control family code (e.g., "AC", "AU", "SC").</param>
    /// <returns>Full family name or the original code if not found.</returns>
    public static string GetControlFamilyName(string familyCode)
    {
        if (string.IsNullOrWhiteSpace(familyCode))
            return familyCode;

        return ControlFamilyNames.TryGetValue(familyCode.Trim(), out var name) 
            ? name 
            : familyCode;
    }

    /// <summary>
    /// Gets the display name with icon for an Azure resource type.
    /// </summary>
    /// <param name="resourceType">Azure resource type (e.g., "Microsoft.Storage/storageAccounts").</param>
    /// <returns>Display name with icon or a generic display if not found.</returns>
    public static string GetResourceTypeDisplayName(string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            return "🔧 Unknown Resource";

        return ResourceTypeDisplayNames.TryGetValue(resourceType.Trim(), out var displayName) 
            ? displayName 
            : $"🔧 {resourceType}";
    }

    /// <summary>
    /// Converts a compliance score (0-100) to a letter grade.
    /// </summary>
    /// <param name="score">Compliance score from 0 to 100.</param>
    /// <returns>Letter grade (A+ to F).</returns>
    public static string GetComplianceGrade(double score)
    {
        return score switch
        {
            >= 95 => "A+",
            >= 90 => "A",
            >= 85 => "A-",
            >= 80 => "B+",
            >= 75 => "B",
            >= 70 => "B-",
            >= 65 => "C+",
            >= 60 => "C",
            >= 55 => "C-",
            >= 50 => "D",
            _ => "F"
        };
    }

    /// <summary>
    /// Generates a visual progress bar for a score.
    /// </summary>
    /// <param name="score">Score from 0 to 100.</param>
    /// <param name="width">Number of characters in the bar (default 10).</param>
    /// <param name="includePercentage">Whether to append percentage to the bar.</param>
    /// <returns>Visual bar like "████████░░ 80.0%".</returns>
    public static string GenerateScoreBar(double score, int width = 10, bool includePercentage = true)
    {
        // Clamp score between 0 and 100
        var clampedScore = Math.Max(0, Math.Min(100, score));
        
        var filledBlocks = (int)Math.Round(clampedScore / 100.0 * width);
        filledBlocks = Math.Max(0, Math.Min(width, filledBlocks));
        
        var emptyBlocks = width - filledBlocks;
        var bar = new string('█', filledBlocks) + new string('░', emptyBlocks);
        
        return includePercentage ? $"{bar} {clampedScore:F1}%" : bar;
    }

    /// <summary>
    /// Generates a colored visual progress bar with emoji indicator.
    /// </summary>
    /// <param name="score">Score from 0 to 100.</param>
    /// <param name="width">Number of characters in the bar (default 20).</param>
    /// <returns>Colored bar like "🟢 ████████████████████".</returns>
    public static string GenerateColoredScoreBar(double score, int width = 20)
    {
        var clampedScore = Math.Max(0, Math.Min(100, score));
        
        var filledBars = (int)(clampedScore / 100.0 * width);
        var emptyBars = width - filledBars;
        
        var color = score switch
        {
            >= 90 => "🟢",
            >= 80 => "🟡",
            >= 70 => "🟠",
            _ => "🔴"
        };
        
        return $"{color} {new string('█', filledBars)}{new string('░', emptyBars)}";
    }

    /// <summary>
    /// Gets all control family codes.
    /// </summary>
    /// <returns>Collection of valid control family codes.</returns>
    public static IReadOnlyCollection<string> GetAllControlFamilyCodes()
    {
        return ControlFamilyNames.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// Validates if a control family code is valid.
    /// </summary>
    /// <param name="familyCode">Control family code to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValidControlFamily(string familyCode)
    {
        if (string.IsNullOrWhiteSpace(familyCode))
            return false;

        return ControlFamilyNames.ContainsKey(familyCode.Trim());
    }
}
