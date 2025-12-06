using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Compliance.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Context;

namespace Platform.Engineering.Copilot.Core.Data.Repositories;

/// <summary>
/// EF Core implementation of compliance assessment repository.
/// Manages ComplianceAssessments and related ComplianceFindings.
/// </summary>
public class ComplianceAssessmentRepository : IComplianceAssessmentRepository
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ILogger<ComplianceAssessmentRepository> _logger;

    public ComplianceAssessmentRepository(
        PlatformEngineeringCopilotContext context,
        ILogger<ComplianceAssessmentRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ==================== Assessment Operations ====================

    public async Task<ComplianceAssessment?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceAssessments
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<ComplianceAssessment?> GetByIdWithFindingsAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceAssessments
            .Include(a => a.Findings)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ComplianceAssessment>> GetBySubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceAssessments
            .Where(a => a.SubscriptionId == subscriptionId)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplianceAssessment>> GetBySubscriptionWithFindingsAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceAssessments
            .Include(a => a.Findings)
            .Where(a => a.SubscriptionId == subscriptionId)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ComplianceAssessment?> GetLatestBySubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceAssessments
            .Include(a => a.Findings)
            .Where(a => a.SubscriptionId == subscriptionId)
            .OrderByDescending(a => a.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ComplianceAssessment?> GetLatestCompletedWithFindingsAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceAssessments
            .Include(a => a.Findings)
            .Where(a => a.SubscriptionId == subscriptionId && a.Status == "Completed")
            .OrderByDescending(a => a.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ComplianceAssessment?> GetBySubscriptionAndDateAsync(string subscriptionId, DateTime date, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceAssessments
            .Where(a => a.SubscriptionId == subscriptionId && 
                       a.Status == "Completed" &&
                       a.CompletedAt != null &&
                       a.CompletedAt.Value.Date == date.Date)
            .OrderByDescending(a => a.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ComplianceAssessment?> GetBySubscriptionAndDateWithFindingsAsync(string subscriptionId, DateTime date, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceAssessments
            .Include(a => a.Findings)
            .Where(a => a.SubscriptionId == subscriptionId && 
                       a.Status == "Completed" &&
                       a.CompletedAt != null &&
                       a.CompletedAt.Value.Date == date.Date)
            .OrderByDescending(a => a.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ComplianceAssessment?> GetPreviousAssessmentAsync(string subscriptionId, DateTime beforeDate, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceAssessments
            .Where(a => a.SubscriptionId == subscriptionId && 
                       a.Status == "Completed" &&
                       a.CompletedAt != null &&
                       a.CompletedAt < beforeDate)
            .OrderByDescending(a => a.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplianceAssessment>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceAssessments
            .Where(a => a.Status == status)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplianceAssessment>> GetByTypeAsync(string assessmentType, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceAssessments
            .Where(a => a.AssessmentType == assessmentType)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplianceAssessment>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceAssessments
            .Where(a => a.StartedAt >= startDate && a.StartedAt <= endDate)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplianceAssessment>> SearchAsync(
        string? subscriptionId = null,
        string? resourceGroup = null,
        string? status = null,
        string? assessmentType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ComplianceAssessments.AsQueryable();

        if (!string.IsNullOrEmpty(subscriptionId))
        {
            query = query.Where(a => a.SubscriptionId == subscriptionId);
        }

        if (!string.IsNullOrEmpty(resourceGroup))
        {
            query = query.Where(a => a.ResourceGroupName == resourceGroup);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrEmpty(assessmentType))
        {
            query = query.Where(a => a.AssessmentType == assessmentType);
        }

        if (startDate.HasValue)
        {
            query = query.Where(a => a.StartedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.StartedAt <= endDate.Value);
        }

        return await query
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceAssessments
            .AnyAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<int> CountBySubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceAssessments
            .CountAsync(a => a.SubscriptionId == subscriptionId, cancellationToken);
    }

    public async Task<ComplianceAssessment> AddAsync(ComplianceAssessment assessment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(assessment.Id))
        {
            assessment.Id = Guid.NewGuid().ToString();
        }

        _context.ComplianceAssessments.Add(assessment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created ComplianceAssessment {AssessmentId} for subscription {SubscriptionId}", 
            assessment.Id, assessment.SubscriptionId);

        return assessment;
    }

    public async Task<ComplianceAssessment> UpdateAsync(ComplianceAssessment assessment, CancellationToken cancellationToken = default)
    {
        _context.ComplianceAssessments.Update(assessment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated ComplianceAssessment {AssessmentId}", assessment.Id);

        return assessment;
    }

    public async Task<bool> UpdateStatusAsync(string id, string status, CancellationToken cancellationToken = default)
    {
        var assessment = await _context.ComplianceAssessments.FindAsync(new object[] { id }, cancellationToken);
        if (assessment == null)
        {
            _logger.LogWarning("Assessment {AssessmentId} not found for status update", id);
            return false;
        }

        assessment.Status = status;
        if (status == "Completed" || status == "Failed" || status == "Cancelled")
        {
            assessment.CompletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated assessment {AssessmentId} status to {Status}", id, status);
        return true;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var assessment = await _context.ComplianceAssessments
            .Include(a => a.Findings)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (assessment == null)
            return false;

        _context.ComplianceAssessments.Remove(assessment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted ComplianceAssessment {AssessmentId} with {FindingCount} findings", 
            id, assessment.Findings.Count);
        return true;
    }

    // ==================== Finding Operations ====================

    public async Task<ComplianceFinding?> GetFindingByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceFindings
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<ComplianceFinding?> GetFindingAsync(string assessmentId, string findingId, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceFindings
            .FirstOrDefaultAsync(f => f.AssessmentId == assessmentId && f.FindingId == findingId, cancellationToken);
    }

    public async Task<IReadOnlyList<ComplianceFinding>> GetFindingsAsync(string assessmentId, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceFindings
            .Where(f => f.AssessmentId == assessmentId)
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplianceFinding>> GetFindingsBySeverityAsync(string assessmentId, string severity, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceFindings
            .Where(f => f.AssessmentId == assessmentId && f.Severity == severity)
            .OrderBy(f => f.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplianceFinding>> GetFindingsByStatusAsync(string assessmentId, string complianceStatus, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceFindings
            .Where(f => f.AssessmentId == assessmentId && f.ComplianceStatus == complianceStatus)
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplianceFinding>> GetFindingsByResourceAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceFindings
            .Where(f => f.ResourceId == resourceId)
            .OrderByDescending(f => f.DetectedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplianceFinding>> GetFindingsByControlAsync(string controlId, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceFindings
            .Where(f => f.ControlId == controlId)
            .OrderByDescending(f => f.DetectedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplianceFinding>> GetUnresolvedFindingsByControlAsync(
        string subscriptionId, 
        string controlId, 
        int limit = 10, 
        CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceFindings
            .Where(f => f.Assessment.SubscriptionId == subscriptionId &&
                       f.ResolvedAt == null &&
                       (f.ControlId == controlId || (f.AffectedNistControls != null && f.AffectedNistControls.Contains(controlId))) &&
                       (f.Severity == "Critical" || f.Severity == "High"))
            .OrderByDescending(f => f.Severity)
            .ThenByDescending(f => f.DetectedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAutoRemediatedFindingsAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceFindings
            .Where(f => f.Assessment.SubscriptionId == subscriptionId &&
                       f.IsAutomaticallyFixable &&
                       f.ResolvedAt != null)
            .CountAsync(cancellationToken);
    }

    public async Task<Dictionary<string, int>> CountFindingsBySeverityAsync(string assessmentId, CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceFindings
            .Where(f => f.AssessmentId == assessmentId)
            .GroupBy(f => f.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Severity, x => x.Count, cancellationToken);
    }

    public async Task<ComplianceFinding> AddFindingAsync(ComplianceFinding finding, CancellationToken cancellationToken = default)
    {
        if (finding.Id == Guid.Empty)
        {
            finding.Id = Guid.NewGuid();
        }

        _context.ComplianceFindings.Add(finding);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Added ComplianceFinding {FindingId} to assessment {AssessmentId}", 
            finding.FindingId, finding.AssessmentId);

        return finding;
    }

    public async Task<IReadOnlyList<ComplianceFinding>> AddFindingsAsync(IEnumerable<ComplianceFinding> findings, CancellationToken cancellationToken = default)
    {
        var findingList = findings.ToList();
        foreach (var finding in findingList)
        {
            if (finding.Id == Guid.Empty)
            {
                finding.Id = Guid.NewGuid();
            }
        }

        await _context.ComplianceFindings.AddRangeAsync(findingList, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Added {Count} ComplianceFindings", findingList.Count);

        return findingList;
    }

    public async Task<ComplianceFinding> UpdateFindingAsync(ComplianceFinding finding, CancellationToken cancellationToken = default)
    {
        _context.ComplianceFindings.Update(finding);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated ComplianceFinding {FindingId}", finding.FindingId);

        return finding;
    }

    public async Task<bool> ResolveFindingAsync(string assessmentId, string findingId, CancellationToken cancellationToken = default)
    {
        var finding = await _context.ComplianceFindings
            .FirstOrDefaultAsync(f => f.AssessmentId == assessmentId && f.FindingId == findingId, cancellationToken);

        if (finding == null)
        {
            _logger.LogWarning("Finding {FindingId} not found in assessment {AssessmentId}", findingId, assessmentId);
            return false;
        }

        finding.ResolvedAt = DateTime.UtcNow;
        finding.ComplianceStatus = "Resolved";

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Resolved finding {FindingId} in assessment {AssessmentId}", findingId, assessmentId);
        return true;
    }

    public async Task<bool> DeleteFindingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var finding = await _context.ComplianceFindings.FindAsync(new object[] { id }, cancellationToken);
        if (finding == null)
            return false;

        _context.ComplianceFindings.Remove(finding);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted ComplianceFinding {FindingId}", id);
        return true;
    }

    public async Task<int> DeleteFindingsAsync(string assessmentId, CancellationToken cancellationToken = default)
    {
        var findings = await _context.ComplianceFindings
            .Where(f => f.AssessmentId == assessmentId)
            .ToListAsync(cancellationToken);

        if (!findings.Any())
            return 0;

        _context.ComplianceFindings.RemoveRange(findings);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted {Count} findings for assessment {AssessmentId}", findings.Count, assessmentId);
        return findings.Count;
    }

    // ==================== Analytics Operations ====================

    public async Task<IReadOnlyList<ComplianceScoreDataPoint>> GetComplianceScoreTrendAsync(
        string subscriptionId, 
        int limit = 10, 
        CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceAssessments
            .Where(a => a.SubscriptionId == subscriptionId && a.Status == "Completed")
            .OrderByDescending(a => a.StartedAt)
            .Take(limit)
            .Select(a => new ComplianceScoreDataPoint
            {
                AssessmentId = a.Id,
                Timestamp = a.StartedAt,
                ComplianceScore = a.ComplianceScore,
                TotalFindings = a.TotalFindings,
                CriticalFindings = a.CriticalFindings
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<FindingStatistics> GetFindingStatisticsAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        var assessments = await _context.ComplianceAssessments
            .Where(a => a.SubscriptionId == subscriptionId)
            .ToListAsync(cancellationToken);

        var findings = await _context.ComplianceFindings
            .Where(f => assessments.Select(a => a.Id).Contains(f.AssessmentId))
            .ToListAsync(cancellationToken);

        var stats = new FindingStatistics
        {
            TotalAssessments = assessments.Count,
            TotalFindings = findings.Count,
            ResolvedFindings = findings.Count(f => f.ResolvedAt.HasValue),
            OpenFindings = findings.Count(f => !f.ResolvedAt.HasValue),
            FindingsBySeverity = findings.GroupBy(f => f.Severity).ToDictionary(g => g.Key, g => g.Count()),
            FindingsByType = findings.GroupBy(f => f.FindingType).ToDictionary(g => g.Key, g => g.Count()),
            FindingsByStatus = findings.GroupBy(f => f.ComplianceStatus).ToDictionary(g => g.Key, g => g.Count()),
            AverageComplianceScore = assessments.Any() 
                ? assessments.Average(a => a.ComplianceScore) 
                : 0
        };

        return stats;
    }
}
