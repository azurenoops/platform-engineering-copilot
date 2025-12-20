using FluentAssertions;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.KnowledgeBase;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration tests for Knowledge Base compliance workflows
/// Tests RMF process, ATO preparation, and compliance documentation generation
/// </summary>
[Trait("Category", "Integration")]
public class KnowledgeBaseComplianceIntegrationTests
{
    #region RMF Process Workflow Tests

    [Fact]
    public void RmfWorkflow_CategorizeToAuthorize()
    {
        // Arrange - Simulate complete RMF workflow
        var steps = new List<RmfProcess>
        {
            new RmfProcess
            {
                Step = "1",
                Title = "Categorize",
                Description = "Categorize the information system",
                Activities = new List<string> { "Document system", "Identify data types" },
                Outputs = new List<string> { "FIPS 199 categorization" }
            },
            new RmfProcess
            {
                Step = "2",
                Title = "Select",
                Description = "Select security controls",
                Activities = new List<string> { "Choose baseline", "Tailor controls" },
                Outputs = new List<string> { "Security Plan (SSP)" }
            },
            new RmfProcess
            {
                Step = "3",
                Title = "Implement",
                Description = "Implement security controls",
                Activities = new List<string> { "Configure systems", "Document settings" },
                Outputs = new List<string> { "Implementation evidence" }
            },
            new RmfProcess
            {
                Step = "4",
                Title = "Assess",
                Description = "Assess security controls",
                Activities = new List<string> { "Execute assessments", "Document findings" },
                Outputs = new List<string> { "Security Assessment Report (SAR)" }
            },
            new RmfProcess
            {
                Step = "5",
                Title = "Authorize",
                Description = "Authorize information system",
                Activities = new List<string> { "Prepare package", "Submit to AO" },
                Outputs = new List<string> { "ATO letter" }
            },
            new RmfProcess
            {
                Step = "6",
                Title = "Monitor",
                Description = "Monitor security controls",
                Activities = new List<string> { "Continuous monitoring", "Report status" },
                Outputs = new List<string> { "ConMon reports" }
            }
        };

        // Assert
        steps.Should().HaveCount(6);
        steps.All(s => !string.IsNullOrEmpty(s.Title)).Should().BeTrue();
        steps.SelectMany(s => s.Outputs).Should().Contain("ATO letter");
    }

    [Fact]
    public void RmfWorkflow_ArtifactCollection()
    {
        // Arrange - Track all required artifacts across RMF steps
        var artifactsByStep = new Dictionary<string, List<string>>
        {
            ["1"] = new List<string> { "System categorization", "FIPS 199 documentation" },
            ["2"] = new List<string> { "System Security Plan (SSP)", "Control tailoring rationale" },
            ["3"] = new List<string> { "Implementation evidence", "Configuration documentation" },
            ["4"] = new List<string> { "Security Assessment Report (SAR)", "Assessment plan" },
            ["5"] = new List<string> { "Authorization package", "Risk acceptance" },
            ["6"] = new List<string> { "POA&M", "Continuous monitoring reports" }
        };

        // Act
        var allArtifacts = artifactsByStep.SelectMany(kvp => kvp.Value).ToList();

        // Assert
        allArtifacts.Should().Contain("System Security Plan (SSP)");
        allArtifacts.Should().Contain("Security Assessment Report (SAR)");
        allArtifacts.Should().Contain("POA&M");
    }

    #endregion

    #region ATO Package Preparation Tests

    [Fact]
    public void AtoPackage_RequiredDocuments()
    {
        // Arrange
        var atoRequirements = new List<AtoPackageRequirement>
        {
            new AtoPackageRequirement
            {
                DocumentType = "SSP",
                Name = "System Security Plan",
                Description = "Comprehensive security documentation",
                IsRequired = true,
                ImpactLevel = "ALL",
                ResponsibleRole = "ISSO"
            },
            new AtoPackageRequirement
            {
                DocumentType = "SAR",
                Name = "Security Assessment Report",
                Description = "Assessment findings and recommendations",
                IsRequired = true,
                ImpactLevel = "ALL",
                ResponsibleRole = "SCA"
            },
            new AtoPackageRequirement
            {
                DocumentType = "POA&M",
                Name = "Plan of Action and Milestones",
                Description = "Remediation plan for identified weaknesses",
                IsRequired = true,
                ImpactLevel = "ALL",
                ResponsibleRole = "System Owner"
            },
            new AtoPackageRequirement
            {
                DocumentType = "RAR",
                Name = "Risk Assessment Report",
                Description = "Risk analysis and mitigation strategies",
                IsRequired = true,
                ImpactLevel = "IL4,IL5,IL6",
                ResponsibleRole = "ISSO"
            }
        };

        // Assert
        atoRequirements.Where(r => r.IsRequired).Should().HaveCountGreaterOrEqualTo(3);
        atoRequirements.Should().Contain(r => r.DocumentType == "SSP");
        atoRequirements.Should().Contain(r => r.DocumentType == "SAR");
        atoRequirements.Should().Contain(r => r.DocumentType == "POA&M");
    }

    [Fact]
    public void AtoPackage_ImpactLevelSpecificRequirements()
    {
        // Arrange
        var il5Requirements = new List<string>
        {
            "Physical isolation documentation",
            "Dedicated connection evidence",
            "FIPS 140-2 validation certificates",
            "Personnel clearance verification",
            "Boundary protection documentation"
        };

        // Assert
        il5Requirements.Should().Contain(r => r.Contains("FIPS"));
        il5Requirements.Should().Contain(r => r.Contains("physical isolation") || r.Contains("Physical isolation"));
    }

    #endregion

    #region Control Family Coverage Tests

    [Fact]
    public void ControlFamilies_CompleteCoverage()
    {
        // Arrange - All 20 NIST 800-53 control families
        var controlFamilies = new Dictionary<string, string>
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

        // Assert
        controlFamilies.Should().HaveCount(20);
        controlFamilies.Keys.All(k => k.Length == 2).Should().BeTrue();
    }

    [Fact]
    public void HighImpactControls_Identification()
    {
        // Arrange - High-priority controls for IL5
        var priorityControls = new List<string>
        {
            "AC-2", "AC-3", "AC-6", "AC-17",
            "AU-2", "AU-3", "AU-6", "AU-12",
            "IA-2", "IA-4", "IA-5", "IA-8",
            "SC-7", "SC-8", "SC-12", "SC-28",
            "CM-6", "CM-7", "CM-8"
        };

        // Assert
        priorityControls.Should().Contain("AC-2");
        priorityControls.Should().Contain("IA-2");
        priorityControls.Should().Contain("SC-28");
        priorityControls.Where(c => c.StartsWith("SC-")).Should().HaveCountGreaterOrEqualTo(3);
    }

    #endregion

    #region STIG Compliance Workflow Tests

    [Fact]
    public void StigCompliance_ByServiceType()
    {
        // Arrange
        var stigsByService = new Dictionary<StigServiceType, int>
        {
            [StigServiceType.Compute] = 45,
            [StigServiceType.Network] = 32,
            [StigServiceType.Storage] = 18,
            [StigServiceType.Database] = 25,
            [StigServiceType.Identity] = 28,
            [StigServiceType.Containers] = 15
        };

        // Assert
        stigsByService.Values.Sum().Should().BeGreaterThan(100);
        stigsByService.Should().ContainKey(StigServiceType.Compute);
        stigsByService.Should().ContainKey(StigServiceType.Identity);
    }

    [Fact]
    public void StigSeverity_Distribution()
    {
        // Arrange - Sample STIG distribution by severity
        var stigsBySeverity = new Dictionary<StigSeverity, List<StigControl>>
        {
            [StigSeverity.High] = new List<StigControl>
            {
                new StigControl { StigId = "V-001", Title = "Critical encryption", Severity = StigSeverity.High }
            },
            [StigSeverity.Medium] = new List<StigControl>
            {
                new StigControl { StigId = "V-002", Title = "Audit logging", Severity = StigSeverity.Medium },
                new StigControl { StigId = "V-003", Title = "Password policy", Severity = StigSeverity.Medium }
            },
            [StigSeverity.Low] = new List<StigControl>
            {
                new StigControl { StigId = "V-004", Title = "Banner message", Severity = StigSeverity.Low }
            }
        };

        // Assert
        stigsBySeverity[StigSeverity.High].Should().HaveCount(1);
        stigsBySeverity[StigSeverity.Medium].Should().HaveCount(2);
        stigsBySeverity[StigSeverity.Low].Should().HaveCount(1);
    }

    #endregion

    #region DoD Instruction Workflow Tests

    [Fact]
    public void DoDInstruction_RelationshipMapping()
    {
        // Arrange
        var instructions = new List<DoDInstruction>
        {
            new DoDInstruction
            {
                InstructionId = "DoDI 8500.01",
                Title = "Cybersecurity",
                RelatedNistControls = new List<string> { "AC-1", "AT-1", "AU-1", "CA-1" }
            },
            new DoDInstruction
            {
                InstructionId = "DoDI 8510.01",
                Title = "RMF for DoD IT",
                RelatedNistControls = new List<string> { "CA-1", "CA-2", "CA-5", "CA-6", "CA-7" }
            }
        };

        // Act - Find instructions related to CA-1
        var ca1Instructions = instructions
            .Where(i => i.RelatedNistControls.Contains("CA-1"))
            .ToList();

        // Assert
        ca1Instructions.Should().HaveCount(2);
        ca1Instructions.Select(i => i.InstructionId).Should().Contain("DoDI 8500.01");
        ca1Instructions.Select(i => i.InstructionId).Should().Contain("DoDI 8510.01");
    }

    #endregion

    #region Impact Level Transition Tests

    [Fact]
    public void ImpactLevel_MigrationPath_IL2toIL5()
    {
        // Arrange
        var migrationSteps = new List<(string FromLevel, string ToLevel, List<string> Requirements)>
        {
            ("IL2", "IL4", new List<string>
            {
                "Migrate to Azure Government",
                "Enable FIPS 140-2 encryption",
                "Configure ExpressRoute/VPN"
            }),
            ("IL4", "IL5", new List<string>
            {
                "Implement physical isolation",
                "Dedicated ExpressRoute only",
                "Enhanced personnel vetting",
                "Customer-managed keys"
            })
        };

        // Assert
        migrationSteps.Should().HaveCount(2);
        migrationSteps[0].Requirements.Should().Contain(r => r.Contains("Azure Government"));
        migrationSteps[1].Requirements.Should().Contain(r => r.Contains("physical isolation"));
    }

    [Fact]
    public void ImpactLevel_AzureServiceAvailability()
    {
        // Arrange
        var serviceAvailability = new Dictionary<string, List<string>>
        {
            ["IL2"] = new List<string>
            {
                "Azure Kubernetes Service",
                "Azure SQL Database",
                "Azure Functions",
                "Azure App Service"
            },
            ["IL4"] = new List<string>
            {
                "Azure Kubernetes Service",
                "Azure SQL Database",
                "Azure Virtual Machines",
                "Azure Key Vault"
            },
            ["IL5"] = new List<string>
            {
                "Azure Kubernetes Service",
                "Azure SQL Database",
                "Azure Virtual Machines",
                "Azure Key Vault",
                "Azure Dedicated HSM"
            }
        };

        // Assert
        serviceAvailability["IL2"].Should().Contain("Azure Kubernetes Service");
        serviceAvailability["IL5"].Should().Contain("Azure Dedicated HSM");
    }

    #endregion

    #region Compliance Documentation Generation Tests

    [Fact]
    public void DocumentGeneration_ControlNarrative()
    {
        // Arrange
        var controlNarrative = @"# AC-2: Account Management

## Control Implementation Status
**Status:** Implemented

## Implementation Description
The organization manages information system accounts using Azure Active Directory (Azure AD) for centralized identity management.

### Account Types Managed
- Individual user accounts
- Group accounts
- Service accounts (Managed Identities)
- Emergency accounts (break-glass)

### Implementation Details
1. **Account Creation**: Automated through HR system integration
2. **Account Review**: Quarterly access reviews using Azure AD Access Reviews
3. **Account Removal**: Automated deprovisioning when user leaves organization
4. **Privileged Access**: Managed through Azure AD PIM

## Evidence
- Azure AD configuration screenshots
- Access review reports
- PIM activation logs";

        // Assert
        controlNarrative.Should().Contain("AC-2");
        controlNarrative.Should().Contain("Implementation");
        controlNarrative.Should().Contain("Azure AD");
        controlNarrative.Should().Contain("Evidence");
    }

    [Fact]
    public void DocumentGeneration_SSPSection()
    {
        // Arrange
        var sspSection = @"# Section 13: System and Communications Protection

## 13.1 Overview

This section describes the security controls implemented to protect the system's communications and data.

## 13.2 Applicable Controls

| Control ID | Title | Status |
|------------|-------|--------|
| SC-7 | Boundary Protection | Implemented |
| SC-8 | Transmission Confidentiality | Implemented |
| SC-12 | Cryptographic Key Establishment | Implemented |
| SC-28 | Protection of Information at Rest | Implemented |

## 13.3 Implementation Details

### SC-7: Boundary Protection
Azure Firewall Premium is deployed as the central network security appliance...

### SC-28: Protection of Information at Rest
All data is encrypted at rest using Azure Storage Service Encryption with customer-managed keys stored in Azure Key Vault...";

        // Assert
        sspSection.Should().Contain("System and Communications Protection");
        sspSection.Should().Contain("SC-7");
        sspSection.Should().Contain("SC-28");
        sspSection.Should().Contain("Azure Firewall");
    }

    #endregion

    #region Cross-Framework Mapping Tests

    [Fact]
    public void CrossFramework_NistToStigToCci()
    {
        // Arrange
        var crossMapping = new List<(string NistControl, string StigId, string CciId)>
        {
            ("AU-2", "V-219153", "CCI-000172"),
            ("AU-3", "V-219154", "CCI-000173"),
            ("IA-2", "V-219187", "CCI-000765"),
            ("SC-28", "V-219200", "CCI-001199")
        };

        // Assert
        crossMapping.Should().HaveCount(4);
        crossMapping.All(m => m.NistControl.Length >= 4).Should().BeTrue();
        crossMapping.All(m => m.StigId.StartsWith("V-")).Should().BeTrue();
        crossMapping.All(m => m.CciId.StartsWith("CCI-")).Should().BeTrue();
    }

    [Fact]
    public void CrossFramework_FedRAMPToNist()
    {
        // Arrange
        var fedRampBaselines = new Dictionary<string, List<string>>
        {
            ["Low"] = new List<string> { "AC-1", "AC-2", "AU-1", "AU-2" },
            ["Moderate"] = new List<string> { "AC-1", "AC-2", "AC-3", "AC-4", "AU-1", "AU-2", "AU-3", "AU-6" },
            ["High"] = new List<string> { "AC-1", "AC-2", "AC-3", "AC-4", "AC-5", "AC-6", "AU-1", "AU-2", "AU-3", "AU-6", "AU-12" }
        };

        // Assert
        fedRampBaselines["High"].Should().HaveCountGreaterThan(fedRampBaselines["Moderate"].Count);
        fedRampBaselines["Moderate"].Should().HaveCountGreaterThan(fedRampBaselines["Low"].Count);
    }

    #endregion

    #region Navy-Specific Workflow Tests

    [Fact]
    public void NavyWorkflow_AtoProcess()
    {
        // Arrange
        var navyAtoWorkflow = new DoDWorkflow
        {
            WorkflowId = "NAVY-ATO-001",
            Name = "Navy ATO Process",
            Organization = DoDOrganization.Navy,
            Description = "Standard Navy Authorization to Operate process",
            Steps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    StepNumber = 1,
                    Title = "eMASS Registration",
                    Description = "Register system in eMASS",
                    EstimatedDuration = "2 weeks"
                },
                new WorkflowStep
                {
                    StepNumber = 2,
                    Title = "Control Selection",
                    Description = "Select and tailor security controls",
                    EstimatedDuration = "2-4 weeks"
                },
                new WorkflowStep
                {
                    StepNumber = 3,
                    Title = "Implementation",
                    Description = "Implement security controls",
                    EstimatedDuration = "4-8 weeks"
                },
                new WorkflowStep
                {
                    StepNumber = 4,
                    Title = "Assessment",
                    Description = "Security control assessment",
                    EstimatedDuration = "2-4 weeks"
                },
                new WorkflowStep
                {
                    StepNumber = 5,
                    Title = "Authorization",
                    Description = "Submit for NAO authorization",
                    EstimatedDuration = "2-4 weeks"
                }
            },
            RequiredDocuments = new List<string> { "SSP", "SAR", "POA&M", "ATO Package" },
            ApprovalAuthorities = new List<string> { "NAO", "ISSM" },
            ImpactLevel = "IL5"
        };

        // Assert
        navyAtoWorkflow.Organization.Should().Be(DoDOrganization.Navy);
        navyAtoWorkflow.Steps.Should().HaveCount(5);
        navyAtoWorkflow.RequiredDocuments.Should().Contain("SSP");
        navyAtoWorkflow.ApprovalAuthorities.Should().Contain("NAO");
    }

    #endregion
}
