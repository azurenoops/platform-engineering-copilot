namespace Platform.Engineering.Copilot.Core.Configuration;

/// <summary>
/// Configuration for NIST control family definitions.
/// Can be customized via appsettings.json.
/// </summary>
public class ControlFamilyConfiguration
{
    public const string SectionName = "ControlFamilies";

    /// <summary>
    /// Control family definitions with codes and descriptions.
    /// </summary>
    public Dictionary<string, ControlFamilyDefinition> Families { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AC"] = new() { Code = "AC", Name = "Access Control", Description = "Policies and procedures for limiting access to systems and information" },
        ["AT"] = new() { Code = "AT", Name = "Awareness and Training", Description = "Security awareness training and role-based training requirements" },
        ["AU"] = new() { Code = "AU", Name = "Audit and Accountability", Description = "Audit logging, review, analysis, and reporting requirements" },
        ["CA"] = new() { Code = "CA", Name = "Security Assessment and Authorization", Description = "Security assessments, authorizations, and continuous monitoring" },
        ["CM"] = new() { Code = "CM", Name = "Configuration Management", Description = "Baseline configurations, change control, and configuration settings" },
        ["CP"] = new() { Code = "CP", Name = "Contingency Planning", Description = "Business continuity, disaster recovery, and backup procedures" },
        ["IA"] = new() { Code = "IA", Name = "Identification and Authentication", Description = "User identification, authentication, and credential management" },
        ["IR"] = new() { Code = "IR", Name = "Incident Response", Description = "Incident handling, monitoring, reporting, and assistance" },
        ["MA"] = new() { Code = "MA", Name = "Maintenance", Description = "System maintenance, tools, and personnel requirements" },
        ["MP"] = new() { Code = "MP", Name = "Media Protection", Description = "Media access, marking, storage, transport, and sanitization" },
        ["PE"] = new() { Code = "PE", Name = "Physical and Environmental Protection", Description = "Physical access, monitoring, and environmental controls" },
        ["PL"] = new() { Code = "PL", Name = "Planning", Description = "Security planning, rules of behavior, and privacy impact" },
        ["PM"] = new() { Code = "PM", Name = "Program Management", Description = "Information security program management and oversight" },
        ["PS"] = new() { Code = "PS", Name = "Personnel Security", Description = "Personnel screening, termination, transfer, and agreements" },
        ["RA"] = new() { Code = "RA", Name = "Risk Assessment", Description = "Risk assessment, vulnerability scanning, and threat identification" },
        ["SA"] = new() { Code = "SA", Name = "System and Services Acquisition", Description = "Acquisition processes, supply chain, and developer security" },
        ["SC"] = new() { Code = "SC", Name = "System and Communications Protection", Description = "Network protection, cryptography, and transmission integrity" },
        ["SI"] = new() { Code = "SI", Name = "System and Information Integrity", Description = "Flaw remediation, malware protection, and system monitoring" }
    };

    /// <summary>
    /// Gets the name for a control family code.
    /// </summary>
    public string GetFamilyName(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return code;

        return Families.TryGetValue(code.Trim(), out var family) ? family.Name : code;
    }

    /// <summary>
    /// Gets the description for a control family code.
    /// </summary>
    public string? GetFamilyDescription(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        return Families.TryGetValue(code.Trim(), out var family) ? family.Description : null;
    }

    /// <summary>
    /// Checks if a control family code is valid.
    /// </summary>
    public bool IsValidFamily(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        return Families.ContainsKey(code.Trim());
    }

    /// <summary>
    /// Gets all configured family codes.
    /// </summary>
    public IEnumerable<string> GetAllCodes() => Families.Keys;
}

/// <summary>
/// Definition for a single control family.
/// </summary>
public class ControlFamilyDefinition
{
    /// <summary>
    /// Two-letter control family code (e.g., "AC", "AU").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the control family.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the control family scope.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this control family is enabled for assessments.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Priority order for this family (lower = higher priority).
    /// </summary>
    public int Priority { get; set; } = 100;
}
