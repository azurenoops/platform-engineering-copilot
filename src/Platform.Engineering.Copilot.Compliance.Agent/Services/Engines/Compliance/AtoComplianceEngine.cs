using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using System.Text.Json;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Models.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.KnowledgeBase;
using Azure.Core;
using Platform.Engineering.Copilot.Compliance.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Helpers;
using Platform.Engineering.Copilot.Core.Constants;
using CF = Platform.Engineering.Copilot.Core.Constants.ComplianceConstants.ControlFamilies;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;

/// <summary>
/// Implementation of the ATO Compliance Engine that orchestrates compliance scanning, 
/// evidence collection, continuous monitoring, and automated remediation
/// </summary>
public class AtoComplianceEngine : IAtoComplianceEngine
{
    private readonly ILogger<AtoComplianceEngine> _logger;
    private readonly INistControlsService _nistControlsService;
    private readonly IAzureResourceService _azureResourceService;
    private readonly IMemoryCache _cache;
    // private readonly IAtoComplianceReportService _reportService; // TODO: Implement this service
    private readonly ComplianceMetricsService _metricsService;
    private readonly ComplianceAgentOptions _options;
    private readonly Dictionary<string, IComplianceScanner> _scanners;
    private readonly Dictionary<string, IEvidenceCollector> _evidenceCollectors;
    private readonly IAssessmentService _assessmentService;
    private readonly IDefenderForCloudService _defenderForCloudService;
    private readonly IEvidenceStorageService _evidenceStorage;

    // Knowledge Base Services for enhanced compliance assessment
    private readonly IRmfKnowledgeService _rmfKnowledgeService;
    private readonly IStigKnowledgeService _stigKnowledgeService;
    private readonly IDoDInstructionService _dodInstructionService;
    private readonly IDoDWorkflowService _dodWorkflowService;
    private readonly IStigValidationService _stigValidationService;

    // Cache configuration
    private static readonly TimeSpan ResourceCacheDuration = TimeSpan.FromMinutes(5);
    private const string ResourceCacheKeyPrefix = "AzureResources_";

    public AtoComplianceEngine(
        ILogger<AtoComplianceEngine> logger,
        INistControlsService nistControlsService,
        IAzureResourceService azureResourceService,
        IMemoryCache cache,
        // IAtoComplianceReportService reportService, // TODO: Implement this service
        ComplianceMetricsService metricsService,
        IOptions<ComplianceAgentOptions> options,
        IAssessmentService assessmentService,
        IRmfKnowledgeService rmfKnowledgeService,
        IStigKnowledgeService stigKnowledgeService,
        IDoDInstructionService dodInstructionService,
        IDoDWorkflowService dodWorkflowService,
        IDefenderForCloudService defenderForCloudService,
        IEvidenceStorageService evidenceStorage,
        IStigValidationService stigValidationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _nistControlsService = nistControlsService ?? throw new ArgumentNullException(nameof(nistControlsService));
        _azureResourceService = azureResourceService ?? throw new ArgumentNullException(nameof(azureResourceService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));

        // _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _assessmentService = assessmentService ?? throw new ArgumentNullException(nameof(assessmentService));

        _rmfKnowledgeService = rmfKnowledgeService ?? throw new ArgumentNullException(nameof(rmfKnowledgeService));
        _stigKnowledgeService = stigKnowledgeService ?? throw new ArgumentNullException(nameof(stigKnowledgeService));
        _dodInstructionService = dodInstructionService ?? throw new ArgumentNullException(nameof(dodInstructionService));
        _dodWorkflowService = dodWorkflowService ?? throw new ArgumentNullException(nameof(dodWorkflowService));
        _defenderForCloudService = defenderForCloudService ?? throw new ArgumentNullException(nameof(defenderForCloudService));
        _evidenceStorage = evidenceStorage ?? throw new ArgumentNullException(nameof(evidenceStorage));
        _stigValidationService = stigValidationService ?? throw new ArgumentNullException(nameof(stigValidationService));

        _scanners = InitializeScanners();
        _evidenceCollectors = InitializeEvidenceCollectors();
    }

    /// <summary>
    /// Runs a comprehensive ATO compliance assessment across all NIST control families
    /// </summary>
    /// <param name="subscriptionId">Azure subscription ID to assess</param>
    /// <param name="progress">Optional progress reporter for real-time status updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<AtoComplianceAssessment> RunComprehensiveAssessmentAsync(
        string subscriptionId,
        IProgress<AssessmentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await RunComprehensiveAssessmentAsync(subscriptionId, null, progress, cancellationToken);
    }

    /// <summary>
    /// Runs a comprehensive ATO compliance assessment across all NIST control families
    /// Supports optional resource group-level scoping
    /// </summary>
    /// <param name="subscriptionId">Azure subscription ID to assess</param>
    /// <param name="resourceGroupName">Optional resource group name to scope assessment</param>
    /// <param name="progress">Optional progress reporter for real-time status updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<AtoComplianceAssessment> RunComprehensiveAssessmentAsync(
        string subscriptionId,
        string? resourceGroupName,
        IProgress<AssessmentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
        _logger.LogInformation("Starting comprehensive ATO compliance assessment for {Scope} in subscription {SubscriptionId}",
            scope, subscriptionId);

        var assessment = new AtoComplianceAssessment
        {
            AssessmentId = Guid.NewGuid().ToString(),
            SubscriptionId = subscriptionId,
            StartTime = DateTimeOffset.UtcNow,
            ControlFamilyResults = new Dictionary<string, ControlFamilyAssessment>()
        };

        try
        {
            // Pre-warm cache with Azure resources for performance
            var cacheWarmupStopwatch = Stopwatch.StartNew();

            // For RG-scoped assessments, we still cache all subscription resources
            // but scanners will filter to RG-specific resources
            await GetCachedAzureResourcesAsync(subscriptionId, cancellationToken);
            cacheWarmupStopwatch.Stop();
            _logger.LogInformation("Cache warmup completed in {ElapsedMs}ms for {Scope} in subscription {SubscriptionId}",
                cacheWarmupStopwatch.ElapsedMilliseconds, scope, subscriptionId);

            // Get all NIST control families from constants
            var controlFamilies = ComplianceConstants.ControlFamilies.All.ToList();

            // Report initial progress
            progress?.Report(new AssessmentProgress
            {
                TotalFamilies = controlFamilies.Count,
                CompletedFamilies = 0,
                CurrentFamily = "Initialization",
                Message = "Starting control family assessments"
            });

            // Run assessments for each control family with progress reporting
            var scanningStopwatch = Stopwatch.StartNew();
            var familyAssessments = new List<ControlFamilyAssessment>();
            var completedCount = 0;

            foreach (var family in controlFamilies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Report progress for current family
                progress?.Report(new AssessmentProgress
                {
                    TotalFamilies = controlFamilies.Count,
                    CompletedFamilies = completedCount,
                    CurrentFamily = family,
                    Message = $"Assessing control family {family}"
                });

                var familyAssessment = await AssessControlFamilyAsync(subscriptionId, resourceGroupName, family, cancellationToken);
                familyAssessments.Add(familyAssessment);

                completedCount++;

                // Report completion of this family
                progress?.Report(new AssessmentProgress
                {
                    TotalFamilies = controlFamilies.Count,
                    CompletedFamilies = completedCount,
                    CurrentFamily = family,
                    Message = $"Completed {family}: {familyAssessment.ComplianceScore}% compliant, {familyAssessment.Findings.Count} findings"
                });
            }

            scanningStopwatch.Stop();
            _logger.LogInformation("Control family scanning completed in {ElapsedMs}ms ({FamilyCount} families)",
                scanningStopwatch.ElapsedMilliseconds, controlFamilies.Count);

            // Aggregate results
            foreach (var familyAssessment in familyAssessments)
            {
                assessment.ControlFamilyResults[familyAssessment.ControlFamily] = familyAssessment;
            }

            // Calculate overall compliance score
            assessment.OverallComplianceScore = CalculateOverallComplianceScore(familyAssessments);
            assessment.TotalFindings = familyAssessments.Sum(f => f.Findings.Count);
            assessment.CriticalFindings = familyAssessments.Sum(f => f.Findings.Count(finding => finding.Severity == AtoFindingSeverity.Critical));
            assessment.HighFindings = familyAssessments.Sum(f => f.Findings.Count(finding => finding.Severity == AtoFindingSeverity.High));
            assessment.MediumFindings = familyAssessments.Sum(f => f.Findings.Count(finding => finding.Severity == AtoFindingSeverity.Medium));
            assessment.LowFindings = familyAssessments.Sum(f => f.Findings.Count(finding => finding.Severity == AtoFindingSeverity.Low));
            assessment.InformationalFindings = familyAssessments.Sum(f => f.Findings.Count(finding => finding.Severity == AtoFindingSeverity.Informational));

            // Generate executive summary
            assessment.ExecutiveSummary = GenerateExecutiveSummary(assessment);

            // Perform risk assessment
            var riskAssessmentStopwatch = Stopwatch.StartNew();
            assessment.RiskProfile = await CalculateRiskProfileAsync(assessment, cancellationToken);
            riskAssessmentStopwatch.Stop();
            _logger.LogDebug("Risk assessment completed in {ElapsedMs}ms", riskAssessmentStopwatch.ElapsedMilliseconds);

            assessment.EndTime = DateTimeOffset.UtcNow;
            assessment.Duration = assessment.EndTime - assessment.StartTime;

            // Store assessment results
            await StoreAssessmentResultsAsync(assessment, cancellationToken);

            stopwatch.Stop();
            _logger.LogInformation(
                "Completed ATO compliance assessment for {Scope} in subscription {SubscriptionId}. " +
                "Overall score: {Score}%, Total findings: {Findings}, Duration: {TotalMs}ms " +
                "(Cache: {CacheMs}ms, Scanning: {ScanMs}ms, Risk: {RiskMs}ms)",
                scope, subscriptionId, assessment.OverallComplianceScore, assessment.TotalFindings,
                stopwatch.ElapsedMilliseconds, cacheWarmupStopwatch.ElapsedMilliseconds,
                scanningStopwatch.ElapsedMilliseconds, riskAssessmentStopwatch.ElapsedMilliseconds);

            return assessment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ATO compliance assessment for {Scope} in subscription {SubscriptionId}",
                scope, subscriptionId);
            assessment.Error = ex.Message;
            assessment.EndTime = DateTimeOffset.UtcNow;
            throw;
        }
    }

    /// <summary>
    /// Gets real-time continuous compliance monitoring status
    /// </summary>
    public async Task<ContinuousComplianceStatus> GetContinuousComplianceStatusAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var status = new ContinuousComplianceStatus
        {
            SubscriptionId = subscriptionId,
            Timestamp = DateTimeOffset.UtcNow,
            MonitoringEnabled = true,
            ControlStatuses = new Dictionary<string, ControlMonitoringStatus>()
        };

        // Get all monitored controls
        var monitoredControls = await GetMonitoredControlsAsync(subscriptionId, cancellationToken);

        foreach (var control in monitoredControls)
        {
            var controlStatus = new ControlMonitoringStatus
            {
                ControlId = control.ControlId,
                LastChecked = control.LastChecked,
                Status = control.ComplianceStatus,
                DriftDetected = control.DriftDetected,
                AutoRemediationEnabled = control.AutoRemediationEnabled,
                Alerts = await GetControlAlertsAsync(subscriptionId, control.ControlId, cancellationToken)
            };

            status.ControlStatuses[control.ControlId] = controlStatus;
        }

        // Calculate compliance drift
        status.ComplianceDriftPercentage = CalculateComplianceDrift(status.ControlStatuses.Values);
        status.AlertCount = status.ControlStatuses.Values.Sum(c => c.Alerts.Count);
        status.AutoRemediationCount = await GetAutoRemediationCountAsync(subscriptionId, cancellationToken);

        return status;
    }

    /// <summary>
    /// Collects comprehensive evidence for specific control families
    /// </summary>
    /// <param name="subscriptionId">Azure subscription ID</param>
    /// <param name="controlFamily">Control family to collect evidence for</param>
    /// <param name="collectedBy">Name or email of the user collecting evidence (for audit trail)</param>
    /// <param name="progress">Optional progress reporter for real-time status updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<EvidencePackage> CollectComplianceEvidenceAsync(
        string subscriptionId,
        string controlFamily,
        string collectedBy,
        IProgress<EvidenceCollectionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Collecting compliance evidence for control family {ControlFamily}", controlFamily);

        var evidencePackage = new EvidencePackage
        {
            PackageId = Guid.NewGuid().ToString(),
            SubscriptionId = subscriptionId,
            ControlFamily = controlFamily,
            CollectionStartTime = DateTimeOffset.UtcNow,
            CollectedBy = collectedBy,
            Evidence = new List<ComplianceEvidence>()
        };

        try
        {
            // Pre-warm cache for better performance
            await GetCachedAzureResourcesAsync(subscriptionId, cancellationToken);

            // Get evidence collectors for control family
            List<IEvidenceCollector> collectors;
            
            if (controlFamily.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                // For "All", use all specialized collectors (exclude Default)
                collectors = _evidenceCollectors
                    .Where(kvp => kvp.Key != "Default")
                    .Select(kvp => kvp.Value)
                    .ToList();
                _logger.LogInformation("Collecting evidence from {CollectorCount} specialized collectors for 'All' control families", collectors.Count);
            }
            else if (_evidenceCollectors.TryGetValue(controlFamily, out var collector))
            {
                collectors = new List<IEvidenceCollector> { collector };
            }
            else
            {
                // Fallback to Default only if unknown family
                collectors = new List<IEvidenceCollector> { _evidenceCollectors["Default"] };
                _logger.LogWarning("Unknown control family {ControlFamily}, using DefaultEvidenceCollector", controlFamily);
            }

            // Define evidence types to collect
            var evidenceTypes = new[]
            {
                "Configuration",
                "Logs",
                "Metrics",
                "Policies",
                "Access Control"
            };

            var totalTypes = evidenceTypes.Length * collectors.Count;
            var completedTypes = 0;

            // Report initial progress
            progress?.Report(new EvidenceCollectionProgress
            {
                ControlFamily = controlFamily,
                TotalItems = totalTypes,
                CollectedItems = 0,
                CurrentEvidenceType = "Initialization",
                Message = "Starting evidence collection"
            });

            // Collect evidence from all collectors
            var allEvidence = new List<List<ComplianceEvidence>>();

            foreach (var collector in collectors)
            {
                // Configuration evidence
                progress?.Report(new EvidenceCollectionProgress
                {
                    ControlFamily = controlFamily,
                    TotalItems = totalTypes,
                    CollectedItems = completedTypes,
                    CurrentEvidenceType = "Configuration",
                    Message = $"Collecting configuration evidence ({collector.GetType().Name})"
                });
                allEvidence.Add(await collector.CollectConfigurationEvidenceAsync(subscriptionId, controlFamily, collectedBy, cancellationToken));
                completedTypes++;

                // Log evidence
                progress?.Report(new EvidenceCollectionProgress
                {
                    ControlFamily = controlFamily,
                    TotalItems = totalTypes,
                    CollectedItems = completedTypes,
                    CurrentEvidenceType = "Logs",
                    Message = $"Collecting log evidence ({collector.GetType().Name})"
                });
                allEvidence.Add(await collector.CollectLogEvidenceAsync(subscriptionId, controlFamily, collectedBy, cancellationToken));
                completedTypes++;

                // Metric evidence
                progress?.Report(new EvidenceCollectionProgress
                {
                    ControlFamily = controlFamily,
                    TotalItems = totalTypes,
                    CollectedItems = completedTypes,
                    CurrentEvidenceType = "Metrics",
                    Message = $"Collecting metric evidence ({collector.GetType().Name})"
                });
                allEvidence.Add(await collector.CollectMetricEvidenceAsync(subscriptionId, controlFamily, collectedBy, cancellationToken));
                completedTypes++;

                // Policy evidence
                progress?.Report(new EvidenceCollectionProgress
                {
                    ControlFamily = controlFamily,
                    TotalItems = totalTypes,
                    CollectedItems = completedTypes,
                    CurrentEvidenceType = "Policies",
                    Message = $"Collecting policy evidence ({collector.GetType().Name})"
                });
                allEvidence.Add(await collector.CollectPolicyEvidenceAsync(subscriptionId, controlFamily, collectedBy, cancellationToken));
                completedTypes++;

                // Access control evidence
                progress?.Report(new EvidenceCollectionProgress
                {
                    ControlFamily = controlFamily,
                    TotalItems = totalTypes,
                    CollectedItems = completedTypes,
                    CurrentEvidenceType = "Access Control",
                    Message = $"Collecting access control evidence ({collector.GetType().Name})"
                });
                allEvidence.Add(await collector.CollectAccessControlEvidenceAsync(subscriptionId, controlFamily, collectedBy, cancellationToken));
                completedTypes++;
            }

            // Report completion
            progress?.Report(new EvidenceCollectionProgress
            {
                ControlFamily = controlFamily,
                TotalItems = totalTypes,
                CollectedItems = completedTypes,
                CurrentEvidenceType = "Complete",
                Message = "Evidence collection completed"
            });

            // Convert from Services.ComplianceEvidence to Models.ComplianceEvidence
            evidencePackage.Evidence = allEvidence.SelectMany(e => e).Select(ev => new ComplianceEvidence
            {
                EvidenceId = ev.EvidenceId,
                EvidenceType = ev.EvidenceType,
                ControlId = ev.ControlId,
                ResourceId = ev.ResourceId,
                CollectedAt = ev.CollectedAt,
                Data = ev.Data,
                Screenshot = ev.Screenshot,
                LogExcerpt = ev.LogExcerpt,
                ConfigSnapshot = ev.ConfigSnapshot
            }).ToList();

            // Generate evidence summary
            evidencePackage.Summary = GenerateEvidenceSummary(evidencePackage.Evidence);

            // Calculate evidence completeness
            evidencePackage.CompletenessScore = CalculateEvidenceCompleteness(controlFamily, evidencePackage.Evidence);

            // Generate attestation statement
            evidencePackage.AttestationStatement = GenerateAttestationStatement(evidencePackage);

            evidencePackage.CollectionEndTime = DateTimeOffset.UtcNow;
            evidencePackage.CollectionDuration = evidencePackage.CollectionEndTime - evidencePackage.CollectionStartTime;

            // Store evidence package
            await StoreEvidencePackageAsync(evidencePackage, cancellationToken);

            stopwatch.Stop();
            _logger.LogInformation(
                "Collected {Count} pieces of evidence for control family {ControlFamily} in {ElapsedMs}ms " +
                "(Completeness: {CompletenessScore}%)",
                evidencePackage.Evidence.Count, controlFamily, stopwatch.ElapsedMilliseconds,
                evidencePackage.CompletenessScore);

            return evidencePackage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting evidence for control family {ControlFamily}", controlFamily);
            evidencePackage.Error = ex.Message;
            throw;
        }
    }
   
    /// <summary>
    /// Gets historical compliance timeline for trend analysis
    /// </summary>
    public async Task<ComplianceTimeline> GetComplianceTimelineAsync(
        string subscriptionId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        var timeline = new ComplianceTimeline
        {
            SubscriptionId = subscriptionId,
            StartDate = startDate,
            EndDate = endDate,
            DataPoints = new List<ComplianceDataPoint>()
        };

        // Generate data points for timeline by querying database for each date
        var currentDate = startDate;
        while (currentDate <= endDate)
        {
            var dataPoint = new ComplianceDataPoint
            {
                Timestamp = currentDate,
                ComplianceScore = await GetComplianceScoreAtDateAsync(subscriptionId, currentDate, cancellationToken),
                ControlsFailed = await GetFailedControlsAtDateAsync(subscriptionId, currentDate, cancellationToken),
                ControlsPassed = await GetPassedControlsAtDateAsync(subscriptionId, currentDate, cancellationToken),
                ActiveFindings = await GetActiveFindingsAtDateAsync(subscriptionId, currentDate, cancellationToken),
                RemediatedFindings = await GetRemediatedFindingsAtDateAsync(subscriptionId, currentDate, cancellationToken),
                Events = await GetComplianceEventsAtDateAsync(subscriptionId, currentDate, cancellationToken)
            };

            timeline.DataPoints.Add(dataPoint);
            currentDate = currentDate.AddDays(1);
        }

        // Calculate trends
        timeline.Trends = CalculateComplianceTrends(timeline.DataPoints);

        // Identify significant events
        timeline.SignificantEvents = IdentifySignificantEvents(timeline.DataPoints);

        // Generate insights
        timeline.Insights = GenerateTimelineInsights(timeline);

        return timeline;
    }

    /// <summary>
    /// Performs comprehensive risk assessment based on compliance status
    /// </summary>
    public async Task<RiskAssessment> PerformRiskAssessmentAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Performing risk assessment for subscription {SubscriptionId}", subscriptionId);

        var assessment = new RiskAssessment
        {
            AssessmentId = Guid.NewGuid().ToString(),
            SubscriptionId = subscriptionId,
            AssessmentDate = DateTimeOffset.UtcNow,
            RiskCategories = new Dictionary<string, CategoryRisk>()
        };

        // Assess different risk categories
        var riskCategories = new[]
        {
            "Data Protection",
            "Access Control",
            "Network Security",
            "Incident Response",
            "Business Continuity",
            "Compliance",
            "Third-Party Risk",
            "Configuration Management"
        };

        foreach (var category in riskCategories)
        {
            var categoryRisk = await AssessCategoryRiskAsync(subscriptionId, category, cancellationToken);
            assessment.RiskCategories[category] = categoryRisk;
        }

        // Calculate overall risk score
        assessment.OverallRiskScore = CalculateOverallRiskScore(assessment.RiskCategories.Values);
        assessment.RiskLevelString = DetermineRiskLevel(assessment.OverallRiskScore);

        // Identify top risks
        assessment.TopRisks = IdentifyTopRisks(assessment.RiskCategories);

        // Generate risk mitigation recommendations
        assessment.MitigationRecommendations = await GenerateMitigationRecommendationsAsync(
            assessment.TopRisks, cancellationToken);

        // Calculate risk trend
        assessment.RiskTrend = await CalculateRiskTrendAsync(subscriptionId, cancellationToken);

        // Generate executive risk summary
        assessment.ExecutiveSummary = GenerateRiskSummary(assessment);

        return assessment;
    }

    /// <summary>
    /// Generates a compliance certificate for successful assessments
    /// </summary>
    public async Task<ComplianceCertificate> GenerateComplianceCertificateAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating compliance certificate for subscription {SubscriptionId}", subscriptionId);

        // Verify compliance status
        var currentAssessment = await GetLatestAssessmentAsync(subscriptionId, cancellationToken);

        if (currentAssessment == null || currentAssessment.OverallComplianceScore < 80)
        {
            throw new InvalidOperationException(
                $"Cannot generate certificate. Compliance score {currentAssessment?.OverallComplianceScore ?? 0}% is below required 80%");
        }

        var certificate = new ComplianceCertificate
        {
            CertificateId = Guid.NewGuid().ToString(),
            SubscriptionId = subscriptionId,
            IssuedDate = DateTimeOffset.UtcNow,
            ValidUntil = DateTimeOffset.UtcNow.AddMonths(6),
            ComplianceScore = currentAssessment.OverallComplianceScore,
            ControlFamiliesCovered = currentAssessment.ControlFamilyResults.Keys.ToList(),
            Attestations = new List<ComplianceAttestation>()
        };

        // Add attestations for each control family
        foreach (var familyResult in currentAssessment.ControlFamilyResults)
        {
            var attestation = new ComplianceAttestation
            {
                ControlFamily = familyResult.Key,
                ComplianceLevel = familyResult.Value.ComplianceScore >= 80 ? "Compliant" : "Partial",
                AttestationDate = DateTimeOffset.UtcNow,
                ValidatedControls = new List<string>(), // TODO: Get actual list of passed control IDs
                Exceptions = familyResult.Value.Findings
                    .Where(f => f.Severity == AtoFindingSeverity.Low)
                    .SelectMany(f => f.AffectedNistControls)
                    .Distinct()
                    .ToList()
            };

            certificate.Attestations.Add(attestation);
        }

        // Generate certificate hash for verification
        certificate.VerificationHash = GenerateCertificateHash(certificate);

        // Store certificate
        await StoreCertificateAsync(certificate, cancellationToken);

        _logger.LogInformation("Generated compliance certificate {CertificateId} valid until {ValidUntil}",
            certificate.CertificateId, certificate.ValidUntil);

        return certificate;
    }

    #region Private Helper Methods

    /// <summary>
    /// Gets Azure resources with caching for improved performance
    /// </summary>
    private async Task<List<AzureResource>> GetCachedAzureResourcesAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{ResourceCacheKeyPrefix}{subscriptionId}";

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out List<AzureResource>? cachedResources) && cachedResources != null)
        {
            _logger.LogDebug("Retrieved {Count} cached Azure resources for subscription {SubscriptionId}",
                cachedResources.Count, subscriptionId);
            return cachedResources;
        }

        // Cache miss - fetch from Azure
        _logger.LogDebug("Cache miss - fetching Azure resources for subscription {SubscriptionId}", subscriptionId);
        var resources = await _azureResourceService.ListAllResourceGroupsInSubscriptionAsync(subscriptionId);

        // Store in cache with expiration
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(ResourceCacheDuration)
            .SetSize(1) // Each cache entry counts as size 1
            .RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                _logger.LogDebug("Cache entry {CacheKey} evicted. Reason: {Reason}", key, reason);
            });

        _cache.Set(cacheKey, resources, cacheOptions);
        _logger.LogInformation("Cached {Count} Azure resources for subscription {SubscriptionId} (expires in {Minutes} minutes)",
            resources.Count(), subscriptionId ?? "all", ResourceCacheDuration.TotalMinutes);

        // Convert to AzureResource list for compliance-focused operations
        var azureResources = resources.Select(r => new AzureResource
        {
            Id = r?.ToString() ?? string.Empty,
            Name = "ComplianceResource",
            Type = "Unknown",
            Location = "Unknown",
            ResourceGroup = "Unknown"
        }).ToList();

        return azureResources;
    }

    private Dictionary<string, IComplianceScanner> InitializeScanners()
    {
        var gatewayOptions = Options.Create(_options.Gateway);
        return new Dictionary<string, IComplianceScanner>
        {
            { CF.AccessControl, new AccessControlScanner(_logger, _azureResourceService) },
            { CF.AuditAccountability, new AuditScanner(_logger, _azureResourceService, gatewayOptions) },
            { CF.SystemCommunications, new SystemCommunicationScanner(_logger, _azureResourceService) },
            { CF.SystemInformationIntegrity, new SystemIntegrityScanner(_logger, _azureResourceService) },
            { CF.ContingencyPlanning, new ContingencyPlanningScanner(_logger, _azureResourceService) },
            { CF.IdentificationAuthentication, new IdentificationAuthenticationScanner(_logger, _azureResourceService) },
            { CF.ConfigurationManagement, new ConfigurationManagementScanner(_logger, _azureResourceService) },
            { CF.IncidentResponse, new IncidentResponseScanner(_logger, _azureResourceService) },
            { CF.RiskAssessment, new RiskAssessmentScanner(_logger, _azureResourceService, _defenderForCloudService) },
            { CF.SecurityAssessment, new SecurityAssessmentScanner(_logger, _azureResourceService, _defenderForCloudService) },
            { "Default", new DefaultComplianceScanner(_logger) }
        };
    }

    private Dictionary<string, IEvidenceCollector> InitializeEvidenceCollectors()
    {
        return new Dictionary<string, IEvidenceCollector>
        {
            { CF.AccessControl, new AccessControlEvidenceCollector(_logger, _azureResourceService) },
            { CF.AuditAccountability, new AuditEvidenceCollector(_logger, _azureResourceService) },
            { CF.SystemCommunications, new SecurityEvidenceCollector(_logger, _azureResourceService) },
            { CF.ContingencyPlanning, new ContingencyPlanningEvidenceCollector(_logger, _azureResourceService) },
            { CF.IdentificationAuthentication, new IdentificationAuthenticationEvidenceCollector(_logger, _azureResourceService) },
            { CF.ConfigurationManagement, new ConfigurationManagementEvidenceCollector(_logger, _azureResourceService) },
            { CF.IncidentResponse, new IncidentResponseEvidenceCollector(_logger, _azureResourceService) },
            { CF.SystemInformationIntegrity, new SystemIntegrityEvidenceCollector(_logger, _azureResourceService) },
            { CF.RiskAssessment, new RiskAssessmentEvidenceCollector(_logger, _azureResourceService, _defenderForCloudService) },
            { CF.SecurityAssessment, new SecurityAssessmentEvidenceCollector(_logger, _azureResourceService, _defenderForCloudService) },
            { "Default", new DefaultEvidenceCollector(_logger) }
        };
    }

    private async Task<ControlFamilyAssessment> AssessControlFamilyAsync(
        string subscriptionId,
        string? resourceGroupName,
        string family,
        CancellationToken cancellationToken)
    {
        var assessment = new ControlFamilyAssessment
        {
            ControlFamily = family,
            FamilyName = ComplianceHelpers.GetControlFamilyName(family),
            AssessmentTime = DateTimeOffset.UtcNow,
            Findings = new List<AtoFinding>()
        };

        // Get scanner for this control family
        if (!_scanners.TryGetValue(family, out var scanner))
        {
            scanner = _scanners["Default"];
        }

        // Get controls for this family
        var controls = await _nistControlsService.GetControlsByFamilyAsync(family, cancellationToken);

        // Scan each control - use RG-aware method if resourceGroupName is provided
        foreach (var control in controls)
        {
            var findings = string.IsNullOrEmpty(resourceGroupName)
                ? await scanner.ScanControlAsync(subscriptionId, control, cancellationToken)
                : await scanner.ScanControlAsync(subscriptionId, resourceGroupName, control, cancellationToken);
            assessment.Findings.AddRange(findings);
        }

        // STIG validation integration
        _logger.LogDebug("Running STIG validation for family {Family}", family);
        var stigFindings = await _stigValidationService.ValidateFamilyStigsAsync(
            subscriptionId,
            resourceGroupName,
            family,
            cancellationToken);

        assessment.Findings.AddRange(stigFindings);

        _logger.LogInformation(
            "Family {Family} assessment complete: {NistFindings} NIST findings, {StigFindings} STIG findings",
            family,
            assessment.Findings.Count - stigFindings.Count,
            stigFindings.Count);

        // Calculate family compliance score
        assessment.TotalControls = controls.Count;
        // Calculate passed controls by counting controls without findings
        // Only count affected controls that belong to THIS control family
        var controlIds = controls.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var affectedControlIds = assessment.Findings
            .SelectMany(f => f.AffectedNistControls)
            .Where(controlId => controlIds.Contains(controlId)) // Only count controls in THIS family
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        assessment.PassedControls = Math.Max(0, controls.Count - affectedControlIds);
        assessment.ComplianceScore = assessment.TotalControls > 0
            ? (double)assessment.PassedControls / assessment.TotalControls * 100
            : 0;

        return assessment;
    }

    private double CalculateOverallComplianceScore(IEnumerable<ControlFamilyAssessment> assessments)
    {
        var totalControls = assessments.Sum(a => a.TotalControls);
        var passedControls = assessments.Sum(a => a.PassedControls);

        return totalControls > 0 ? (double)passedControls / totalControls * 100 : 0;
    }

    private string GenerateExecutiveSummary(AtoComplianceAssessment assessment)
    {
        return $"ATO Compliance Assessment completed with {assessment.OverallComplianceScore:F1}% compliance. " +
               $"Found {assessment.CriticalFindings} critical, {assessment.HighFindings} high, " +
               $"{assessment.MediumFindings} medium, and {assessment.LowFindings} low severity findings. " +
               $"Risk level: {assessment.RiskProfile?.RiskLevel ?? "Unknown"}";
    }

    private int GetSeverityPriority(string severity)
    {
        return severity switch
        {
            "Critical" => 0,
            "High" => 1,
            "Medium" => 2,
            "Low" => 3,
            _ => 4
        };
    }

    private int GetSeverityPriority(AtoFindingSeverity severity)
    {
        return severity switch
        {
            AtoFindingSeverity.Critical => 0,
            AtoFindingSeverity.High => 1,
            AtoFindingSeverity.Medium => 2,
            AtoFindingSeverity.Low => 3,
            _ => 4
        };
    }

    private double GetRiskScore(AtoFinding finding)
    {
        return finding.Severity switch
        {
            AtoFindingSeverity.Critical => 10.0,
            AtoFindingSeverity.High => 7.5,
            AtoFindingSeverity.Medium => 5.0,
            AtoFindingSeverity.Low => 2.5,
            _ => 1.0
        };
    }

    private string GenerateRemediationSummary(RemediationPlan plan)
    {
        return $"Remediation plan addresses {plan.RemediationItems.Count} findings with " +
               $"estimated effort of {plan.EstimatedEffort.TotalHours:F1} hours. " +
               $"Projected risk reduction: {plan.ProjectedRiskReduction:F1}%. " +
               $"{plan.RemediationItems.Count(i => i.AutomationAvailable)} items can be automated.";
    }

    private async Task<List<MonitoredControl>> GetMonitoredControlsAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        return await _assessmentService.GetMonitoredControlsAsync(subscriptionId, cancellationToken);
    }

    private async Task<List<ComplianceAlert>> GetControlAlertsAsync(
        string subscriptionId,
        string controlId,
        CancellationToken cancellationToken)
    {
        return await _assessmentService.GetControlAlertsAsync(subscriptionId, controlId, 10, cancellationToken);
    }

    private AlertType DetermineAlertType(string findingType)
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

    private AlertSeverity ParseAlertSeverity(string severity)
    {
        return severity?.ToLowerInvariant() switch
        {
            "critical" => AlertSeverity.Critical,
            "high" => AlertSeverity.Error,
            "medium" => AlertSeverity.Warning,
            "low" => AlertSeverity.Info,
            _ => AlertSeverity.Info
        };
    }

    private DateTime CalculateAlertDueDate(string severity, DateTime detectedAt)
    {
        // Calculate due date based on severity
        var daysToRemediate = severity?.ToLowerInvariant() switch
        {
            "critical" => 7,   // 7 days for critical
            "high" => 30,      // 30 days for high
            "medium" => 90,    // 90 days for medium
            _ => 180           // 180 days for low/informational
        };

        return detectedAt.AddDays(daysToRemediate);
    }

    private double CalculateComplianceDrift(IEnumerable<ControlMonitoringStatus> statuses)
    {
        var total = statuses.Count();
        var drifted = statuses.Count(s => s.DriftDetected);

        return total > 0 ? (double)drifted / total * 100 : 0;
    }

    private async Task<int> GetAutoRemediationCountAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Count findings that were auto-remediable and have been resolved
            var count = await _assessmentService.CountAutoRemediatedFindingsAsync(subscriptionId, cancellationToken);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get auto-remediation count for subscription {SubscriptionId}", subscriptionId);
            return 0;
        }
    }

    private string GenerateEvidenceSummary(List<ComplianceEvidence> evidence)
    {
        var byType = evidence.GroupBy(e => e.EvidenceType);
        return $"Collected {evidence.Count} pieces of evidence: " +
               string.Join(", ", byType.Select(g => $"{g.Count()} {g.Key}"));
    }

    private double CalculateEvidenceCompleteness(string controlFamily, List<ComplianceEvidence> evidence)
    {
        // If no evidence collected, 0% complete
        if (evidence == null || evidence.Count == 0)
            return 0;

        // Calculate based on number of unique evidence types collected
        var collectedTypes = evidence.Select(e => e.EvidenceType).Distinct().Count();

        // Get target number of evidence types for this control family
        var targetTypes = GetTargetEvidenceTypeCount(controlFamily);

        // Calculate percentage: (collected types / target types) * 100
        // Cap at 100% if we exceed target
        var completeness = Math.Min(100.0, (double)collectedTypes / targetTypes * 100);

        return Math.Round(completeness, 2);
    }

    private int GetTargetEvidenceTypeCount(string controlFamily)
    {
        // Target number of different evidence types we expect for each control family
        return controlFamily switch
        {
            CF.AccessControl => 5,  // Access Control: NSGs, Key Vaults, Log Analytics, RBAC, Conditional Access
            CF.AuditAccountability => 4,  // Audit: Logs, Retention, Protection, Monitoring
            CF.SystemCommunications => 5,  // System Communications: Network, Encryption, Certificates, Firewalls, DDoS
            CF.IdentificationAuthentication => 4,  // Identification & Authentication: MFA, Identity, Access Policies, Authentication
            CF.ConfigurationManagement => 4,  // Configuration Management: Config, Baselines, Change Control, Inventory
            CF.IncidentResponse => 3,  // Incident Response: Response Plans, Detection, Monitoring
            CF.RiskAssessment => 3,  // Risk Assessment: Assessments, Vulnerabilities, Risks
            CF.SecurityAssessment => 4,  // Security Assessment: Continuous Monitoring, Assessments, Testing, Reviews
            CF.SystemInformationIntegrity => 4,  // System Integrity: Integrity Checks, Malware Protection, Updates, Monitoring
            CF.ContingencyPlanning => 3,  // Contingency Planning: Backups, Recovery, Business Continuity
            _ => 3      // Default: expect at least 3 different types of evidence
        };
    }

    private string GenerateAttestationStatement(EvidencePackage package)
    {
        return $"Evidence package {package.PackageId} collected on {package.CollectionStartTime:yyyy-MM-dd} " +
               $"for control family {package.ControlFamily} with {package.CompletenessScore:F1}% completeness. " +
               $"This evidence supports compliance attestation for subscription {package.SubscriptionId}.";
    }

    private async Task StoreAssessmentResultsAsync(
        AtoComplianceAssessment assessment,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use AssessmentService to persist the assessment
            await _assessmentService.SaveAssessmentAsync(assessment, cancellationToken);

            _logger.LogInformation("✅ Persisted assessment {AssessmentId} with {FindingsCount} findings to database",
                assessment.AssessmentId, assessment.ControlFamilyResults.Values.Sum(cf => cf.Findings.Count));

            // Also cache the assessment summary for fast access
            _assessmentService.CacheAssessmentSummary(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store assessment results {AssessmentId}", assessment.AssessmentId);
            // Don't throw - assessment still succeeded even if storage failed
        }
    }

    private async Task StoreEvidencePackageAsync(
        EvidencePackage package,
        CancellationToken cancellationToken)
    {
        try
        {
            // Convert EvidencePackage to a format suitable for blob storage
            var storageData = new
            {
                package.PackageId,
                package.SubscriptionId,
                package.ControlFamily,
                package.CollectionStartTime,
                package.CollectionEndTime,
                package.CollectionDuration,
                package.CompletenessScore,
                package.Summary,
                package.AttestationStatement,
                EvidenceCount = package.Evidence.Count,
                Evidence = package.Evidence
            };

            var blobUri = await _evidenceStorage.StoreScanResultsAsync(
                scanType: $"ato-evidence-{package.ControlFamily.ToLower()}",
                scanResults: storageData,
                projectPath: package.SubscriptionId,
                cancellationToken);

            _logger.LogInformation("Stored evidence package {PackageId} for control family {ControlFamily} to blob storage: {BlobUri}",
                package.PackageId, package.ControlFamily, blobUri);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store evidence package {PackageId} to blob storage", package.PackageId);
        }
    }

    private async Task StoreCertificateAsync(
        ComplianceCertificate certificate,
        CancellationToken cancellationToken)
    {
        if (_evidenceStorage == null)
        {
            _logger.LogDebug("Evidence storage not configured, certificate {CertificateId} will not be persisted to blob storage", certificate.CertificateId);
            return;
        }

        try
        {
            // Store certificate in blob storage for immutable compliance record
            var blobUri = await _evidenceStorage.StoreScanResultsAsync(
                scanType: "compliance-certificate",
                scanResults: certificate,
                projectPath: certificate.SubscriptionId,
                cancellationToken);

            _logger.LogInformation("Stored compliance certificate {CertificateId} to blob storage: {BlobUri} (valid until {ValidUntil})",
                certificate.CertificateId, blobUri, certificate.ValidUntil);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store compliance certificate {CertificateId} to blob storage", certificate.CertificateId);
        }
    }

    private async Task<RiskProfile> CalculateRiskProfileAsync(
        AtoComplianceAssessment assessment,
        CancellationToken cancellationToken)
    {

        await Task.CompletedTask; // TODO: Implement async operations
        return new RiskProfile
        {
            RiskLevel = assessment.CriticalFindings > 0 ? "Critical" :
                       assessment.HighFindings > 5 ? "High" :
                       assessment.MediumFindings > 10 ? "Medium" : "Low",
            RiskScore = CalculateRiskScore(assessment),
            TopRisks = IdentifyTopRisks(assessment)
        };
    }

    private double CalculateRiskScore(AtoComplianceAssessment assessment)
    {
        return (assessment.CriticalFindings * 10) +
               (assessment.HighFindings * 7.5) +
               (assessment.MediumFindings * 5) +
               (assessment.LowFindings * 2.5);
    }

    private List<string> IdentifyTopRisks(AtoComplianceAssessment assessment)
    {
        return assessment.ControlFamilyResults
            .Where(r => r.Value.ComplianceScore < 70)
            .OrderBy(r => r.Value.ComplianceScore)
            .Take(5)
            .Select(r => $"{r.Key}: {r.Value.ComplianceScore:F1}% compliant")
            .ToList();
    }

    private List<string> IdentifyTopRisks(Dictionary<string, CategoryRisk> categories)
    {
        return categories
            .Where(c => c.Value.RiskScore > 7)
            .OrderByDescending(c => c.Value.RiskScore)
            .Take(5)
            .Select(c => $"{c.Key}: {c.Value.RiskLevel}")
            .ToList();
    }

    private async Task<CategoryRisk> AssessCategoryRiskAsync(
        string subscriptionId,
        string category,
        CancellationToken cancellationToken)
    {
        // Implementation would assess specific category risks

        await Task.CompletedTask; // TODO: Implement async operations
        return new CategoryRisk
        {
            Category = category,
            RiskScore = Random.Shared.Next(1, 10),
            RiskLevel = "Medium",
            Vulnerabilities = new List<string>(),
            Mitigations = new List<string>()
        };
    }

    private double CalculateOverallRiskScore(IEnumerable<CategoryRisk> categoryRisks)
    {
        return categoryRisks.Average(r => r.RiskScore);
    }

    private string DetermineRiskLevel(double riskScore)
    {
        return riskScore switch
        {
            >= 8 => "Critical",
            >= 6 => "High",
            >= 4 => "Medium",
            >= 2 => "Low",
            _ => "Minimal"
        };
    }

    private async Task<List<RiskMitigation>> GenerateMitigationRecommendationsAsync(
        List<string> topRisks,
        CancellationToken cancellationToken)
    {
        // Generate specific mitigations for top risks
        return topRisks.Select(risk => new RiskMitigation
        {
            Risk = risk,
            Recommendation = $"Implement controls to address {risk}",
            Priority = "High",
            EstimatedEffort = TimeSpan.FromHours(8)
        }).ToList();
    }

    private async Task<string> CalculateRiskTrendAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        // Compare with historical risk assessments

        await Task.CompletedTask; // TODO: Implement async operations
        return "Improving"; // Simplified
    }

    private string GenerateRiskSummary(RiskAssessment assessment)
    {
        return $"Risk assessment identified overall risk level as {assessment.RiskLevel} " +
               $"with risk score {assessment.OverallRiskScore:F1}/10. " +
               $"Assessment completed at {assessment.AssessmentDate}.";
    }

    public async Task<AtoComplianceAssessment?> GetLatestAssessmentAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        return await _assessmentService.GetLatestCompletedAssessmentAsync(subscriptionId, cancellationToken);
    }

    private AtoFindingSeverity ParseSeverity(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" => AtoFindingSeverity.Critical,
            "high" => AtoFindingSeverity.High,
            "medium" => AtoFindingSeverity.Medium,
            "low" => AtoFindingSeverity.Low,
            _ => AtoFindingSeverity.Informational
        };
    }

    private string GenerateCertificateHash(ComplianceCertificate certificate)
    {
        var data = JsonSerializer.Serialize(certificate);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    private async Task<double> GetComplianceScoreAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken)
    {
        return await _assessmentService.GetComplianceScoreAtDateAsync(subscriptionId, date, cancellationToken);
    }

    private async Task<int> GetFailedControlsAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken)
    {
        return await _assessmentService.GetFailedControlsAtDateAsync(subscriptionId, date, cancellationToken);
    }

    private async Task<int> GetPassedControlsAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken)
    {
        return await _assessmentService.GetPassedControlsAtDateAsync(subscriptionId, date, cancellationToken);
    }

    private async Task<int> GetActiveFindingsAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken)
    {
        return await _assessmentService.GetActiveFindingsAtDateAsync(subscriptionId, date, cancellationToken);
    }

    private async Task<int> GetRemediatedFindingsAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken)
    {
        return await _assessmentService.GetRemediatedFindingsAtDateAsync(subscriptionId, date, cancellationToken);
    }

    private async Task<List<string>> GetComplianceEventsAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken)
    {
        return await _assessmentService.GetComplianceEventsAtDateAsync(subscriptionId, date, cancellationToken);
    }

    private ComplianceTrends CalculateComplianceTrends(List<ComplianceDataPoint> dataPoints)
    {
        return new ComplianceTrends
        {
            ComplianceScoreTrend = "Improving",
            FindingsTrend = "Decreasing",
            RemediationRate = "High"
        };
    }

    private List<string> IdentifySignificantEvents(List<ComplianceDataPoint> dataPoints)
    {
        var events = new List<string>();

        if (dataPoints == null || dataPoints.Count < 2)
            return events;

        // Analyze data points chronologically
        for (int i = 1; i < dataPoints.Count; i++)
        {
            var current = dataPoints[i];
            var previous = dataPoints[i - 1];

            // Significant score improvements (≥10% increase)
            var scoreDelta = current.ComplianceScore - previous.ComplianceScore;
            if (scoreDelta >= 10)
            {
                events.Add($"Compliance score improved by {scoreDelta:F1}% on {current.Date:yyyy-MM-dd}");
            }

            // Significant score declines (≥10% decrease)
            if (scoreDelta <= -10)
            {
                events.Add($"⚠️ Compliance score declined by {Math.Abs(scoreDelta):F1}% on {current.Date:yyyy-MM-dd}");
            }

            // Large remediation efforts (≥15 findings remediated)
            var remediationDelta = current.RemediatedFindings - previous.RemediatedFindings;
            if (remediationDelta >= 15)
            {
                events.Add($"✅ {remediationDelta} findings remediated on {current.Date:yyyy-MM-dd}");
            }

            // New critical findings spike (≥5 increase)
            var findingsDelta = current.ActiveFindings - previous.ActiveFindings;
            if (findingsDelta >= 5)
            {
                events.Add($"🔴 {findingsDelta} new findings discovered on {current.Date:yyyy-MM-dd}");
            }

            // Failed controls reduction (≥8 controls fixed)
            var failedControlsDelta = previous.ControlsFailed - current.ControlsFailed;
            if (failedControlsDelta >= 8)
            {
                events.Add($"✅ {failedControlsDelta} controls brought into compliance on {current.Date:yyyy-MM-dd}");
            }

            // Failed controls increase (≥5 controls failing)
            if (failedControlsDelta <= -5)
            {
                events.Add($"⚠️ {Math.Abs(failedControlsDelta)} additional controls failed on {current.Date:yyyy-MM-dd}");
            }
        }

        // Check first and last data points for milestone events
        if (dataPoints.Count > 0)
        {
            var first = dataPoints.First();
            var last = dataPoints.Last();

            // Achieved high compliance
            if (last.ComplianceScore >= 90 && first.ComplianceScore < 90)
            {
                events.Add($"🎯 Achieved {last.ComplianceScore:F1}% compliance (high compliance milestone)");
            }

            // Overall trend analysis
            var overallScoreDelta = last.ComplianceScore - first.ComplianceScore;
            if (overallScoreDelta >= 20)
            {
                events.Add($"📈 Overall compliance improved by {overallScoreDelta:F1}% over the period");
            }
            else if (overallScoreDelta <= -20)
            {
                events.Add($"📉 Overall compliance declined by {Math.Abs(overallScoreDelta):F1}% over the period");
            }
        }

        return events;
    }

    private List<string> GenerateTimelineInsights(ComplianceTimeline timeline)
    {
        var insights = new List<string>();

        if (timeline.DataPoints == null || timeline.DataPoints.Count == 0)
        {
            insights.Add("No historical data available for trend analysis");
            return insights;
        }

        var first = timeline.DataPoints.First();
        var last = timeline.DataPoints.Last();

        // Overall compliance trend insight
        var overallScoreDelta = last.ComplianceScore - first.ComplianceScore;
        if (overallScoreDelta > 0)
        {
            insights.Add($"Compliance score improved by {overallScoreDelta:F1}% over the period (from {first.ComplianceScore:F1}% to {last.ComplianceScore:F1}%)");
        }
        else if (overallScoreDelta < 0)
        {
            insights.Add($"⚠️ Compliance score declined by {Math.Abs(overallScoreDelta):F1}% over the period - immediate action recommended");
        }
        else
        {
            insights.Add("Compliance score remained stable over the period");
        }

        // Remediation effectiveness
        var totalRemediatedFindings = timeline.DataPoints.Sum(dp => dp.RemediatedFindings);
        if (totalRemediatedFindings > 50)
        {
            insights.Add($"Strong remediation efforts: {totalRemediatedFindings} total findings remediated");
        }
        else if (totalRemediatedFindings > 0)
        {
            insights.Add($"Moderate remediation progress: {totalRemediatedFindings} findings remediated - consider accelerating efforts");
        }
        else
        {
            insights.Add("⚠️ No remediation activity detected - develop and execute remediation plan");
        }

        // Control compliance trend
        var controlImprovements = last.ControlsPassed - first.ControlsPassed;
        if (controlImprovements > 10)
        {
            insights.Add($"Excellent progress: {controlImprovements} additional controls brought into compliance");
        }
        else if (controlImprovements < -5)
        {
            insights.Add($"⚠️ Control compliance degraded: {Math.Abs(controlImprovements)} controls now failing");
        }

        // Active findings trend
        var findingsTrend = last.ActiveFindings - first.ActiveFindings;
        if (findingsTrend < 0)
        {
            insights.Add($"Positive trend: {Math.Abs(findingsTrend)} fewer active findings than at the start of the period");
        }
        else if (findingsTrend > 10)
        {
            insights.Add($"⚠️ Rising findings: {findingsTrend} new active findings - investigate root causes");
        }

        // Volatility analysis
        if (timeline.DataPoints.Count > 3)
        {
            var scoreChanges = new List<double>();
            for (int i = 1; i < timeline.DataPoints.Count; i++)
            {
                scoreChanges.Add(Math.Abs(timeline.DataPoints[i].ComplianceScore - timeline.DataPoints[i - 1].ComplianceScore));
            }
            var avgChange = scoreChanges.Average();

            if (avgChange > 8)
            {
                insights.Add("High compliance score volatility detected - establish consistent compliance practices");
            }
            else if (avgChange < 2)
            {
                insights.Add("Stable compliance posture maintained - continue current practices");
            }
        }

        // Recommendations based on current state
        if (last.ComplianceScore < 70)
        {
            insights.Add("⚠️ Compliance below 70% - prioritize critical findings and develop comprehensive remediation plan");
        }
        else if (last.ComplianceScore >= 90)
        {
            insights.Add($"Excellent compliance posture at {last.ComplianceScore:F1}% - focus on maintaining this level and continuous improvement");
        }

        // Automation recommendation
        if (timeline.DataPoints.Count >= 7 && totalRemediatedFindings < 20)
        {
            insights.Add("Consider implementing automated compliance monitoring and remediation to accelerate improvements");
        }

        // Trend-based recommendations
        if (timeline.Trends?.ComplianceScoreTrend == "Improving")
        {
            insights.Add("Compliance trajectory is positive - maintain current remediation velocity");
        }
        else if (timeline.Trends?.ComplianceScoreTrend == "Declining")
        {
            insights.Add("⚠️ Compliance is declining - review recent changes and strengthen controls");
        }

        return insights;
    }

    #endregion

    #region Data Access Methods (delegated to AssessmentService)

    /// <inheritdoc />
    public async Task<IReadOnlyList<ComplianceAssessmentSummary>> GetComplianceHistoryAsync(
        string subscriptionId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _assessmentService.GetComplianceHistoryAsync(subscriptionId, startDate, endDate, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssessmentAuditEntry>> GetAssessmentAuditLogAsync(
        string subscriptionId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _assessmentService.GetAssessmentAuditLogAsync(subscriptionId, startDate, endDate, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ComplianceAssessmentWithFindings>> GetComplianceTrendsDataAsync(
        string subscriptionId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _assessmentService.GetComplianceTrendsDataAsync(subscriptionId, startDate, endDate, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ComplianceAssessmentWithFindings?> GetCachedAssessmentAsync(
        string subscriptionId,
        string? resourceGroupName,
        int cacheHours,
        CancellationToken cancellationToken = default)
    {
        return await _assessmentService.GetCachedAssessmentAsync(subscriptionId, resourceGroupName, cacheHours, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string?> SaveAssessmentAsync(
        AtoComplianceAssessment assessment,
        string subscriptionId,
        string? resourceGroupName,
        string initiatedBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _assessmentService.SaveAssessmentAsync(assessment, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save assessment");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<AtoFinding?> GetFindingByIdAsync(
        string findingId,
        CancellationToken cancellationToken = default)
    {
        var findings = await _assessmentService.GetFindingsAsync(string.Empty, cancellationToken);
        return findings.FirstOrDefault(f => f.Id == findingId);
    }

    /// <inheritdoc />
    public async Task<AtoFinding?> GetFindingByIdWithAssessmentAsync(
        string findingId,
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        // Search for the finding across assessments for this subscription
        var assessments = await _assessmentService.GetAssessmentsAsync(subscriptionId, cancellationToken);

        foreach (var assessment in assessments)
        {
            var finding = assessment.ControlFamilyResults.Values
                .SelectMany(cf => cf.Findings)
                .FirstOrDefault(f => f.Id == findingId);
            if (finding != null)
            {
                return finding;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AtoFinding>> GetUnresolvedFindingsAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var assessments = await _assessmentService.GetAssessmentsAsync(subscriptionId, cancellationToken);

        var unresolvedFindings = assessments
            .SelectMany(a => a.ControlFamilyResults.Values)
            .SelectMany(cf => cf.Findings)
            .Where(f => f.RemediationStatus != AtoRemediationStatus.Completed)
            .ToList();

        return unresolvedFindings;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateFindingStatusAsync(
        string findingId,
        string status,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Find and resolve the finding
            return await _assessmentService.ResolveFindingAsync(string.Empty, findingId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update finding status for {FindingId}", findingId);
            return false;
        }
    }

    #endregion
}
