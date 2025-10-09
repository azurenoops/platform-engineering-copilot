using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Services.Compliance;

namespace Platform.Engineering.Copilot.Core.Plugins;

/// <summary>
/// Semantic Kernel plugin for ATO compliance operations - AI-powered query-based approach.
/// Simplified from 6 functions to 1 function using natural language processing.
/// </summary>
public class CompliancePlugin : BaseSupervisorPlugin
{
    private readonly ComplianceService _complianceService;

    public CompliancePlugin(
        ILogger<CompliancePlugin> logger,
        Kernel kernel,
        ComplianceService complianceService) : base(logger, kernel)
    {
        _complianceService = complianceService ?? throw new ArgumentNullException(nameof(complianceService));
    }

    [KernelFunction("process_compliance_query")]
    [Description("Process any ATO compliance query using natural language. Handles assessments, evidence collection, remediation, monitoring, reporting, risk assessment, and certificates. Use this for ANY compliance-related request. Examples: 'Run compliance assessment for subscription xyz', 'Collect evidence for AC-2 control', 'Generate FedRAMP report in PDF format', 'Check compliance status', 'Assess risks', 'Generate compliance certificate'.")]
    public async Task<string> ProcessComplianceQueryAsync(
        [Description("Natural language query describing the compliance operation (e.g., 'Run assessment for subscription abc-123', 'Collect evidence for AU controls', 'Generate compliance report')")] string query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing compliance query via plugin: {Query}", query);
            var result = await _complianceService.ProcessComplianceQueryAsync(query, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("process compliance query", ex);
        }
    }
}
