using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Compliance.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Repositories;
using Platform.Engineering.Copilot.Core.Helpers;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Data;

/// <summary>
/// Service for managing compliance assessments and findings using IComplianceAssessmentRepository.
/// Implements IAssessmentService interface for better testability and DI.
/// </summary>
public class AssessmentService : IAssessmentService
{
    private readonly IComplianceAssessmentRepository _complianceRepository;
    private readonly ILogger<AssessmentService> _logger;
    private readonly IMemoryCache _cache;

    // Cache configuration
    private static readonly TimeSpan AssessmentCacheDuration = TimeSpan.FromHours(24);
    private const string AssessmentCacheKeyPrefix = "ComplianceAssessment_";

    public AssessmentService(
        IComplianceAssessmentRepository complianceRepository,
        ILogger<AssessmentService> logger,
        IMemoryCache cache)
    {
        _complianceRepository = complianceRepository;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Saves a compliance assessment to the database
    /// </summary>
    public async Task<string> SaveAssessmentAsync(AtoComplianceAssessment assessment, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Saving compliance assessment {AssessmentId} for subscription {SubscriptionId}",
                assessment.AssessmentId, assessment.SubscriptionId);

            // Map domain model to entity
            var entity = new ComplianceAssessment
            {
                Id = assessment.AssessmentId,
                SubscriptionId = assessment.SubscriptionId,
                AssessmentType = "NIST-800-53",
                Status = string.IsNullOrEmpty(assessment.Error) ? "Completed" : "Failed",
                ComplianceScore = (decimal)assessment.OverallComplianceScore,
                TotalFindings = assessment.TotalFindings,
                CriticalFindings = assessment.CriticalFindings,
                HighFindings = assessment.HighFindings,
                MediumFindings = assessment.MediumFindings,
                LowFindings = assessment.LowFindings,
                InformationalFindings = 0, // Not tracked in AtoComplianceAssessment
                ExecutiveSummary = assessment.ExecutiveSummary,
                RiskProfile = assessment.RiskProfile != null ? JsonSerializer.Serialize(assessment.RiskProfile) : null,
                Results = JsonSerializer.Serialize(assessment.ControlFamilyResults),
                Recommendations = null, // Not tracked in AtoComplianceAssessment
                Metadata = JsonSerializer.Serialize(new { assessment.Error }),
                InitiatedBy = "System", // Not tracked in AtoComplianceAssessment
                StartedAt = assessment.StartTime.UtcDateTime,
                CompletedAt = assessment.EndTime.UtcDateTime,
                Duration = assessment.Duration.Ticks // Convert TimeSpan to ticks (long)
            };

            // Map findings
            foreach (var familyResult in assessment.ControlFamilyResults.Values)
            {
                foreach (var finding in familyResult.Findings)
                {
                    entity.Findings.Add(MapFindingToEntity(assessment.AssessmentId, finding));
                }
            }

            // Check if assessment already exists
            var existing = await _complianceRepository.GetByIdWithFindingsAsync(assessment.AssessmentId, cancellationToken);

            if (existing != null)
            {
                _logger.LogInformation("Updating existing assessment {AssessmentId}", assessment.AssessmentId);
                
                // Update the existing entity with new values
                existing.SubscriptionId = entity.SubscriptionId;
                existing.AssessmentType = entity.AssessmentType;
                existing.Status = entity.Status;
                existing.ComplianceScore = entity.ComplianceScore;
                existing.TotalFindings = entity.TotalFindings;
                existing.CriticalFindings = entity.CriticalFindings;
                existing.HighFindings = entity.HighFindings;
                existing.MediumFindings = entity.MediumFindings;
                existing.LowFindings = entity.LowFindings;
                existing.InformationalFindings = entity.InformationalFindings;
                existing.ExecutiveSummary = entity.ExecutiveSummary;
                existing.RiskProfile = entity.RiskProfile;
                existing.Results = entity.Results;
                existing.Recommendations = entity.Recommendations;
                existing.Metadata = entity.Metadata;
                existing.StartedAt = entity.StartedAt;
                existing.CompletedAt = entity.CompletedAt;
                existing.Duration = entity.Duration;
                
                // Clear and re-add findings
                existing.Findings.Clear();
                foreach (var finding in entity.Findings)
                {
                    existing.Findings.Add(finding);
                }

                await _complianceRepository.UpdateAsync(existing, cancellationToken);
                
                _logger.LogInformation("Successfully updated assessment {AssessmentId} with {FindingCount} findings", 
                    assessment.AssessmentId, entity.Findings.Count);

                return existing.Id;
            }
            else
            {
                _logger.LogInformation("Creating new assessment {AssessmentId}", assessment.AssessmentId);
                await _complianceRepository.AddAsync(entity, cancellationToken);
                
                _logger.LogInformation("Successfully saved assessment {AssessmentId} with {FindingCount} findings", 
                    assessment.AssessmentId, entity.Findings.Count);

                return entity.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving assessment {AssessmentId}", assessment.AssessmentId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a compliance assessment by ID
    /// </summary>
    public async Task<AtoComplianceAssessment?> GetAssessmentAsync(string assessmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Retrieving assessment {AssessmentId}", assessmentId);

            var entity = await _complianceRepository.GetByIdWithFindingsAsync(assessmentId, cancellationToken);

            if (entity == null)
            {
                _logger.LogWarning("Assessment {AssessmentId} not found", assessmentId);
                return null;
            }

            return MapEntityToAssessment(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assessment {AssessmentId}", assessmentId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all assessments for a subscription
    /// </summary>
    public async Task<IEnumerable<AtoComplianceAssessment>> GetAssessmentsAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Retrieving assessments for subscription {SubscriptionId}", subscriptionId);

            var entities = await _complianceRepository.GetBySubscriptionWithFindingsAsync(subscriptionId, cancellationToken);

            _logger.LogInformation("Found {Count} assessments for subscription {SubscriptionId}", 
                entities.Count, subscriptionId);

            return entities.Select(MapEntityToAssessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assessments for subscription {SubscriptionId}", subscriptionId);
            throw;
        }
    }

    /// <summary>
    /// Gets a summary of assessments for a subscription
    /// </summary>
    public async Task<AtoScanSummary> GetAssessmentSummaryAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting assessment summary for subscription {SubscriptionId}", subscriptionId);

            var latestAssessment = await _complianceRepository.GetLatestBySubscriptionAsync(subscriptionId, cancellationToken);

            if (latestAssessment == null)
            {
                return new AtoScanSummary
                {
                    TotalResourcesScanned = 0,
                    TotalFindings = 0,
                    CriticalFindings = 0,
                    HighFindings = 0,
                    MediumFindings = 0,
                    LowFindings = 0,
                    InformationalFindings = 0,
                    RemediableFindings = 0,
                    NonCompliantResources = 0,
                    ComplianceScore = 0,
                    FindingsByType = new Dictionary<string, int>(),
                    FindingsByFramework = new Dictionary<string, int>()
                };
            }

            var findings = await _complianceRepository.GetFindingsAsync(latestAssessment.Id, cancellationToken);

            // Calculate findings by type
            var findingsByType = findings
                .GroupBy(f => f.FindingType)
                .ToDictionary(g => g.Key, g => g.Count());

            // Calculate findings by framework
            var findingsByFramework = new Dictionary<string, int>();
            foreach (var finding in findings)
            {
                if (!string.IsNullOrEmpty(finding.ComplianceFrameworks))
                {
                    var frameworks = DeserializeList(finding.ComplianceFrameworks);
                    foreach (var framework in frameworks)
                    {
                        if (findingsByFramework.ContainsKey(framework))
                            findingsByFramework[framework]++;
                        else
                            findingsByFramework[framework] = 1;
                    }
                }
            }

            return new AtoScanSummary
            {
                TotalResourcesScanned = findings.Select(f => f.ResourceId).Distinct().Count(),
                TotalFindings = latestAssessment.TotalFindings,
                CriticalFindings = latestAssessment.CriticalFindings,
                HighFindings = latestAssessment.HighFindings,
                MediumFindings = latestAssessment.MediumFindings,
                LowFindings = latestAssessment.LowFindings,
                InformationalFindings = latestAssessment.InformationalFindings,
                RemediableFindings = findings.Count(f => f.IsRemediable),
                NonCompliantResources = findings.Where(f => f.ComplianceStatus == "NonCompliant")
                    .Select(f => f.ResourceId)
                    .Distinct()
                    .Count(),
                ComplianceScore = (double)latestAssessment.ComplianceScore,
                FindingsByType = findingsByType,
                FindingsByFramework = findingsByFramework
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assessment summary for subscription {SubscriptionId}", subscriptionId);
            throw;
        }
    }

    /// <summary>
    /// Deletes an assessment and all its findings
    /// </summary>
    public async Task<bool> DeleteAssessmentAsync(string assessmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting assessment {AssessmentId}", assessmentId);

            var result = await _complianceRepository.DeleteAsync(assessmentId, cancellationToken);

            if (!result)
            {
                _logger.LogWarning("Assessment {AssessmentId} not found for deletion", assessmentId);
                return false;
            }

            _logger.LogInformation("Successfully deleted assessment {AssessmentId}", assessmentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting assessment {AssessmentId}", assessmentId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all findings for an assessment
    /// </summary>
    public async Task<IEnumerable<AtoFinding>> GetFindingsAsync(string assessmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Retrieving findings for assessment {AssessmentId}", assessmentId);

            var findings = await _complianceRepository.GetFindingsAsync(assessmentId, cancellationToken);

            _logger.LogInformation("Found {Count} findings for assessment {AssessmentId}", 
                findings.Count, assessmentId);

            return findings.Select(MapEntityToFinding);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving findings for assessment {AssessmentId}", assessmentId);
            throw;
        }
    }

    /// <summary>
    /// Marks a finding as resolved
    /// </summary>
    public async Task<bool> ResolveFindingAsync(string assessmentId, string findingId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Resolving finding {FindingId} in assessment {AssessmentId}", 
                findingId, assessmentId);

            var finding = await _complianceRepository.GetFindingAsync(assessmentId, findingId, cancellationToken);

            if (finding == null)
            {
                _logger.LogWarning("Finding {FindingId} not found in assessment {AssessmentId}", 
                    findingId, assessmentId);
                return false;
            }

            finding.ResolvedAt = DateTime.UtcNow;
            finding.ComplianceStatus = "Resolved";

            await _complianceRepository.UpdateFindingAsync(finding, cancellationToken);

            _logger.LogInformation("Successfully resolved finding {FindingId}", findingId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving finding {FindingId} in assessment {AssessmentId}", 
                findingId, assessmentId);
            throw;
        }
    }

    #region Extended Methods for AtoComplianceEngine

    /// <summary>
    /// Gets the latest completed assessment with findings for a subscription
    /// </summary>
    public async Task<AtoComplianceAssessment?> GetLatestCompletedAssessmentAsync(
        string subscriptionId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔍 Querying database for latest completed assessment for subscription {SubscriptionId}", subscriptionId);

            var latestDbAssessment = await _complianceRepository.GetLatestCompletedWithFindingsAsync(subscriptionId, cancellationToken);

            if (latestDbAssessment == null)
            {
                _logger.LogInformation("No completed assessment found in database for subscription {SubscriptionId}", subscriptionId);
                return null;
            }

            _logger.LogInformation("Found assessment {AssessmentId} completed at {CompletedAt} with score {Score}%",
                latestDbAssessment.Id, latestDbAssessment.CompletedAt, latestDbAssessment.ComplianceScore);

            return MapEntityToAssessmentWithFindings(latestDbAssessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest completed assessment for subscription {SubscriptionId}", subscriptionId);
            throw;
        }
    }

    /// <summary>
    /// Gets monitored controls for a subscription based on the latest assessment
    /// </summary>
    public async Task<List<MonitoredControl>> GetMonitoredControlsAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latestAssessment = await _complianceRepository.GetLatestCompletedWithFindingsAsync(subscriptionId, cancellationToken);

            if (latestAssessment == null)
            {
                _logger.LogDebug("No assessments found for subscription {SubscriptionId}", subscriptionId);
                return new List<MonitoredControl>();
            }

            // Extract unique control IDs from findings
            var controlIds = latestAssessment.Findings
                .Where(f => !string.IsNullOrEmpty(f.ControlId))
                .Select(f => f.ControlId!)
                .Distinct()
                .ToList();

            // Also extract from AffectedNistControls JSON
            var affectedControls = latestAssessment.Findings
                .Where(f => !string.IsNullOrEmpty(f.AffectedNistControls))
                .SelectMany(f =>
                {
                    try
                    {
                        return JsonSerializer.Deserialize<List<string>>(f.AffectedNistControls!) ?? new List<string>();
                    }
                    catch
                    {
                        return new List<string>();
                    }
                })
                .Distinct()
                .ToList();

            // Combine all control IDs
            var allControlIds = controlIds.Concat(affectedControls).Distinct().ToList();

            // Build MonitoredControl objects
            var monitoredControls = allControlIds.Select(controlId =>
            {
                var controlFindings = latestAssessment.Findings
                    .Where(f => f.ControlId == controlId ||
                               (!string.IsNullOrEmpty(f.AffectedNistControls) &&
                                f.AffectedNistControls.Contains(controlId)))
                    .ToList();

                var hasFailures = controlFindings.Any(f =>
                    f.ComplianceStatus == "NonCompliant" ||
                    f.Severity == "Critical" ||
                    f.Severity == "High");

                var hasDrift = controlFindings.Any(f => f.ResolvedAt == null);

                return new MonitoredControl
                {
                    ControlId = controlId,
                    LastChecked = latestAssessment.CompletedAt ?? DateTimeOffset.UtcNow,
                    ComplianceStatus = hasFailures ? "NonCompliant" : "Compliant",
                    DriftDetected = hasDrift,
                    AutoRemediationEnabled = controlFindings.Any(f => f.IsAutomaticallyFixable)
                };
            }).ToList();

            _logger.LogInformation("Retrieved {Count} monitored controls for subscription {SubscriptionId}",
                monitoredControls.Count, subscriptionId);

            return monitoredControls;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve monitored controls for subscription {SubscriptionId}", subscriptionId);
            return new List<MonitoredControl>();
        }
    }

    /// <summary>
    /// Gets unresolved findings for a specific control
    /// </summary>
    public async Task<List<ComplianceAlert>> GetControlAlertsAsync(
        string subscriptionId,
        string controlId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var findings = await _complianceRepository.GetUnresolvedFindingsByControlAsync(
                subscriptionId, controlId, limit, cancellationToken);

            var alerts = findings.Select(f => new ComplianceAlert
            {
                AlertId = Guid.NewGuid().ToString(),
                ControlId = controlId,
                Type = DetermineAlertType(f.FindingType),
                Severity = ParseAlertSeverity(f.Severity),
                SeverityString = f.Severity,
                Title = f.Title,
                Message = f.Description,
                Description = f.Description,
                AffectedResources = new List<string>
                {
                    f.ResourceId ?? "Unknown Resource"
                }.Where(r => !string.IsNullOrEmpty(r)).ToList(),
                ActionRequired = !string.IsNullOrEmpty(f.Remediation)
                    ? f.Remediation
                    : "Review and remediate this finding",
                AlertTime = new DateTimeOffset(f.DetectedAt, TimeSpan.Zero),
                DueDate = CalculateAlertDueDate(f.Severity, f.DetectedAt).DateTime,
                Acknowledged = false
            }).ToList();

            _logger.LogDebug("Retrieved {Count} alerts for control {ControlId} in subscription {SubscriptionId}",
                alerts.Count, controlId, subscriptionId);

            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve alerts for control {ControlId} in subscription {SubscriptionId}",
                controlId, subscriptionId);
            return new List<ComplianceAlert>();
        }
    }

    /// <summary>
    /// Counts auto-remediated findings for a subscription
    /// </summary>
    public async Task<int> CountAutoRemediatedFindingsAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _complianceRepository.CountAutoRemediatedFindingsAsync(subscriptionId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to count auto-remediated findings for subscription {SubscriptionId}", subscriptionId);
            return 0;
        }
    }

    /// <summary>
    /// Gets compliance score at a specific date
    /// </summary>
    public async Task<double> GetComplianceScoreAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assessment = await _complianceRepository.GetBySubscriptionAndDateAsync(subscriptionId, date.DateTime, cancellationToken);
            return assessment != null ? (double)assessment.ComplianceScore : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve compliance score for date {Date}", date);
            return 0;
        }
    }

    /// <summary>
    /// Gets failed controls count at a specific date
    /// </summary>
    public async Task<int> GetFailedControlsAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assessment = await _complianceRepository.GetBySubscriptionAndDateWithFindingsAsync(subscriptionId, date.DateTime, cancellationToken);

            if (assessment == null)
                return 0;

            var failedControls = assessment.Findings
                .Where(f => !string.IsNullOrEmpty(f.AffectedNistControls))
                .SelectMany(f =>
                {
                    try
                    {
                        return JsonSerializer.Deserialize<List<string>>(f.AffectedNistControls!) ?? new List<string>();
                    }
                    catch
                    {
                        return !string.IsNullOrEmpty(f.ControlId) ? new List<string> { f.ControlId } : new List<string>();
                    }
                })
                .Distinct()
                .Count();

            return failedControls;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve failed controls for date {Date}", date);
            return 0;
        }
    }

    /// <summary>
    /// Gets passed controls count at a specific date
    /// </summary>
    public async Task<int> GetPassedControlsAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assessment = await _complianceRepository.GetBySubscriptionAndDateAsync(subscriptionId, date.DateTime, cancellationToken);

            if (assessment == null)
                return 0;

            var estimatedTotalControls = 100;
            var passedControls = (int)Math.Round(estimatedTotalControls * ((double)assessment.ComplianceScore / 100));

            return passedControls;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve passed controls for date {Date}", date);
            return 0;
        }
    }

    /// <summary>
    /// Gets active findings count at a specific date
    /// </summary>
    public async Task<int> GetActiveFindingsAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assessment = await _complianceRepository.GetBySubscriptionAndDateAsync(subscriptionId, date.DateTime, cancellationToken);
            return assessment?.TotalFindings ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve active findings for date {Date}", date);
            return 0;
        }
    }

    /// <summary>
    /// Gets remediated findings count at a specific date
    /// </summary>
    public async Task<int> GetRemediatedFindingsAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var currentAssessment = await _complianceRepository.GetBySubscriptionAndDateAsync(subscriptionId, date.DateTime, cancellationToken);

            if (currentAssessment == null || currentAssessment.CompletedAt == null)
                return 0;

            var previousAssessment = await _complianceRepository.GetPreviousAssessmentAsync(subscriptionId, currentAssessment.CompletedAt.Value, cancellationToken);

            if (previousAssessment == null)
                return 0;

            return Math.Max(0, previousAssessment.TotalFindings - currentAssessment.TotalFindings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve remediated findings for date {Date}", date);
            return 0;
        }
    }

    /// <summary>
    /// Gets compliance events at a specific date
    /// </summary>
    public async Task<List<string>> GetComplianceEventsAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken = default)
    {
        var events = new List<string>();

        try
        {
            var currentAssessment = await _complianceRepository.GetBySubscriptionAndDateAsync(
                subscriptionId, date.DateTime, cancellationToken);

            if (currentAssessment == null)
                return events;

            var previousAssessment = currentAssessment.CompletedAt.HasValue
                ? await _complianceRepository.GetPreviousAssessmentAsync(
                    subscriptionId, currentAssessment.CompletedAt.Value, cancellationToken)
                : null;

            if (previousAssessment != null)
            {
                var scoreDelta = (double)(currentAssessment.ComplianceScore - previousAssessment.ComplianceScore);

                if (scoreDelta >= 10)
                {
                    events.Add($"Compliance score improved by {scoreDelta:F1}% (from {previousAssessment.ComplianceScore}% to {currentAssessment.ComplianceScore}%)");
                }
                else if (scoreDelta <= -10)
                {
                    events.Add($"⚠️ Compliance score declined by {Math.Abs(scoreDelta):F1}% (from {previousAssessment.ComplianceScore}% to {currentAssessment.ComplianceScore}%)");
                }

                var newCritical = currentAssessment.CriticalFindings - previousAssessment.CriticalFindings;
                if (newCritical > 0)
                {
                    events.Add($"🔴 {newCritical} new critical finding{(newCritical > 1 ? "s" : "")} detected");
                }
                else if (newCritical < 0)
                {
                    events.Add($"✅ {Math.Abs(newCritical)} critical finding{(Math.Abs(newCritical) > 1 ? "s" : "")} resolved");
                }
            }

            return events;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve compliance events for date {Date}", date);
            return events;
        }
    }

    /// <summary>
    /// Searches assessments with filters
    /// </summary>
    public async Task<IReadOnlyList<ComplianceAssessment>> SearchAssessmentsAsync(
        string? subscriptionId = null,
        string? resourceGroup = null,
        string? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        return await _complianceRepository.SearchAsync(
            subscriptionId: subscriptionId,
            resourceGroup: resourceGroup,
            status: status,
            startDate: startDate,
            endDate: endDate,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets compliance history for a subscription
    /// </summary>
    public async Task<IReadOnlyList<ComplianceAssessmentSummary>> GetComplianceHistoryAsync(
        string subscriptionId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var assessments = await _complianceRepository.SearchAsync(
            subscriptionId: subscriptionId,
            status: "Completed",
            startDate: startDate,
            endDate: endDate,
            cancellationToken: cancellationToken);

        return assessments
            .OrderBy(a => a.CompletedAt)
            .Select(a => new ComplianceAssessmentSummary
            {
                Id = a.Id,
                CompletedAt = a.CompletedAt,
                ComplianceScore = a.ComplianceScore,
                TotalFindings = a.TotalFindings,
                CriticalFindings = a.CriticalFindings,
                HighFindings = a.HighFindings,
                MediumFindings = a.MediumFindings,
                LowFindings = a.LowFindings,
                InitiatedBy = a.InitiatedBy
            })
            .ToList();
    }

    /// <summary>
    /// Gets assessment audit log
    /// </summary>
    public async Task<IReadOnlyList<AssessmentAuditEntry>> GetAssessmentAuditLogAsync(
        string subscriptionId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var assessments = await _complianceRepository.SearchAsync(
            subscriptionId: subscriptionId,
            startDate: startDate,
            endDate: endDate,
            cancellationToken: cancellationToken);

        return assessments
            .OrderByDescending(a => a.CompletedAt)
            .Select(a => new AssessmentAuditEntry
            {
                Id = a.Id,
                StartedAt = a.StartedAt,
                CompletedAt = a.CompletedAt,
                Duration = a.Duration,
                Status = a.Status ?? "Unknown",
                InitiatedBy = a.InitiatedBy,
                ComplianceScore = a.ComplianceScore,
                TotalFindings = a.TotalFindings,
                CriticalFindings = a.CriticalFindings,
                HighFindings = a.HighFindings,
                ResourceGroupName = a.ResourceGroupName,
                AssessmentType = a.AssessmentType
            })
            .ToList();
    }

    /// <summary>
    /// Gets compliance trends data with findings
    /// </summary>
    public async Task<IReadOnlyList<ComplianceAssessmentWithFindings>> GetComplianceTrendsDataAsync(
        string subscriptionId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var assessments = await _complianceRepository.SearchAsync(
            subscriptionId: subscriptionId,
            status: "Completed",
            startDate: startDate,
            endDate: endDate,
            cancellationToken: cancellationToken);

        var result = new List<ComplianceAssessmentWithFindings>();

        foreach (var assessment in assessments.OrderBy(a => a.CompletedAt))
        {
            var assessmentWithFindings = await _complianceRepository.GetByIdWithFindingsAsync(
                assessment.Id, cancellationToken);

            if (assessmentWithFindings != null)
            {
                result.Add(new ComplianceAssessmentWithFindings
                {
                    Id = assessmentWithFindings.Id,
                    SubscriptionId = assessmentWithFindings.SubscriptionId,
                    ResourceGroupName = assessmentWithFindings.ResourceGroupName,
                    CompletedAt = assessmentWithFindings.CompletedAt,
                    ComplianceScore = assessmentWithFindings.ComplianceScore,
                    TotalFindings = assessmentWithFindings.TotalFindings,
                    CriticalFindings = assessmentWithFindings.CriticalFindings,
                    HighFindings = assessmentWithFindings.HighFindings,
                    MediumFindings = assessmentWithFindings.MediumFindings,
                    LowFindings = assessmentWithFindings.LowFindings,
                    Duration = assessmentWithFindings.Duration,
                    Results = assessmentWithFindings.Results,
                    Findings = assessmentWithFindings.Findings.Select(f => new ComplianceFindingSummary
                    {
                        Id = f.Id,
                        FindingId = f.FindingId,
                        RuleId = f.RuleId,
                        Title = f.Title,
                        Severity = f.Severity,
                        ComplianceStatus = f.ComplianceStatus,
                        ResourceId = f.ResourceId,
                        ControlId = f.ControlId
                    }).ToList()
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Gets a cached assessment if available
    /// </summary>
    public async Task<ComplianceAssessmentWithFindings?> GetCachedAssessmentAsync(
        string subscriptionId,
        string? resourceGroupName,
        int cacheHours,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddHours(-cacheHours);

            var assessments = await _complianceRepository.SearchAsync(
                subscriptionId: subscriptionId,
                resourceGroup: resourceGroupName,
                status: "Completed",
                startDate: cutoff,
                cancellationToken: cancellationToken);

            var cached = assessments
                .OrderByDescending(a => a.CompletedAt)
                .FirstOrDefault();

            if (cached == null)
                return null;

            var assessmentWithFindings = await _complianceRepository.GetByIdWithFindingsAsync(
                cached.Id, cancellationToken);

            if (assessmentWithFindings == null)
                return null;

            return new ComplianceAssessmentWithFindings
            {
                Id = assessmentWithFindings.Id,
                SubscriptionId = assessmentWithFindings.SubscriptionId,
                ResourceGroupName = assessmentWithFindings.ResourceGroupName,
                CompletedAt = assessmentWithFindings.CompletedAt,
                ComplianceScore = assessmentWithFindings.ComplianceScore,
                TotalFindings = assessmentWithFindings.TotalFindings,
                CriticalFindings = assessmentWithFindings.CriticalFindings,
                HighFindings = assessmentWithFindings.HighFindings,
                MediumFindings = assessmentWithFindings.MediumFindings,
                LowFindings = assessmentWithFindings.LowFindings,
                Duration = assessmentWithFindings.Duration,
                Results = assessmentWithFindings.Results,
                Findings = assessmentWithFindings.Findings.Select(f => new ComplianceFindingSummary
                {
                    Id = f.Id,
                    FindingId = f.FindingId,
                    RuleId = f.RuleId,
                    Title = f.Title,
                    Severity = f.Severity,
                    ComplianceStatus = f.ComplianceStatus,
                    ResourceId = f.ResourceId,
                    ControlId = f.ControlId
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cached assessment for subscription {SubscriptionId}", subscriptionId);
            return null;
        }
    }

    /// <summary>
    /// Gets an assessment by ID with findings
    /// </summary>
    public async Task<ComplianceAssessment?> GetAssessmentEntityWithFindingsAsync(
        string assessmentId,
        CancellationToken cancellationToken = default)
    {
        return await _complianceRepository.GetByIdWithFindingsAsync(assessmentId, cancellationToken);
    }

    /// <summary>
    /// Caches assessment summary for quick access
    /// </summary>
    public void CacheAssessmentSummary(AtoComplianceAssessment assessment)
    {
        var cacheKey = $"{AssessmentCacheKeyPrefix}{assessment.AssessmentId}";
        var cacheOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = AssessmentCacheDuration
        };

        var assessmentSummary = new
        {
            assessment.AssessmentId,
            assessment.SubscriptionId,
            assessment.OverallComplianceScore,
            assessment.TotalFindings,
            assessment.CriticalFindings,
            assessment.HighFindings,
            assessment.MediumFindings,
            assessment.LowFindings,
            FindingsCount = assessment.ControlFamilyResults.Values.Sum(cf => cf.Findings.Count),
            PassedControls = assessment.ControlFamilyResults.Values.Sum(cf => cf.PassedControls),
            TotalControls = assessment.ControlFamilyResults.Values.Sum(cf => cf.TotalControls),
            StartTime = assessment.StartTime,
            EndTime = assessment.EndTime,
            Duration = assessment.Duration
        };

        _cache.Set(cacheKey, assessmentSummary, cacheOptions);

        _logger.LogInformation("✅ Cached assessment {AssessmentId} with {FindingsCount} findings",
            assessment.AssessmentId, assessmentSummary.FindingsCount);
    }

    #endregion

    #region Private Helper Methods

    private static AlertType DetermineAlertType(string findingType)
    {
        return findingType?.ToLowerInvariant() switch
        {
            "security" => AlertType.NewCriticalFinding,
            "configuration" => AlertType.SecurityBaseline,
            "compliance" => AlertType.ComplianceFrameworkUpdate,
            "policy" => AlertType.SecurityBaseline,
            _ => AlertType.NewCriticalFinding
        };
    }

    private static AlertSeverity ParseAlertSeverity(string severity)
    {
        return severity?.ToLowerInvariant() switch
        {
            "critical" => AlertSeverity.Critical,
            "high" => AlertSeverity.Error,
            "medium" => AlertSeverity.Warning,
            "low" => AlertSeverity.Info,
            _ => AlertSeverity.Warning
        };
    }

    private static DateTimeOffset CalculateAlertDueDate(string severity, DateTime detectedAt)
    {
        var hoursToAdd = severity?.ToLowerInvariant() switch
        {
            "critical" => 24,
            "high" => 72,
            "medium" => 168, // 7 days
            "low" => 720, // 30 days
            _ => 168
        };

        return new DateTimeOffset(detectedAt.AddHours(hoursToAdd));
    }

    private AtoComplianceAssessment MapEntityToAssessmentWithFindings(ComplianceAssessment entity)
    {
        // Convert database findings to AtoFinding model
        var allFindings = entity.Findings
            .Select(f => new AtoFinding
            {
                Id = f.FindingId,
                RuleId = f.RuleId,
                Title = f.Title,
                Description = f.Description,
                Severity = ParseSeverity(f.Severity),
                ComplianceStatus = Enum.TryParse<AtoComplianceStatus>(f.ComplianceStatus, out var status) ? status : AtoComplianceStatus.NonCompliant,
                FindingType = Enum.TryParse<AtoFindingType>(f.FindingType, out var type) ? type : AtoFindingType.Configuration,
                ResourceId = f.ResourceId ?? string.Empty,
                ResourceType = f.ResourceType ?? string.Empty,
                ResourceName = f.ResourceName ?? string.Empty,
                AffectedNistControls = !string.IsNullOrEmpty(f.AffectedNistControls)
                    ? JsonSerializer.Deserialize<List<string>>(f.AffectedNistControls) ?? new List<string>()
                    : new List<string> { f.ControlId ?? string.Empty }.Where(c => !string.IsNullOrEmpty(c)).ToList(),
                ComplianceFrameworks = !string.IsNullOrEmpty(f.ComplianceFrameworks)
                    ? JsonSerializer.Deserialize<List<string>>(f.ComplianceFrameworks) ?? new List<string>()
                    : new List<string>(),
                Evidence = f.Evidence ?? string.Empty,
                RemediationGuidance = f.Remediation ?? string.Empty,
                IsRemediable = f.IsRemediable,
                IsAutoRemediable = f.IsAutomaticallyFixable,
                DetectedAt = f.DetectedAt,
                Metadata = !string.IsNullOrEmpty(f.Metadata)
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(f.Metadata) ?? new Dictionary<string, object>()
                    : new Dictionary<string, object>()
            })
            .ToList();

        // Group findings by control family to reconstruct ControlFamilyResults
        var controlFamilyResults = new Dictionary<string, ControlFamilyAssessment>();

        var controlFamilies = allFindings
            .SelectMany(f => f.AffectedNistControls)
            .Select(controlId => controlId.Length >= 2 ? controlId.Substring(0, 2).ToUpper() : controlId)
            .Distinct()
            .ToHashSet();

        foreach (var family in controlFamilies)
        {
            var familyFindings = allFindings
                .Where(f => f.AffectedNistControls.Any(c => c.StartsWith(family, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var uniqueControls = familyFindings
                .SelectMany(f => f.AffectedNistControls)
                .Where(c => c.StartsWith(family, StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList();

            controlFamilyResults[family] = new ControlFamilyAssessment
            {
                ControlFamily = family,
                FamilyName = ComplianceHelpers.GetControlFamilyName(family),
                TotalControls = uniqueControls.Count,
                PassedControls = 0, // Will be recalculated based on findings
                Findings = familyFindings,
                ComplianceScore = familyFindings.Count > 0 ? 0 : 100
            };
        }

        return new AtoComplianceAssessment
        {
            AssessmentId = entity.Id,
            SubscriptionId = entity.SubscriptionId,
            StartTime = new DateTimeOffset(entity.StartedAt),
            EndTime = entity.CompletedAt.HasValue
                ? new DateTimeOffset(entity.CompletedAt.Value)
                : DateTimeOffset.UtcNow,
            Duration = entity.Duration.HasValue ? TimeSpan.FromTicks(entity.Duration.Value) : TimeSpan.Zero,
            ControlFamilyResults = controlFamilyResults,
            OverallComplianceScore = (double)entity.ComplianceScore,
            TotalFindings = entity.TotalFindings,
            CriticalFindings = entity.CriticalFindings,
            HighFindings = entity.HighFindings,
            MediumFindings = entity.MediumFindings,
            LowFindings = entity.LowFindings,
            ExecutiveSummary = entity.ExecutiveSummary,
            RiskProfile = DeserializeRiskProfile(entity.RiskProfile)
        };
    }

    private static AtoFindingSeverity ParseSeverity(string severity)
    {
        return severity?.ToLowerInvariant() switch
        {
            "critical" => AtoFindingSeverity.Critical,
            "high" => AtoFindingSeverity.High,
            "medium" => AtoFindingSeverity.Medium,
            "low" => AtoFindingSeverity.Low,
            "informational" => AtoFindingSeverity.Informational,
            _ => AtoFindingSeverity.Medium
        };
    }

    #endregion

    #region Private Mapping Methods

    private static ComplianceFinding MapFindingToEntity(string assessmentId, AtoFinding finding)
    {
        return new ComplianceFinding
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessmentId,
            FindingId = finding.Id,
            RuleId = finding.RuleId,
            Title = finding.Title,
            Description = finding.Description,
            Severity = finding.Severity.ToString(),
            ComplianceStatus = finding.ComplianceStatus.ToString(),
            FindingType = finding.FindingType.ToString(),
            ResourceId = finding.ResourceId,
            ResourceType = finding.ResourceType,
            ResourceName = finding.ResourceName,
            ControlId = finding.AffectedControls.FirstOrDefault(),
            ComplianceFrameworks = JsonSerializer.Serialize(finding.ComplianceFrameworks),
            AffectedNistControls = JsonSerializer.Serialize(finding.AffectedNistControls),
            Evidence = finding.Evidence,
            Remediation = finding.RemediationGuidance,
            Metadata = JsonSerializer.Serialize(finding.Metadata),
            IsRemediable = finding.IsRemediable,
            IsAutomaticallyFixable = finding.IsAutoRemediable,
            DetectedAt = finding.DetectedAt,
            ResolvedAt = null
        };
    }

    private static AtoFinding MapEntityToFinding(ComplianceFinding entity)
    {
        return new AtoFinding
        {
            Id = entity.FindingId,
            ResourceId = entity.ResourceId ?? string.Empty,
            ResourceName = entity.ResourceName ?? string.Empty,
            ResourceType = entity.ResourceType ?? string.Empty,
            SubscriptionId = string.Empty, // Not stored in entity
            ResourceGroupName = string.Empty, // Not stored in entity
            FindingType = Enum.TryParse<AtoFindingType>(entity.FindingType, out var type) 
                ? type : AtoFindingType.Security,
            Severity = Enum.TryParse<AtoFindingSeverity>(entity.Severity, out var severity) 
                ? severity : AtoFindingSeverity.Medium,
            ComplianceStatus = Enum.TryParse<AtoComplianceStatus>(entity.ComplianceStatus, out var status) 
                ? status : AtoComplianceStatus.NonCompliant,
            Title = entity.Title,
            Description = entity.Description,
            Recommendation = entity.Remediation ?? string.Empty,
            RuleId = entity.RuleId,
            RemediationGuidance = entity.Remediation ?? string.Empty,
            IsAutoRemediable = entity.IsAutomaticallyFixable,
            AffectedControls = entity.ControlId != null ? new List<string> { entity.ControlId } : new List<string>(),
            AffectedNistControls = DeserializeList(entity.AffectedNistControls),
            ComplianceFrameworks = DeserializeList(entity.ComplianceFrameworks),
            DetectedAt = entity.DetectedAt,
            Evidence = entity.Evidence,
            Metadata = DeserializeDictionary(entity.Metadata),
            IsRemediable = entity.IsRemediable,
            RemediationActions = new List<AtoRemediationAction>(),
            RemediationStatus = entity.ResolvedAt.HasValue 
                ? AtoRemediationStatus.Completed 
                : AtoRemediationStatus.NotStarted
        };
    }

    private AtoComplianceAssessment MapEntityToAssessment(ComplianceAssessment entity)
    {
        var controlFamilyResults = new Dictionary<string, ControlFamilyAssessment>();
        
        if (!string.IsNullOrEmpty(entity.Results))
        {
            try
            {
                var deserialized = JsonSerializer.Deserialize<Dictionary<string, ControlFamilyAssessment>>(entity.Results);
                if (deserialized != null)
                {
                    controlFamilyResults = deserialized;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize control family results for assessment {AssessmentId}", entity.Id);
            }
        }

        return new AtoComplianceAssessment
        {
            AssessmentId = entity.Id,
            SubscriptionId = entity.SubscriptionId,
            StartTime = new DateTimeOffset(entity.StartedAt),
            EndTime = entity.CompletedAt.HasValue 
                ? new DateTimeOffset(entity.CompletedAt.Value) 
                : DateTimeOffset.UtcNow,
            Duration = entity.Duration.HasValue ? TimeSpan.FromTicks(entity.Duration.Value) : TimeSpan.Zero,
            ControlFamilyResults = controlFamilyResults,
            OverallComplianceScore = (double)entity.ComplianceScore,
            TotalFindings = entity.TotalFindings,
            CriticalFindings = entity.CriticalFindings,
            HighFindings = entity.HighFindings,
            MediumFindings = entity.MediumFindings,
            LowFindings = entity.LowFindings,
            ExecutiveSummary = entity.ExecutiveSummary,
            RiskProfile = DeserializeRiskProfile(entity.RiskProfile),
            Error = null
        };
    }

    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static Dictionary<string, object> DeserializeDictionary(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return new Dictionary<string, object>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json) 
                ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    private RiskProfile? DeserializeRiskProfile(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<RiskProfile>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize risk profile");
            return null;
        }
    }

    #endregion
}
