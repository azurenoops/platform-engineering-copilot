using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Platform.Engineering.Copilot.Tests.Manual.Agents;

/// <summary>
/// Manual tests for Compliance Agent via MCP HTTP API.
/// Tests NIST 800-53 compliance scanning, ATO preparation, evidence collection, and remediation.
/// 
/// Prerequisites:
/// - MCP server running in HTTP mode: dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http --port 5100
/// - Azure CLI authenticated
/// </summary>
public class ComplianceAgentManualTests : McpHttpTestBase
{
    // Accept multiple intent types since orchestrator may return different intents
    private static readonly string[] AcceptableIntents = { "compliance", "multi_agent", "agent_execution", "orchestrat" };

    public ComplianceAgentManualTests(ITestOutputHelper output) : base(output) { }

    #region Compliance Assessments

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task RunComplianceAssessment_AccessControlFamily_ShouldReturnFindings()
    {
        // Arrange
        var message = "Run a NIST 800-53 compliance assessment for the AC (Access Control) control family against my Azure subscription";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-ac-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertComplianceResponseStructure(response, "AC");
        
        // Assert - Quality checks
        AssertPerformance(response, maxMilliseconds: 60000);
        
        // Log response summary for manual review
        Output.WriteLine($"\n📋 Response Preview:\n{response.Response?[..Math.Min(500, response.Response?.Length ?? 0)]}...");
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task RunComplianceAssessment_SystemCommunicationsProtection_ShouldReturnFindings()
    {
        // Arrange
        var message = "Run a NIST 800-53 compliance assessment for the SC (System and Communications Protection) control family";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-sc-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content should contain SC controls and status
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertComplianceResponseStructure(response, "SC");
        
        // Response should mention encryption, communications, or network security
        AssertResponseContains(response, "SC-", "encryption", "communication", "network", "protection", "TLS", "HTTPS");
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task RunComplianceAssessment_AuditAccountability_ShouldReturnFindings()
    {
        // Arrange
        var message = "Run NIST 800-53 compliance assessment for AU (Audit and Accountability) controls";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-au-001");

        // Assert
        AssertSuccessfulResponse(response);
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertComplianceResponseStructure(response, "AU");
        
        // AU controls relate to logging, audit, monitoring
        AssertResponseContains(response, "AU-", "audit", "log", "monitor", "record", "accountability");
    }

    #endregion

    #region Specific Control Checks

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task CheckSpecificControl_AC2AccountManagement_ShouldReturnStatus()
    {
        // Arrange
        var message = "Check compliance status for control AC-2 Account Management and show any findings or gaps";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-ac2-001");

        // Assert - Should specifically mention AC-2
        AssertSuccessfulResponse(response);
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        
        // Should contain AC-2 specific content
        AssertResponseContains(response, "AC-2", "account", "management", "user", "access");
        
        // Should have status information
        AssertResponseContains(response, "status", "compliant", "finding", "gap", "implemented", "control");
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task CheckSpecificControl_SC8TransmissionConfidentiality_ShouldReturnStatus()
    {
        // Arrange
        var message = "Check compliance status for control SC-8 Transmission Confidentiality and Integrity";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-sc8-001");

        // Assert
        AssertSuccessfulResponse(response);
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        
        // SC-8 relates to transmission security
        AssertResponseContains(response, "SC-8", "transmission", "confidentiality", "integrity", "encrypt", "TLS");
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task CheckSpecificControl_IA2IdentificationAuthentication_ShouldReturnStatus()
    {
        // Arrange
        var message = "Check compliance for IA-2 Identification and Authentication control with MFA requirements";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-ia2-001");

        // Assert
        AssertSuccessfulResponse(response);
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        
        // IA-2 relates to authentication and MFA
        AssertResponseContains(response, "IA-2", "authentication", "identification", "MFA", "multi-factor", "identity");
    }

    #endregion

    #region SSP Generation

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task GenerateSSPSection_SystemCommunications_ShouldReturnDocumentation()
    {
        // Arrange
        var message = "Generate the System Security Plan section for the SC (System and Communications Protection) control family including control implementation statements";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-ssp-sc-001");

        // Assert - SSP should be well-formatted documentation
        AssertSuccessfulResponse(response);
        AssertResponseHasMeaningfulContent(response, minLength: 200);
        
        // SSP should have markdown formatting for documentation
        AssertResponseHasMarkdownFormatting(response);
        
        // Should contain implementation statements
        AssertResponseContains(response, "implementation", "control", "SC-", "SSP", "System Security Plan", "description");
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task GenerateSSPSection_AccessControl_ShouldReturnDocumentation()
    {
        // Arrange
        var message = "Generate SSP documentation for the AC (Access Control) control family with Azure-specific implementation details";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-ssp-ac-001");

        // Assert
        AssertSuccessfulResponse(response);
        AssertResponseHasMeaningfulContent(response, minLength: 200);
        AssertResponseHasMarkdownFormatting(response);
        
        // Should mention Azure services
        AssertResponseContains(response, "AC-", "Azure", "implementation", "RBAC", "access control", "Entra", "Active Directory");
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task GenerateSSPSection_IdentificationAuthentication_ShouldReturnDocumentation()
    {
        // Arrange
        var message = "Generate SSP section for IA (Identification and Authentication) controls including Azure AD integration";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-ssp-ia-001");

        // Assert
        AssertSuccessfulResponse(response);
        AssertResponseHasMeaningfulContent(response, minLength: 200);
        
        // Should reference Azure identity services
        AssertResponseContains(response, "IA-", "Azure AD", "Entra", "authentication", "identity", "SSO", "MFA");
    }

    #endregion

    #region Evidence Collection

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task CollectEvidence_AuditAccountability_ShouldReturnEvidence()
    {
        // Arrange
        var message = "Collect compliance evidence for the AU (Audit and Accountability) control family including configuration snapshots and audit logs";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-evidence-au-001");

        // Assert
        AssertSuccessfulResponse(response);
        AssertResponseHasMeaningfulContent(response, minLength: 100);
        
        // Evidence collection should mention artifacts
        AssertResponseContains(response, "evidence", "audit", "log", "configuration", "snapshot", "AU-", "monitor");
        
        // Should indicate tool execution for evidence gathering
        if (response.ToolExecuted)
        {
            Output.WriteLine("✅ Tool was executed for evidence collection");
        }
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task CollectEvidence_ConfigurationManagement_ShouldReturnEvidence()
    {
        // Arrange
        var message = "Collect compliance evidence for CM (Configuration Management) controls including resource configurations";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-evidence-cm-001");

        // Assert
        AssertSuccessfulResponse(response);
        AssertResponseHasMeaningfulContent(response, minLength: 100);
        
        // CM evidence should include configuration data
        AssertResponseContains(response, "CM-", "configuration", "baseline", "setting", "evidence", "resource");
    }

    #endregion

    #region Remediation Planning

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task CreateRemediationPlan_IdentificationAuthentication_ShouldReturnPlan()
    {
        // Arrange
        var message = "Create a remediation plan for all high-severity compliance findings in the IA (Identification and Authentication) control family";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-remediation-ia-001");

        // Assert - Remediation plan should be structured
        AssertSuccessfulResponse(response);
        AssertResponseHasMeaningfulContent(response, minLength: 150);
        AssertResponseHasMarkdownFormatting(response);
        
        // Should contain actionable remediation steps
        AssertResponseContains(response, "remediation", "step", "action", "fix", "resolve", "IA-", "implement");
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task CreateRemediationPlan_AllHighSeverity_ShouldReturnPrioritizedPlan()
    {
        // Arrange
        var message = "Create a prioritized remediation plan for all high and critical severity compliance findings across all control families";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-remediation-all-001");

        // Assert
        AssertSuccessfulResponse(response);
        AssertResponseHasMeaningfulContent(response, minLength: 150);
        
        // Should have prioritization
        AssertResponseContains(response, "priority", "critical", "high", "remediation", "finding", "action", "step");
    }

    #endregion

    #region ATO Readiness

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task GetATOStatus_Summary_ShouldReturnReadinessReport()
    {
        // Arrange
        var message = "Provide an ATO readiness summary showing compliance percentage by control family and list any POA&M items";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-ato-summary-001");

        // Assert - ATO report should have structured data
        AssertSuccessfulResponse(response);
        AssertResponseHasMeaningfulContent(response, minLength: 100);
        
        // Should contain ATO-specific terminology
        AssertResponseContains(response, "ATO", "readiness", "POA&M", "compliance", "percentage", "control", "status");
        
        // Should have percentage or numeric data
        var content = response.Response ?? "";
        var hasPercentage = content.Contains("%") || System.Text.RegularExpressions.Regex.IsMatch(content, @"\d+");
        Output.WriteLine($"   Numeric/percentage data: {(hasPercentage ? "✅" : "⚠️")}");
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task GetATOStatus_FedRAMPModerate_ShouldReturnBaseline()
    {
        // Arrange
        var message = "Show ATO readiness status for FedRAMP Moderate baseline with gap analysis";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-ato-fedramp-001");

        // Assert
        AssertSuccessfulResponse(response);
        AssertResponseHasMeaningfulContent(response, minLength: 100);
        
        // Should reference FedRAMP specifically
        AssertResponseContains(response, "FedRAMP", "Moderate", "baseline", "gap", "control", "compliance");
    }

    #endregion

    #region Continuous Monitoring

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task GenerateContinuousMonitoringReport_Last30Days_ShouldReturnReport()
    {
        // Arrange
        var message = "Generate a continuous monitoring report for the last 30 days showing compliance drift, new findings, and resolved issues";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-conmon-001");

        // Assert - ConMon report should have trend data
        AssertSuccessfulResponse(response);
        AssertResponseHasMeaningfulContent(response, minLength: 100);
        AssertResponseHasMarkdownFormatting(response);
        
        // Should contain monitoring terminology
        AssertResponseContains(response, "monitoring", "drift", "finding", "resolved", "trend", "30 day", "report");
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Compliance")]
    public async Task GenerateContinuousMonitoringReport_Quarterly_ShouldReturnReport()
    {
        // Arrange
        var message = "Generate a quarterly continuous monitoring report with trend analysis and executive summary";

        // Act
        var response = await SendChatRequestAsync(message, "compliance-conmon-quarterly-001");

        // Assert
        AssertSuccessfulResponse(response);
        AssertResponseHasMeaningfulContent(response, minLength: 100);
        
        // Should have executive summary structure
        AssertResponseContains(response, "quarterly", "trend", "summary", "executive", "analysis", "report");
    }

    #endregion
}
