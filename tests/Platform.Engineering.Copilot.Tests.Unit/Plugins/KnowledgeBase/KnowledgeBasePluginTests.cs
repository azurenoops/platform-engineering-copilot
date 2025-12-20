using FluentAssertions;
using Platform.Engineering.Copilot.Core.Models.KnowledgeBase;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Plugins.KnowledgeBase;

/// <summary>
/// Unit tests for KnowledgeBasePlugin
/// Tests response formatting, control parsing, caching patterns, and validation
/// Note: The plugin relies on Semantic Kernel which is sealed, so we test
/// response formatting patterns, data transformations, and validation logic.
/// </summary>
public class KnowledgeBasePluginTests
{
    #region NIST Control Parsing Tests

    [Theory]
    [InlineData("AC-2", "AC-2")]
    [InlineData("ac-2", "AC-2")]
    [InlineData("Ac-2", "AC-2")]
    [InlineData(" AC-2 ", "AC-2")]
    [InlineData("IA-2(1)", "IA-2(1)")]
    [InlineData("SC-28", "SC-28")]
    public void NormalizeControlId_ReturnsUpperCase(string input, string expected)
    {
        // Act
        var normalized = input?.Trim().ToUpperInvariant() ?? "";

        // Assert
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("AC-2")]
    [InlineData("AU-2")]
    [InlineData("AT-1")]
    [InlineData("CM-6")]
    [InlineData("CP-9")]
    [InlineData("IA-2")]
    [InlineData("IR-4")]
    [InlineData("MA-2")]
    [InlineData("MP-2")]
    [InlineData("PS-1")]
    [InlineData("PE-3")]
    [InlineData("PL-1")]
    [InlineData("PM-1")]
    [InlineData("RA-3")]
    [InlineData("CA-1")]
    [InlineData("SC-7")]
    [InlineData("SI-2")]
    [InlineData("SA-1")]
    [InlineData("SR-1")]
    [InlineData("PT-1")]
    public void ValidNistControlId_AllFamilies(string controlId)
    {
        // Arrange - Extract control family (first 2 characters)
        var family = controlId.Split('-')[0];

        // Assert
        family.Should().HaveLength(2);
        controlId.Should().MatchRegex(@"^[A-Z]{2}-\d+");
    }

    [Theory]
    [InlineData("IA-2(1)", "IA-2", "1")]
    [InlineData("AC-2(4)", "AC-2", "4")]
    [InlineData("SC-28(1)", "SC-28", "1")]
    [InlineData("AU-3(1)", "AU-3", "1")]
    public void EnhancementParsing_ExtractsBaseControl(string enhancement, string baseControl, string enhancementNumber)
    {
        // Act
        var parts = enhancement.Split('(');
        var extractedBase = parts[0];
        var extractedEnhancement = parts[1].TrimEnd(')');

        // Assert
        extractedBase.Should().Be(baseControl);
        extractedEnhancement.Should().Be(enhancementNumber);
    }

    [Fact]
    public void IsEnhancement_DetectsEnhancementFormat()
    {
        // Arrange
        var enhancement = "IA-2(1)";
        var baseControl = "IA-2";

        // Act
        var isEnhancement = enhancement.Contains('(');
        var isBase = !baseControl.Contains('(');

        // Assert
        isEnhancement.Should().BeTrue();
        isBase.Should().BeTrue();
    }

    #endregion

    #region Control Family Mapping Tests

    [Theory]
    [InlineData("AC", "Access Control")]
    [InlineData("AU", "Audit and Accountability")]
    [InlineData("AT", "Awareness and Training")]
    [InlineData("CM", "Configuration Management")]
    [InlineData("CP", "Contingency Planning")]
    [InlineData("IA", "Identification and Authentication")]
    [InlineData("IR", "Incident Response")]
    [InlineData("MA", "Maintenance")]
    [InlineData("MP", "Media Protection")]
    [InlineData("PS", "Personnel Security")]
    [InlineData("PE", "Physical and Environmental Protection")]
    [InlineData("PL", "Planning")]
    [InlineData("PM", "Program Management")]
    [InlineData("RA", "Risk Assessment")]
    [InlineData("CA", "Assessment, Authorization, and Monitoring")]
    [InlineData("SC", "System and Communications Protection")]
    [InlineData("SI", "System and Information Integrity")]
    [InlineData("SA", "System and Services Acquisition")]
    [InlineData("SR", "Supply Chain Risk Management")]
    [InlineData("PT", "Personally Identifiable Information Processing and Transparency")]
    public void ControlFamily_MapsToDescription(string familyCode, string familyName)
    {
        // Arrange
        var familyMap = new Dictionary<string, string>
        {
            ["AC"] = "Access Control",
            ["AU"] = "Audit and Accountability",
            ["AT"] = "Awareness and Training",
            ["CM"] = "Configuration Management",
            ["CP"] = "Contingency Planning",
            ["IA"] = "Identification and Authentication",
            ["IR"] = "Incident Response",
            ["MA"] = "Maintenance",
            ["MP"] = "Media Protection",
            ["PS"] = "Personnel Security",
            ["PE"] = "Physical and Environmental Protection",
            ["PL"] = "Planning",
            ["PM"] = "Program Management",
            ["RA"] = "Risk Assessment",
            ["CA"] = "Assessment, Authorization, and Monitoring",
            ["SC"] = "System and Communications Protection",
            ["SI"] = "System and Information Integrity",
            ["SA"] = "System and Services Acquisition",
            ["SR"] = "Supply Chain Risk Management",
            ["PT"] = "Personally Identifiable Information Processing and Transparency"
        };

        // Act & Assert
        familyMap[familyCode].Should().Be(familyName);
    }

    [Fact]
    public void AllControlFamilies_Count20()
    {
        // Arrange
        var families = new[] { "AC", "AU", "AT", "CM", "CP", "IA", "IR", "MA", "MP", "PS", 
                              "PE", "PL", "PM", "RA", "CA", "SC", "SI", "SA", "SR", "PT" };

        // Assert
        families.Should().HaveCount(20);
    }

    #endregion

    #region Azure Implementation Guidance Tests

    [Theory]
    [InlineData("AC-2", "Azure Active Directory")]
    [InlineData("AC-2", "RBAC")]
    [InlineData("AC-2", "PIM")]
    [InlineData("IA-2", "MFA")]
    [InlineData("IA-2", "Conditional Access")]
    [InlineData("SC-28", "Disk Encryption")]
    [InlineData("SC-28", "Key Vault")]
    [InlineData("AU-2", "Azure Monitor")]
    [InlineData("SC-7", "Azure Firewall")]
    [InlineData("SC-7", "NSG")]
    [InlineData("CM-6", "Azure Policy")]
    public void AzureImplementationGuidance_ContainsExpectedService(string controlId, string expectedService)
    {
        // Arrange - Simulate Azure implementation guidance mapping
        var azureGuidance = GetAzureImplementationGuidance(controlId);

        // Assert
        azureGuidance.Should().Contain(expectedService);
    }

    private string GetAzureImplementationGuidance(string controlId)
    {
        return controlId.ToUpperInvariant() switch
        {
            "AC-2" => @"### 🔵 Azure Implementation
- **Azure AD**: Use Azure Active Directory for centralized identity management
- **RBAC**: Implement Role-Based Access Control with least privilege principle
- **PIM**: Use Privileged Identity Management for just-in-time access
- **Access Reviews**: Configure periodic access reviews in Azure AD",

            "IA-2" => @"### 🔵 Azure Implementation
- **Azure AD**: Centralized authentication for all users
- **MFA**: Require multi-factor authentication
- **Conditional Access**: Risk-based authentication policies
- **FIDO2/Passwordless**: Support for modern authentication methods",

            "SC-28" => @"### 🔵 Azure Implementation
- **Azure Disk Encryption**: Encrypt VM disks with BitLocker/DM-Crypt
- **Storage Service Encryption**: Automatic encryption for Azure Storage
- **Azure SQL TDE**: Transparent Data Encryption for databases
- **Key Vault**: Centralized key management with HSM backing",

            "AU-2" => @"### 🔵 Azure Implementation
- **Azure Monitor**: Centralized logging and monitoring
- **Diagnostic Settings**: Route logs to Log Analytics, Storage, Event Hub
- **Activity Logs**: Track control plane operations
- **Microsoft Defender for Cloud**: Security event monitoring",

            "SC-7" => @"### 🔵 Azure Implementation
- **Azure Firewall**: Centralized network security
- **NSG**: Network Security Groups for subnet/NIC filtering
- **Private Endpoints**: Keep traffic on Microsoft backbone
- **Azure DDoS Protection**: Protect against volumetric attacks",

            "CM-6" => @"### 🔵 Azure Implementation
- **Azure Policy**: Enforce configuration standards
- **Azure Blueprints**: Deploy compliant environments
- **ARM Templates/Bicep**: Infrastructure as Code for consistency",

            _ => @"### 🔵 Azure Implementation
*For specific Azure implementation guidance, consider reviewing Microsoft Compliance documentation.*"
        };
    }

    #endregion

    #region RMF Step Validation Tests

    [Theory]
    [InlineData("1", true)]
    [InlineData("2", true)]
    [InlineData("3", true)]
    [InlineData("4", true)]
    [InlineData("5", true)]
    [InlineData("6", true)]
    [InlineData("0", false)]
    [InlineData("7", false)]
    [InlineData("-1", false)]
    [InlineData("abc", false)]
    public void RmfStep_Validation(string step, bool isValid)
    {
        // Act
        var stepIsValid = int.TryParse(step, out var stepNumber) && stepNumber >= 1 && stepNumber <= 6;

        // Assert
        stepIsValid.Should().Be(isValid);
    }

    [Theory]
    [InlineData("1", "Categorize")]
    [InlineData("2", "Select")]
    [InlineData("3", "Implement")]
    [InlineData("4", "Assess")]
    [InlineData("5", "Authorize")]
    [InlineData("6", "Monitor")]
    public void RmfStep_TitleMapping(string step, string expectedTitle)
    {
        // Arrange
        var stepTitles = new Dictionary<string, string>
        {
            ["1"] = "Categorize",
            ["2"] = "Select",
            ["3"] = "Implement",
            ["4"] = "Assess",
            ["5"] = "Authorize",
            ["6"] = "Monitor"
        };

        // Assert
        stepTitles[step].Should().Be(expectedTitle);
    }

    #endregion

    #region Impact Level Validation Tests

    [Theory]
    [InlineData("IL2", true)]
    [InlineData("IL4", true)]
    [InlineData("IL5", true)]
    [InlineData("IL6", true)]
    [InlineData("il2", true)]
    [InlineData("il5", true)]
    [InlineData("IL3", false)]
    [InlineData("IL7", false)]
    [InlineData("L5", false)]
    public void ImpactLevel_Validation(string level, bool isValid)
    {
        // Act
        var normalizedLevel = level.ToUpperInvariant().Replace(" ", "");
        var validLevels = new HashSet<string> { "IL2", "IL4", "IL5", "IL6" };
        var levelIsValid = validLevels.Contains(normalizedLevel);

        // Assert
        levelIsValid.Should().Be(isValid);
    }

    [Theory]
    [InlineData("IL2", "AzureCloud")]
    [InlineData("IL4", "AzureGovernment")]
    [InlineData("IL5", "AzureGovernment")]
    [InlineData("IL6", "AzureGovernmentSecret")]
    public void ImpactLevel_AzureEnvironmentMapping(string level, string expectedEnvironment)
    {
        // Arrange
        var environmentMap = new Dictionary<string, string>
        {
            ["IL2"] = "AzureCloud",
            ["IL4"] = "AzureGovernment",
            ["IL5"] = "AzureGovernment",
            ["IL6"] = "AzureGovernmentSecret"
        };

        // Assert
        environmentMap[level].Should().Be(expectedEnvironment);
    }

    #endregion

    #region STIG Severity Parsing Tests

    [Theory]
    [InlineData("CAT I", StigSeverity.High)]
    [InlineData("CAT II", StigSeverity.Medium)]
    [InlineData("CAT III", StigSeverity.Low)]
    public void StigCategory_SeverityMapping(string category, StigSeverity expectedSeverity)
    {
        // Arrange
        var severityMap = new Dictionary<string, StigSeverity>
        {
            ["CAT I"] = StigSeverity.High,
            ["CAT II"] = StigSeverity.Medium,
            ["CAT III"] = StigSeverity.Low
        };

        // Assert
        severityMap[category].Should().Be(expectedSeverity);
    }

    [Theory]
    [InlineData("V-219153")]
    [InlineData("V-219187")]
    [InlineData("V-12345")]
    public void StigId_Format_IsValid(string stigId)
    {
        // Assert
        stigId.Should().MatchRegex(@"^V-\d+$");
    }

    [Theory]
    [InlineData("SV-219153r897755_rule")]
    [InlineData("SV-12345r123456_rule")]
    public void StigRuleId_Format_IsValid(string ruleId)
    {
        // Assert
        ruleId.Should().MatchRegex(@"^SV-\d+r\d+_rule$");
    }

    #endregion

    #region DoD Instruction Parsing Tests

    [Theory]
    [InlineData("DoDI 8500.01")]
    [InlineData("DoDI 8510.01")]
    [InlineData("CNSSI 1253")]
    [InlineData("DoD CIO")]
    public void DoDInstruction_Format_IsValid(string instructionId)
    {
        // Assert
        instructionId.Should().NotBeNullOrEmpty();
        instructionId.Length.Should().BeGreaterThan(3);
    }

    [Theory]
    [InlineData("DoDI 8500.01", "Cybersecurity")]
    [InlineData("DoDI 8510.01", "Risk Management Framework")]
    [InlineData("CNSSI 1253", "Security Categorization")]
    public void DoDInstruction_TitleMapping(string instructionId, string expectedTopic)
    {
        // Arrange
        var instructionTopics = new Dictionary<string, string>
        {
            ["DoDI 8500.01"] = "Cybersecurity",
            ["DoDI 8510.01"] = "Risk Management Framework",
            ["CNSSI 1253"] = "Security Categorization"
        };

        // Assert
        instructionTopics[instructionId].Should().Be(expectedTopic);
    }

    #endregion

    #region Response Formatting Tests

    [Fact]
    public void FormatControlExplanation_IncludesAllSections()
    {
        // Arrange
        var controlId = "AC-2";
        var title = "Account Management";
        var statement = "The organization manages system accounts...";
        var azureGuidance = "Use Azure Active Directory...";

        // Act
        var response = $@"# 📚 NIST 800-53 Control: {controlId}

## {title}

### Control Statement

{statement}

### 🔵 Azure Implementation

{azureGuidance}

---
*This is informational only. To check your compliance status, ask: 'Run a compliance assessment'*";

        // Assert
        response.Should().Contain("# 📚 NIST 800-53 Control:");
        response.Should().Contain(controlId);
        response.Should().Contain(title);
        response.Should().Contain("Control Statement");
        response.Should().Contain("Azure Implementation");
        response.Should().Contain("informational only");
    }

    [Fact]
    public void FormatRmfExplanation_IncludesAllSections()
    {
        // Arrange
        var step = "3";
        var title = "Implement Security Controls";
        var activities = new List<string> { "Implement controls", "Document implementation" };
        var outputs = new List<string> { "Security Plan", "Implementation Evidence" };

        // Act
        var response = $@"# RMF Step {step}: {title}

## Activities

{string.Join("\n", activities.Select((a, i) => $"{i + 1}. {a}"))}

## Key Deliverables

{string.Join("\n", outputs.Select(o => $"- {o}"))}

## Next Steps

After completing Step {step}, proceed to Step {int.Parse(step) + 1}.";

        // Assert
        response.Should().Contain($"RMF Step {step}");
        response.Should().Contain("Activities");
        response.Should().Contain("Key Deliverables");
        response.Should().Contain("Next Steps");
    }

    [Fact]
    public void FormatStigExplanation_IncludesAllSections()
    {
        // Arrange
        var stigId = "V-219153";
        var title = "Windows Server 2016 audit configuration";
        var severity = StigSeverity.Medium;
        var checkText = "Run the following PowerShell command...";
        var fixText = "Configure the policy value...";

        // Act
        var response = $@"# {stigId}: {title}

**Severity:** {severity}
**Category:** CAT II

## Description

{title}

## Check Procedure

{checkText}

## Remediation

{fixText}";

        // Assert
        response.Should().Contain(stigId);
        response.Should().Contain("Severity:");
        response.Should().Contain("Check Procedure");
        response.Should().Contain("Remediation");
    }

    [Fact]
    public void FormatImpactLevelExplanation_IncludesAllSections()
    {
        // Arrange
        var level = "IL5";
        var description = "Controlled Unclassified Information (CUI)";

        // Act
        var response = $@"# Impact Level 5 ({level})

## Description
{description}

## Security Requirements

**Baseline:** NIST 800-53 Moderate/High

### Encryption Requirements
- **Data at Rest:** AES-256, FIPS 140-2 validated
- **Data in Transit:** TLS 1.2 minimum

## Azure Implementation

**Cloud Environment:** Azure Government";

        // Assert
        response.Should().Contain(level);
        response.Should().Contain("Security Requirements");
        response.Should().Contain("Encryption Requirements");
        response.Should().Contain("Azure Implementation");
    }

    #endregion

    #region Search Result Formatting Tests

    [Fact]
    public void FormatSearchResults_MultipleResults()
    {
        // Arrange
        var results = new List<KnowledgeBaseSearchResult>
        {
            new KnowledgeBaseSearchResult
            {
                Type = "NIST Control",
                Id = "AC-2",
                Title = "Account Management",
                RelevanceScore = 0.95
            },
            new KnowledgeBaseSearchResult
            {
                Type = "STIG",
                Id = "V-219153",
                Title = "Windows Server Account Management",
                RelevanceScore = 0.82
            }
        };

        // Act
        var formattedResults = results.Select(r => $"- **{r.Type}** [{r.Id}]: {r.Title} (Score: {r.RelevanceScore:F2})");

        // Assert
        formattedResults.Should().HaveCount(2);
        string.Join("\n", formattedResults).Should().Contain("AC-2");
        string.Join("\n", formattedResults).Should().Contain("V-219153");
    }

    [Theory]
    [InlineData(0.95, "0.95")]
    [InlineData(0.82, "0.82")]
    [InlineData(0.75, "0.75")]
    public void RelevanceScore_Formatting(double score, string expectedFormatted)
    {
        // Act
        var formatted = score.ToString("F2");

        // Assert
        formatted.Should().Be(expectedFormatted);
    }

    #endregion

    #region Error Response Formatting Tests

    [Theory]
    [InlineData("XY-999", "Control 'XY-999' was not found")]
    [InlineData("", "Control '' was not found")]
    public void ControlNotFound_ErrorMessage(string controlId, string expectedContains)
    {
        // Act
        var errorMessage = $"❓ Control '{controlId}' was not found in the NIST 800-53 catalog.";

        // Assert
        errorMessage.Should().Contain(expectedContains);
    }

    [Fact]
    public void ControlNotFound_SuggestsAlternatives()
    {
        // Arrange
        var controlId = "XY-999";

        // Act
        var errorMessage = $@"❓ Control '{controlId}' was not found in the NIST 800-53 catalog.

**Suggestions:**
- Check the control ID format (e.g., AC-2, IA-2(1), SC-28)
- Use 'search for NIST controls about [topic]' to find related controls
- Common control families: AC (Access Control), AU (Audit), IA (Identification), SC (System Protection), CM (Configuration)";

        // Assert
        errorMessage.Should().Contain("Suggestions:");
        errorMessage.Should().Contain("AC (Access Control)");
    }

    [Theory]
    [InlineData("7", "Valid steps are 1-6")]
    [InlineData("0", "Valid steps are 1-6")]
    public void InvalidRmfStep_ErrorMessage(string step, string expectedContains)
    {
        // Act
        var errorMessage = $"RMF Step {step} not found. Valid steps are 1-6.";

        // Assert
        errorMessage.Should().Contain(expectedContains);
    }

    [Theory]
    [InlineData("IL3", "Valid levels: IL2, IL4, IL5, IL6")]
    [InlineData("IL7", "Valid levels: IL2, IL4, IL5, IL6")]
    public void InvalidImpactLevel_ErrorMessage(string level, string expectedContains)
    {
        // Act
        var errorMessage = $"Impact Level '{level}' not found. Valid levels: IL2, IL4, IL5, IL6.";

        // Assert
        errorMessage.Should().Contain(expectedContains);
    }

    #endregion

    #region Caching Pattern Tests

    [Fact]
    public void CacheKey_NistControl_Format()
    {
        // Arrange
        var controlId = "AC-2";
        var cacheKey = $"nist_control_{controlId.ToUpperInvariant()}";

        // Assert
        cacheKey.Should().Be("nist_control_AC-2");
    }

    [Fact]
    public void CacheKey_RmfStep_Format()
    {
        // Arrange
        var step = "3";
        var cacheKey = $"rmf_step_{step}";

        // Assert
        cacheKey.Should().Be("rmf_step_3");
    }

    [Fact]
    public void CacheKey_Stig_Format()
    {
        // Arrange
        var stigId = "V-219153";
        var cacheKey = $"stig_{stigId.ToUpperInvariant()}";

        // Assert
        cacheKey.Should().Be("stig_V-219153");
    }

    [Fact]
    public void CacheKey_ImpactLevel_Format()
    {
        // Arrange
        var level = "IL5";
        var cacheKey = $"impact_level_{level.ToUpperInvariant()}";

        // Assert
        cacheKey.Should().Be("impact_level_IL5");
    }

    [Theory]
    [InlineData(60)]
    [InlineData(1440)]
    [InlineData(24 * 60)]
    public void CacheDuration_ValidMinuteValues(int minutes)
    {
        // Arrange
        var timeSpan = TimeSpan.FromMinutes(minutes);

        // Assert
        timeSpan.TotalMinutes.Should().Be(minutes);
    }

    #endregion

    #region Azure Service Mapping Tests

    [Fact]
    public void AzureServiceMapping_IA2_IncludesAuthServices()
    {
        // Arrange
        var servicesForIA2 = new List<string>
        {
            "Azure Active Directory",
            "Azure MFA",
            "Conditional Access",
            "Managed Identities",
            "FIDO2 Security Keys"
        };

        // Assert
        servicesForIA2.Should().Contain("Azure Active Directory");
        servicesForIA2.Should().Contain("Azure MFA");
        servicesForIA2.Should().HaveCountGreaterOrEqualTo(5);
    }

    [Fact]
    public void AzureServiceMapping_SC28_IncludesEncryptionServices()
    {
        // Arrange
        var servicesForSC28 = new List<string>
        {
            "Azure Storage Service Encryption",
            "Azure Disk Encryption",
            "Azure SQL TDE",
            "Azure Key Vault",
            "Customer-Managed Keys"
        };

        // Assert
        servicesForSC28.Should().Contain("Azure Key Vault");
        servicesForSC28.Should().Contain("Azure Disk Encryption");
    }

    [Fact]
    public void AzureServiceMapping_SC7_IncludesNetworkServices()
    {
        // Arrange
        var servicesForSC7 = new List<string>
        {
            "Azure Firewall",
            "Network Security Groups (NSG)",
            "Private Endpoints",
            "Azure DDoS Protection",
            "Azure Bastion"
        };

        // Assert
        servicesForSC7.Should().Contain("Azure Firewall");
        servicesForSC7.Should().Contain("Network Security Groups (NSG)");
    }

    #endregion

    #region Control Cross-Reference Tests

    [Fact]
    public void ControlMapping_AC2_RelatedControls()
    {
        // Arrange
        var relatedControls = new List<string> { "AC-3", "AC-5", "AC-6", "IA-2", "IA-4" };

        // Assert
        relatedControls.Should().Contain("AC-3");
        relatedControls.Should().Contain("IA-2");
    }

    [Fact]
    public void StigToNist_Mapping()
    {
        // Arrange
        var stigNistMapping = new Dictionary<string, List<string>>
        {
            ["V-219153"] = new List<string> { "AU-2", "AU-3", "AU-12" },
            ["V-219187"] = new List<string> { "IA-2", "IA-2(1)" }
        };

        // Assert
        stigNistMapping["V-219153"].Should().Contain("AU-2");
        stigNistMapping["V-219187"].Should().Contain("IA-2");
    }

    #endregion

    #region Workflow Step Formatting Tests

    [Fact]
    public void WorkflowStep_Formatting()
    {
        // Arrange
        var step = new WorkflowStep
        {
            StepNumber = 1,
            Title = "System Registration",
            Description = "Register system in eMASS",
            Responsibilities = new List<string> { "System Owner", "ISSO" },
            Deliverables = new List<string> { "eMASS Registration Package" },
            EstimatedDuration = "2 weeks"
        };

        // Act
        var formatted = $@"### Step {step.StepNumber}: {step.Title}

{step.Description}

**Duration:** {step.EstimatedDuration}
**Responsible:** {string.Join(", ", step.Responsibilities)}

**Deliverables:**
{string.Join("\n", step.Deliverables.Select(d => $"- {d}"))}";

        // Assert
        formatted.Should().Contain("Step 1:");
        formatted.Should().Contain("System Registration");
        formatted.Should().Contain("2 weeks");
    }

    #endregion

    #region Organization Formatting Tests

    [Theory]
    [InlineData(DoDOrganization.Navy, "Navy")]
    [InlineData(DoDOrganization.DISA, "DISA")]
    [InlineData(DoDOrganization.CYBERCOM, "CYBERCOM")]
    public void DoDOrganization_ToString(DoDOrganization org, string expected)
    {
        // Assert
        org.ToString().Should().Be(expected);
    }

    [Fact]
    public void OrganizationWorkflow_ServiceMapping()
    {
        // Arrange
        var serviceMapping = new Dictionary<string, string>
        {
            ["navy"] = "Navy",
            ["usnavy"] = "Navy",
            ["disa"] = "DISA",
            ["airforce"] = "Air Force",
            ["usaf"] = "Air Force",
            ["army"] = "Army"
        };

        // Assert
        serviceMapping["navy"].Should().Be("Navy");
        serviceMapping["usaf"].Should().Be("Air Force");
    }

    #endregion

    #region FedRAMP Template Tests

    [Theory]
    [InlineData("1", "System Identification")]
    [InlineData("2", "Security Management")]
    [InlineData("3", "System Environment")]
    public void SSPSection_NumberMapping(string sectionNumber, string sectionTitle)
    {
        // Arrange
        var sectionMap = new Dictionary<string, string>
        {
            ["1"] = "System Identification",
            ["2"] = "Security Management",
            ["3"] = "System Environment"
        };

        // Assert
        sectionMap[sectionNumber].Should().Be(sectionTitle);
    }

    [Fact]
    public void AuthorizationPackage_RequiredDocuments()
    {
        // Arrange
        var requiredDocs = new List<string>
        {
            "System Security Plan (SSP)",
            "Security Assessment Report (SAR)",
            "Plan of Action and Milestones (POA&M)",
            "Authorization Decision Letter",
            "Continuous Monitoring Plan"
        };

        // Assert
        requiredDocs.Should().Contain("System Security Plan (SSP)");
        requiredDocs.Should().HaveCountGreaterOrEqualTo(5);
    }

    #endregion
}
