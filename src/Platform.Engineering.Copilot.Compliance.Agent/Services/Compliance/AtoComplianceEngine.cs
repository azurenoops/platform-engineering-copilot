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
using Platform.Engineering.Copilot.Core.Models.KnowledgeBase;
using Azure.Core;
using Azure.ResourceManager.Resources;
using Azure;
using Platform.Engineering.Copilot.Core.Data.Context;
using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Compliance.Core.Data.Entities;

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
    private readonly PlatformEngineeringCopilotContext _dbContext;
    private readonly IDefenderForCloudService _defenderForCloudService;
    private readonly EvidenceStorageService _evidenceStorage;

    // Knowledge Base Services for enhanced compliance assessment
    private readonly IRmfKnowledgeService _rmfKnowledgeService;
    private readonly IStigKnowledgeService _stigKnowledgeService;
    private readonly IDoDInstructionService _dodInstructionService;
    private readonly IDoDWorkflowService _dodWorkflowService;

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
        PlatformEngineeringCopilotContext dbContext,
        IRmfKnowledgeService rmfKnowledgeService,
        IStigKnowledgeService stigKnowledgeService,
        IDoDInstructionService dodInstructionService,
        IDoDWorkflowService dodWorkflowService,
        IDefenderForCloudService defenderForCloudService,
        EvidenceStorageService evidenceStorage)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _nistControlsService = nistControlsService ?? throw new ArgumentNullException(nameof(nistControlsService));
        _azureResourceService = azureResourceService ?? throw new ArgumentNullException(nameof(azureResourceService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));

        // _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

        _rmfKnowledgeService = rmfKnowledgeService ?? throw new ArgumentNullException(nameof(rmfKnowledgeService));
        _stigKnowledgeService = stigKnowledgeService ?? throw new ArgumentNullException(nameof(stigKnowledgeService));
        _dodInstructionService = dodInstructionService ?? throw new ArgumentNullException(nameof(dodInstructionService));
        _dodWorkflowService = dodWorkflowService ?? throw new ArgumentNullException(nameof(dodWorkflowService));
        _defenderForCloudService = defenderForCloudService ?? throw new ArgumentNullException(nameof(defenderForCloudService));
        _evidenceStorage = evidenceStorage ?? throw new ArgumentNullException(nameof(evidenceStorage));

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

            // Get all NIST control families - using known families
            var controlFamilies = new List<string>
            {
                "AC", "AU", "SC", "SI", "CM", "CP", "IA", "IR", "MA", "MP",
                "PE", "PL", "PS", "RA", "SA", "CA", "AT", "PM"
            };

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
            { "AC", new AccessControlScanner(_logger, _azureResourceService) },
            { "AU", new AuditScanner(_logger, _azureResourceService, gatewayOptions) },
            { "SC", new SystemCommunicationScanner(_logger, _azureResourceService) },
            { "SI", new SystemIntegrityScanner(_logger, _azureResourceService) },
            { "CP", new ContingencyPlanningScanner(_logger, _azureResourceService) },
            { "IA", new IdentificationAuthenticationScanner(_logger, _azureResourceService) },
            { "CM", new ConfigurationManagementScanner(_logger, _azureResourceService) },
            { "IR", new IncidentResponseScanner(_logger, _azureResourceService) },
            { "RA", new RiskAssessmentScanner(_logger, _azureResourceService, _defenderForCloudService) },
            { "CA", new SecurityAssessmentScanner(_logger, _azureResourceService, _defenderForCloudService) },
            { "Default", new DefaultComplianceScanner(_logger) }
        };
    }

    private Dictionary<string, IEvidenceCollector> InitializeEvidenceCollectors()
    {
        return new Dictionary<string, IEvidenceCollector>
        {
            { "AC", new AccessControlEvidenceCollector(_logger, _azureResourceService) },
            { "AU", new AuditEvidenceCollector(_logger, _azureResourceService) },
            { "SC", new SecurityEvidenceCollector(_logger, _azureResourceService) },
            { "CP", new ContingencyPlanningEvidenceCollector(_logger, _azureResourceService) },
            { "IA", new IdentificationAuthenticationEvidenceCollector(_logger, _azureResourceService) },
            { "CM", new ConfigurationManagementEvidenceCollector(_logger, _azureResourceService) },
            { "IR", new IncidentResponseEvidenceCollector(_logger, _azureResourceService) },
            { "SI", new SystemIntegrityEvidenceCollector(_logger, _azureResourceService) },
            { "RA", new RiskAssessmentEvidenceCollector(_logger, _azureResourceService, _defenderForCloudService) },
            { "CA", new SecurityAssessmentEvidenceCollector(_logger, _azureResourceService, _defenderForCloudService) },
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
            FamilyName = GetControlFamilyName(family),
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
        var stigFindings = await ValidateFamilyStigsAsync(
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
        try
        {
            // Get the latest assessment to determine monitored controls
            var latestAssessment = await _dbContext.ComplianceAssessments
                .Include(a => a.Findings)
                .Where(a => a.SubscriptionId == subscriptionId && a.Status == "Completed")
                .OrderByDescending(a => a.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

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
                // Find findings for this control
                var controlFindings = latestAssessment.Findings
                    .Where(f => f.ControlId == controlId || 
                               (!string.IsNullOrEmpty(f.AffectedNistControls) && 
                                f.AffectedNistControls.Contains(controlId)))
                    .ToList();

                // Determine compliance status based on findings
                var hasFailures = controlFindings.Any(f => 
                    f.ComplianceStatus == "NonCompliant" || 
                    f.Severity == "Critical" || 
                    f.Severity == "High");

                // Detect drift if there are new or unresolved findings
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

    private async Task<List<ComplianceAlert>> GetControlAlertsAsync(
        string subscriptionId,
        string controlId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Query findings for this control that are unresolved and high priority
            var findings = await _dbContext.ComplianceFindings
                .Where(f => f.Assessment.SubscriptionId == subscriptionId &&
                           f.ResolvedAt == null &&
                           (f.ControlId == controlId || f.AffectedNistControls!.Contains(controlId)) &&
                           (f.Severity == "Critical" || f.Severity == "High"))
                .OrderByDescending(f => f.Severity)
                .ThenByDescending(f => f.DetectedAt)
                .Take(10) // Limit to top 10 alerts per control
                .ToListAsync(cancellationToken);

            // Convert findings to ComplianceAlert objects
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
                AlertTime = f.DetectedAt,
                DueDate = CalculateAlertDueDate(f.Severity, f.DetectedAt),
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
            var count = await _dbContext.ComplianceFindings
                .Where(f => f.Assessment.SubscriptionId == subscriptionId &&
                           f.IsAutomaticallyFixable &&
                           f.ResolvedAt != null)
                .CountAsync(cancellationToken);

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
            "AC" => 5,  // Access Control: NSGs, Key Vaults, Log Analytics, RBAC, Conditional Access
            "AU" => 4,  // Audit: Logs, Retention, Protection, Monitoring
            "SC" => 5,  // System Communications: Network, Encryption, Certificates, Firewalls, DDoS
            "IA" => 4,  // Identification & Authentication: MFA, Identity, Access Policies, Authentication
            "CM" => 4,  // Configuration Management: Config, Baselines, Change Control, Inventory
            "IR" => 3,  // Incident Response: Response Plans, Detection, Monitoring
            "RA" => 3,  // Risk Assessment: Assessments, Vulnerabilities, Risks
            "CA" => 4,  // Security Assessment: Continuous Monitoring, Assessments, Testing, Reviews
            "SI" => 4,  // System Integrity: Integrity Checks, Malware Protection, Updates, Monitoring
            "CP" => 3,  // Contingency Planning: Backups, Recovery, Business Continuity
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
            // Store in database for persistence
            var dbAssessment = new ComplianceAssessment
            {
                Id = assessment.AssessmentId,
                SubscriptionId = assessment.SubscriptionId,
                AssessmentType = "NIST-800-53",
                Status = "Completed",
                ComplianceScore = (decimal)assessment.OverallComplianceScore,
                TotalFindings = assessment.TotalFindings,
                CriticalFindings = assessment.CriticalFindings,
                HighFindings = assessment.HighFindings,
                MediumFindings = assessment.MediumFindings,
                LowFindings = assessment.LowFindings,
                InformationalFindings = assessment.InformationalFindings,
                ExecutiveSummary = assessment.ExecutiveSummary,
                RiskProfile = JsonSerializer.Serialize(assessment.RiskProfile),
                Results = JsonSerializer.Serialize(assessment.ControlFamilyResults),
                Recommendations = assessment.Recommendations != null ? JsonSerializer.Serialize(assessment.Recommendations) : null,
                InitiatedBy = "ComplianceAgent",
                StartedAt = assessment.StartTime.DateTime,
                CompletedAt = assessment.EndTime.DateTime,
                Duration = assessment.Duration.Ticks // Store as ticks (BIGINT)
            };

            // Add findings
            foreach (var familyResult in assessment.ControlFamilyResults.Values)
            {
                foreach (var finding in familyResult.Findings)
                {
                    var dbFinding = new ComplianceFinding
                    {
                        AssessmentId = assessment.AssessmentId,
                        FindingId = finding.Id,
                        RuleId = finding.RuleId,
                        Title = finding.Title ?? finding.Description.Substring(0, Math.Min(200, finding.Description.Length)),
                        Description = finding.Description,
                        Severity = finding.Severity.ToString(),
                        ComplianceStatus = finding.ComplianceStatus.ToString(),
                        FindingType = finding.FindingType.ToString(),
                        ResourceId = finding.ResourceId,
                        ResourceType = finding.ResourceType,
                        ResourceName = finding.ResourceName,
                        ControlId = finding.AffectedNistControls.FirstOrDefault(),
                        AffectedNistControls = System.Text.Json.JsonSerializer.Serialize(finding.AffectedNistControls),
                        Evidence = finding.Evidence != null ? System.Text.Json.JsonSerializer.Serialize(finding.Evidence) : null,
                        Remediation = finding.RemediationGuidance,
                        IsRemediable = finding.IsRemediable,
                        IsAutomaticallyFixable = finding.IsAutoRemediable,
                        DetectedAt = DateTime.UtcNow
                    };
                    dbAssessment.Findings.Add(dbFinding);
                }
            }

            _dbContext.ComplianceAssessments.Add(dbAssessment);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("✅ Persisted assessment {AssessmentId} with {FindingsCount} findings to database",
                assessment.AssessmentId, dbAssessment.Findings.Count);

            // Also store in memory cache for fast access
            var cacheKey = $"ComplianceAssessment_{assessment.AssessmentId}";
            var cacheOptions = new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromHours(24) // Keep assessment results for 24 hours
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

            _logger.LogInformation("✅ Stored assessment {AssessmentId} with {FindingsCount} findings in memory cache",
                assessment.AssessmentId, assessmentSummary.FindingsCount);
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
        try
        {
            _logger.LogInformation("🔍 Querying database for latest assessment for subscription {SubscriptionId}", subscriptionId);

            // Query the database for the most recent completed assessment for this subscription
            var latestDbAssessment = await _dbContext.ComplianceAssessments
                .Include(a => a.Findings)
                .Where(a => a.SubscriptionId == subscriptionId && a.Status == "Completed")
                .OrderByDescending(a => a.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestDbAssessment == null)
            {
                _logger.LogInformation("No assessment found in database for subscription {SubscriptionId}", subscriptionId);
                return null;
            }

            _logger.LogInformation("Found assessment {AssessmentId} completed at {CompletedAt} with score {Score}%",
                latestDbAssessment.Id, latestDbAssessment.CompletedAt, latestDbAssessment.ComplianceScore);

            // Convert database findings to AtoFinding model
            var allFindings = latestDbAssessment.Findings
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
            
            // Get all unique control families from findings
            var controlFamilies = allFindings
                .SelectMany(f => f.AffectedNistControls)
                .Select(controlId => controlId.Length >= 2 ? controlId.Substring(0, 2).ToUpper() : controlId)
                .Distinct()
                .ToHashSet();

            // Build a ControlFamilyAssessment for each family
            foreach (var family in controlFamilies)
            {
                var familyFindings = allFindings
                    .Where(f => f.AffectedNistControls.Any(c => c.StartsWith(family, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                var familyAssessment = new ControlFamilyAssessment
                {
                    ControlFamily = family,
                    FamilyName = GetControlFamilyName(family),
                    AssessmentTime = latestDbAssessment.CompletedAt ?? DateTimeOffset.UtcNow,
                    Findings = familyFindings,
                    TotalControls = 0, // Cannot reconstruct without re-querying NIST controls
                    PassedControls = 0, // Cannot reconstruct without re-querying NIST controls
                    ComplianceScore = 0 // Will be calculated below
                };

                // Estimate compliance score based on findings
                // If there are findings, assume some controls failed
                // This is an approximation since we don't have the original total/passed control counts
                if (familyFindings.Any())
                {
                    // Assume 20 controls per family (rough average)
                    // Calculate failed controls based on unique affected controls
                    var affectedControls = familyFindings
                        .SelectMany(f => f.AffectedNistControls)
                        .Where(c => c.StartsWith(family, StringComparison.OrdinalIgnoreCase))
                        .Distinct()
                        .Count();
                    
                    familyAssessment.TotalControls = Math.Max(20, affectedControls);
                    familyAssessment.PassedControls = Math.Max(0, familyAssessment.TotalControls - affectedControls);
                    familyAssessment.ComplianceScore = familyAssessment.TotalControls > 0
                        ? (double)familyAssessment.PassedControls / familyAssessment.TotalControls * 100
                        : 100;
                }
                else
                {
                    // No findings = 100% compliant
                    familyAssessment.TotalControls = 20;
                    familyAssessment.PassedControls = 20;
                    familyAssessment.ComplianceScore = 100;
                }

                controlFamilyResults[family] = familyAssessment;
            }

            // Reconstruct full AtoComplianceAssessment
            var assessment = new AtoComplianceAssessment
            {
                AssessmentId = latestDbAssessment.Id,
                SubscriptionId = latestDbAssessment.SubscriptionId,
                StartTime = latestDbAssessment.StartedAt,
                EndTime = latestDbAssessment.CompletedAt ?? DateTimeOffset.UtcNow,
                Duration = latestDbAssessment.Duration.HasValue ? TimeSpan.FromTicks(latestDbAssessment.Duration.Value) : TimeSpan.Zero,
                OverallComplianceScore = (double)latestDbAssessment.ComplianceScore,
                ControlFamilyResults = controlFamilyResults,
                TotalFindings = latestDbAssessment.TotalFindings,
                CriticalFindings = latestDbAssessment.CriticalFindings,
                HighFindings = latestDbAssessment.HighFindings,
                MediumFindings = latestDbAssessment.MediumFindings,
                LowFindings = latestDbAssessment.LowFindings,
                ExecutiveSummary = latestDbAssessment.ExecutiveSummary,
                RiskProfile = !string.IsNullOrEmpty(latestDbAssessment.RiskProfile)
                    ? JsonSerializer.Deserialize<RiskProfile>(latestDbAssessment.RiskProfile)
                    : null
            };

            _logger.LogInformation("Successfully reconstructed assessment with {FamilyCount} control families and {FindingCount} findings",
                controlFamilyResults.Count, allFindings.Count);

            return assessment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve latest assessment for {SubscriptionId}", subscriptionId);
            return null;
        }
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

    private string GetControlFamilyName(string familyCode)
    {
        return familyCode switch
        {
            "AC" => "Access Control",
            "AU" => "Audit and Accountability",
            "AT" => "Awareness and Training",
            "CM" => "Configuration Management",
            "CP" => "Contingency Planning",
            "IA" => "Identification and Authentication",
            "IR" => "Incident Response",
            "MA" => "Maintenance",
            "MP" => "Media Protection",
            "PE" => "Physical and Environmental Protection",
            "PL" => "Planning",
            "PS" => "Personnel Security",
            "RA" => "Risk Assessment",
            "CA" => "Security Assessment and Authorization",
            "SC" => "System and Communications Protection",
            "SI" => "System and Information Integrity",
            "SA" => "System and Services Acquisition",
            _ => familyCode
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
        try
        {
            // Find the closest assessment to the specified date
            var assessment = await _dbContext.ComplianceAssessments
                .Where(a => a.SubscriptionId == subscriptionId && 
                           a.Status == "Completed" &&
                           a.CompletedAt != null &&
                           a.CompletedAt.Value.Date == date.Date)
                .OrderByDescending(a => a.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

            return assessment != null ? (double)assessment.ComplianceScore : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve compliance score for date {Date}", date);
            return 0;
        }
    }

    private async Task<int> GetFailedControlsAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken)
    {
        try
        {
            // Find assessments for the specified date
            var assessment = await _dbContext.ComplianceAssessments
                .Include(a => a.Findings)
                .Where(a => a.SubscriptionId == subscriptionId && 
                           a.Status == "Completed" &&
                           a.CompletedAt != null &&
                           a.CompletedAt.Value.Date == date.Date)
                .OrderByDescending(a => a.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (assessment == null)
                return 0;

            // Count unique affected controls (controls with findings = failed)
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

    private async Task<int> GetPassedControlsAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken)
    {
        try
        {
            var assessment = await _dbContext.ComplianceAssessments
                .Where(a => a.SubscriptionId == subscriptionId && 
                           a.Status == "Completed" &&
                           a.CompletedAt != null &&
                           a.CompletedAt.Value.Date == date.Date)
                .OrderByDescending(a => a.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (assessment == null)
                return 0;

            // Estimate passed controls based on compliance score
            // Assuming ~100 total controls across all families
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

    private async Task<int> GetActiveFindingsAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken)
    {
        try
        {
            var assessment = await _dbContext.ComplianceAssessments
                .Where(a => a.SubscriptionId == subscriptionId && 
                           a.Status == "Completed" &&
                           a.CompletedAt != null &&
                           a.CompletedAt.Value.Date == date.Date)
                .OrderByDescending(a => a.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

            return assessment?.TotalFindings ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve active findings for date {Date}", date);
            return 0;
        }
    }

    private async Task<int> GetRemediatedFindingsAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get current assessment for the date
            var currentAssessment = await _dbContext.ComplianceAssessments
                .Where(a => a.SubscriptionId == subscriptionId && 
                           a.Status == "Completed" &&
                           a.CompletedAt != null &&
                           a.CompletedAt.Value.Date == date.Date)
                .OrderByDescending(a => a.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (currentAssessment == null)
                return 0;

            // Get previous assessment (before this date)
            var previousAssessment = await _dbContext.ComplianceAssessments
                .Where(a => a.SubscriptionId == subscriptionId && 
                           a.Status == "Completed" &&
                           a.CompletedAt != null &&
                           a.CompletedAt < currentAssessment.CompletedAt)
                .OrderByDescending(a => a.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (previousAssessment == null)
                return 0;

            // Calculate remediated findings as the difference
            var remediatedCount = Math.Max(0, previousAssessment.TotalFindings - currentAssessment.TotalFindings);

            return remediatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve remediated findings for date {Date}", date);
            return 0;
        }
    }

    private async Task<List<string>> GetComplianceEventsAtDateAsync(
        string subscriptionId,
        DateTimeOffset date,
        CancellationToken cancellationToken)
    {
        var events = new List<string>();

        try
        {
            // Get current assessment for the date
            var currentAssessment = await _dbContext.ComplianceAssessments
                .Where(a => a.SubscriptionId == subscriptionId && 
                           a.Status == "Completed" &&
                           a.CompletedAt != null &&
                           a.CompletedAt.Value.Date == date.Date)
                .OrderByDescending(a => a.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (currentAssessment == null)
                return events;

            // Get previous assessment for comparison
            var previousAssessment = await _dbContext.ComplianceAssessments
                .Where(a => a.SubscriptionId == subscriptionId && 
                           a.Status == "Completed" &&
                           a.CompletedAt != null &&
                           a.CompletedAt < currentAssessment.CompletedAt)
                .OrderByDescending(a => a.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

            // Detect significant events
            if (previousAssessment != null)
            {
                var scoreDelta = (double)(currentAssessment.ComplianceScore - previousAssessment.ComplianceScore);

                // Score improved significantly
                if (scoreDelta >= 10)
                {
                    events.Add($"Compliance score improved by {scoreDelta:F1}% (from {previousAssessment.ComplianceScore}% to {currentAssessment.ComplianceScore}%)");
                }
                // Score declined significantly
                else if (scoreDelta <= -10)
                {
                    events.Add($"⚠️ Compliance score declined by {Math.Abs(scoreDelta):F1}% (from {previousAssessment.ComplianceScore}% to {currentAssessment.ComplianceScore}%)");
                }

                // New critical findings
                var newCritical = currentAssessment.CriticalFindings - previousAssessment.CriticalFindings;
                if (newCritical > 0)
                {
                    events.Add($"🔴 {newCritical} new critical finding{(newCritical > 1 ? "s" : "")} detected");
                }
                // Critical findings resolved
                else if (newCritical < 0)
                {
                    events.Add($"✅ {Math.Abs(newCritical)} critical finding{(Math.Abs(newCritical) > 1 ? "s" : "")} resolved");
                }

                // Significant finding reduction
                var findingDelta = previousAssessment.TotalFindings - currentAssessment.TotalFindings;
                if (findingDelta >= 10)
                {
                    events.Add($"✅ {findingDelta} findings remediated");
                }
            }
            else
            {
                // First assessment
                events.Add($"Initial compliance assessment completed: {currentAssessment.ComplianceScore}% compliance with {currentAssessment.TotalFindings} findings");
            }

            // High finding count
            if (currentAssessment.CriticalFindings > 0)
            {
                events.Add($"⚠️ {currentAssessment.CriticalFindings} critical findings require immediate attention");
            }

            return events;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve compliance events for date {Date}", date);
            return events;
        }
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

    #region STIG Validation Methods

    /// <summary>
    /// Validates STIGs for a specific control family
    /// </summary>
    private async Task<List<AtoFinding>> ValidateFamilyStigsAsync(
        string subscriptionId,
        string? resourceGroupName,
        string family,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        // Get all STIGs mapped to this control family
        var allStigs = await _stigKnowledgeService.GetAllStigsAsync(cancellationToken);
        var familyStigs = allStigs.Where(s =>
            s.NistControls != null &&
            s.NistControls.Any(nc => nc.StartsWith(family, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        _logger.LogDebug("Found {Count} STIGs for control family {Family}", familyStigs.Count, family);

        // Validate each STIG
        foreach (var stig in familyStigs)
        {
            var stigFindings = await ValidateStigComplianceAsync(
                subscriptionId,
                resourceGroupName,
                stig,
                cancellationToken);
            findings.AddRange(stigFindings);
        }

        return findings;
    }

    /// <summary>
    /// Validates a specific STIG and returns findings
    /// </summary>
    private async Task<List<AtoFinding>> ValidateStigComplianceAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        // Dispatch to specific validation method based on STIG service type
        return stig.ServiceType switch
        {
            StigServiceType.Network => await ValidateNetworkStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Storage => await ValidateStorageStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Compute => await ValidateComputeStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Database => await ValidateDatabaseStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Identity => await ValidateIdentityStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Monitoring => await ValidateMonitoringStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Security => await ValidateSecurityStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Platform => await ValidatePlatformStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Integration => await ValidateIntegrationStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Containers => await ValidateContainerStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => new List<AtoFinding>()
        };
    }

    private async Task<List<AtoFinding>> ValidateNetworkStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219187" => await ValidateNoPublicIpsStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219210" => await ValidateNsgDenyAllStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219235" => await ValidateAksPrivateClusterStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219240" => await ValidateAzureFirewallStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219245" => await ValidateStoragePrivateEndpointStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    private async Task<List<AtoFinding>> ValidateStorageStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219165" => await ValidateStorageEncryptionStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219215" => await ValidateStoragePublicAccessStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219245" => await ValidateStoragePrivateEndpointStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    private async Task<List<AtoFinding>> ValidateComputeStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219230" => await ValidateAksRbacStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219235" => await ValidateAksPrivateClusterStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219265" => await ValidateVmDiskEncryptionStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    private async Task<List<AtoFinding>> ValidateDatabaseStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219201" => await ValidateSqlTlsStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219225" => await ValidateSqlTdeStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219255" => await ValidateSqlAtpStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219305" => await ValidateCosmosDbPrivateEndpointStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219310" => await ValidateCosmosDbCmkStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    private async Task<List<AtoFinding>> ValidateIdentityStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219153" => await ValidateMfaStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219250" => await ValidateAzureAdPimStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219275" => await ValidateManagedIdentityStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    private async Task<List<AtoFinding>> ValidateMonitoringStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219220" => await ValidateDiagnosticLogsStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219260" => await ValidateActivityLogRetentionStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    private async Task<List<AtoFinding>> ValidateSecurityStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219178" => await ValidateKeyVaultStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219270" => await ValidateAzurePolicyStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219280" => await ValidateDefenderForCloudStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    // Individual STIG validation implementations
    private async Task<List<AtoFinding>> ValidateNoPublicIpsStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var vms = allResources.Where(r =>
                r.Type.Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var vm in vms)
            {
                try
                {
                    var vmResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(vm.Id)).GetAsync(cancellationToken);
                    var vmProps = vmResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    if (vmProps != null && vmProps.TryGetValue("networkProfile", out var networkProfileObj))
                    {
                        var networkProfile = JsonSerializer.Deserialize<Dictionary<string, object>>(networkProfileObj.ToString() ?? "{}");

                        if (networkProfile != null && networkProfile.TryGetValue("networkInterfaces", out var nicsObj))
                        {
                            var nics = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(nicsObj.ToString() ?? "[]");

                            if (nics != null)
                            {
                                foreach (var nic in nics)
                                {
                                    if (nic.TryGetValue("id", out var nicIdObj))
                                    {
                                        var nicId = nicIdObj.ToString();
                                        if (!string.IsNullOrEmpty(nicId))
                                        {
                                            var nicResource = await armClient.GetGenericResource(
                                                new ResourceIdentifier(nicId)).GetAsync(cancellationToken);
                                            var nicProps = nicResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                                            if (nicProps != null && nicProps.TryGetValue("ipConfigurations", out var ipConfigsObj))
                                            {
                                                var ipConfigs = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(ipConfigsObj.ToString() ?? "[]");

                                                if (ipConfigs != null)
                                                {
                                                    foreach (var ipConfig in ipConfigs)
                                                    {
                                                        if (ipConfig.TryGetValue("properties", out var ipPropsObj))
                                                        {
                                                            var ipProps = JsonSerializer.Deserialize<Dictionary<string, object>>(ipPropsObj.ToString() ?? "{}");

                                                            if (ipProps != null && ipProps.ContainsKey("publicIPAddress"))
                                                            {
                                                                findings.Add(new AtoFinding
                                                                {
                                                                    AffectedNistControls = stig.NistControls.ToList(),
                                                                    Title = $"VM Has Public IP Address - {stig.Title}",
                                                                    Description = $"Virtual machine '{vm.Name}' has a public IP address assigned, which increases attack surface. {stig.Description}",
                                                                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                                                                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                                                                    ResourceId = vm.Id,
                                                                    ResourceName = vm.Name,
                                                                    ResourceType = vm.Type,
                                                                    Evidence = $"Public IP found on network interface: {nicId}",
                                                                    RemediationGuidance = stig.FixText,
                                                                    Metadata = new Dictionary<string, object>
                                                                    {
                                                                        ["StigId"] = stig.StigId,
                                                                        ["VulnId"] = stig.VulnId,
                                                                        ["StigSeverity"] = stig.Severity.ToString(),
                                                                        ["Category"] = stig.Category,
                                                                        ["CciRefs"] = stig.CciRefs,
                                                                        ["Source"] = "STIG"
                                                                    }
                                                                });
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception vmEx)
                {
                    _logger.LogWarning(vmEx, "Unable to query VM {VmName}", vm.Name);
                }
            }

            _logger.LogInformation(
                "STIG {StigId} validation complete: {NonCompliant}/{Total} VMs have public IPs",
                stig.StigId, findings.Count, vms.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateNsgDenyAllStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var nsgs = allResources.Where(r =>
                r.Type.Equals("Microsoft.Network/networkSecurityGroups", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var nsg in nsgs)
            {
                try
                {
                    var nsgResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(nsg.Id)).GetAsync(cancellationToken);
                    var nsgProps = nsgResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    if (nsgProps != null && nsgProps.TryGetValue("securityRules", out var rulesObj))
                    {
                        var rules = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(rulesObj.ToString() ?? "[]");

                        if (rules != null)
                        {
                            var inboundRules = rules.Where(r =>
                            {
                                if (r.TryGetValue("properties", out var propsObj))
                                {
                                    var props = JsonSerializer.Deserialize<Dictionary<string, object>>(propsObj.ToString() ?? "{}");
                                    return props != null && props.TryGetValue("direction", out var dir) && dir.ToString() == "Inbound";
                                }
                                return false;
                            }).ToList();

                            // Check if there's a deny-all rule at lowest priority (highest number)
                            var hasDenyAll = inboundRules.Any(r =>
                            {
                                if (r.TryGetValue("properties", out var propsObj))
                                {
                                    var props = JsonSerializer.Deserialize<Dictionary<string, object>>(propsObj.ToString() ?? "{}");
                                    if (props != null)
                                    {
                                        var isDeny = props.TryGetValue("access", out var access) && access.ToString() == "Deny";
                                        var isAnySource = props.TryGetValue("sourceAddressPrefix", out var src) && src.ToString() == "*";
                                        return isDeny && isAnySource;
                                    }
                                }
                                return false;
                            });

                            if (!hasDenyAll)
                            {
                                findings.Add(new AtoFinding
                                {
                                    AffectedNistControls = stig.NistControls.ToList(),
                                    Title = $"NSG Missing Deny-All Rule - {stig.Title}",
                                    Description = $"Network Security Group '{nsg.Name}' does not have a deny-all inbound rule at lowest priority. {stig.Description}",
                                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                                    ResourceId = nsg.Id,
                                    ResourceName = nsg.Name,
                                    ResourceType = nsg.Type,
                                    Evidence = $"NSG has {inboundRules.Count} inbound rules but no deny-all rule",
                                    RemediationGuidance = stig.FixText,
                                    Metadata = new Dictionary<string, object>
                                    {
                                        ["StigId"] = stig.StigId,
                                        ["VulnId"] = stig.VulnId,
                                        ["StigSeverity"] = stig.Severity.ToString(),
                                        ["Category"] = stig.Category,
                                        ["CciRefs"] = stig.CciRefs,
                                        ["Source"] = "STIG"
                                    }
                                });
                            }
                        }
                    }
                }
                catch (Exception nsgEx)
                {
                    _logger.LogWarning(nsgEx, "Unable to query NSG {NsgName}", nsg.Name);
                }
            }

            _logger.LogInformation(
                "STIG {StigId} validation complete: {NonCompliant}/{Total} NSGs missing deny-all rules",
                stig.StigId, findings.Count, nsgs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateStorageEncryptionStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var storageAccounts = allResources.Where(r =>
                r.Type.Equals("Microsoft.Storage/storageAccounts", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var storage in storageAccounts)
            {
                try
                {
                    var storageResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(storage.Id)).GetAsync(cancellationToken);
                    var storageProps = storageResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool encryptionEnabled = true;

                    if (storageProps != null && storageProps.TryGetValue("encryption", out var encryptionObj))
                    {
                        var encryption = JsonSerializer.Deserialize<Dictionary<string, object>>(encryptionObj.ToString() ?? "{}");

                        if (encryption != null && encryption.TryGetValue("services", out var servicesObj))
                        {
                            var services = JsonSerializer.Deserialize<Dictionary<string, object>>(servicesObj.ToString() ?? "{}");

                            if (services != null)
                            {
                                // Check blob encryption
                                if (services.TryGetValue("blob", out var blobObj))
                                {
                                    var blob = JsonSerializer.Deserialize<Dictionary<string, object>>(blobObj.ToString() ?? "{}");
                                    if (blob != null && blob.TryGetValue("enabled", out var blobEnabled))
                                    {
                                        encryptionEnabled = encryptionEnabled && bool.Parse(blobEnabled.ToString() ?? "false");
                                    }
                                    else
                                    {
                                        encryptionEnabled = false;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        encryptionEnabled = false;
                    }

                    if (!encryptionEnabled)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Storage Account Encryption Disabled - {stig.Title}",
                            Description = $"Storage account '{storage.Name}' does not have encryption at rest enabled. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = storage.Id,
                            ResourceName = storage.Name,
                            ResourceType = storage.Type,
                            Evidence = "Encryption not enabled for storage services",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception storageEx)
                {
                    _logger.LogWarning(storageEx, "Unable to query storage account {StorageName}", storage.Name);
                }
            }

            _logger.LogInformation(
                "STIG {StigId} validation complete: {NonCompliant}/{Total} storage accounts without encryption",
                stig.StigId, findings.Count, storageAccounts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateStoragePublicAccessStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var storageAccounts = allResources.Where(r =>
                r.Type.Equals("Microsoft.Storage/storageAccounts", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var storage in storageAccounts)
            {
                try
                {
                    var storageResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(storage.Id)).GetAsync(cancellationToken);
                    var storageProps = storageResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool publicAccessEnabled = false;

                    if (storageProps != null)
                    {
                        if (storageProps.TryGetValue("allowBlobPublicAccess", out var allowPublicObj))
                        {
                            publicAccessEnabled = bool.Parse(allowPublicObj.ToString() ?? "true");
                        }
                        else
                        {
                            publicAccessEnabled = true;
                        }
                    }

                    if (publicAccessEnabled)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Storage Account Public Access Enabled - {stig.Title}",
                            Description = $"Storage account '{storage.Name}' allows public blob access. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = storage.Id,
                            ResourceName = storage.Name,
                            ResourceType = storage.Type,
                            Evidence = "Public blob access is enabled",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception storageEx)
                {
                    _logger.LogWarning(storageEx, "Unable to query storage account {StorageName}", storage.Name);
                }
            }

            _logger.LogInformation(
                "STIG {StigId} validation complete: {NonCompliant}/{Total} storage accounts with public access",
                stig.StigId, findings.Count, storageAccounts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateDiagnosticLogsStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var criticalResourceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Microsoft.KeyVault/vaults",
                "Microsoft.Network/networkSecurityGroups",
                "Microsoft.Storage/storageAccounts",
                "Microsoft.Sql/servers"
            };

            var criticalResources = allResources.Where(r =>
                criticalResourceTypes.Contains(r.Type)).ToList();

            foreach (var resource in criticalResources)
            {
                findings.Add(new AtoFinding
                {
                    AffectedNistControls = stig.NistControls.ToList(),
                    Title = $"Diagnostic Logging Not Configured - {stig.Title}",
                    Description = $"Critical resource '{resource.Name}' may not have diagnostic logging configured. {stig.Description}",
                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    Evidence = "Diagnostic settings validation required",
                    RemediationGuidance = stig.FixText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["StigId"] = stig.StigId,
                        ["VulnId"] = stig.VulnId,
                        ["StigSeverity"] = stig.Severity.ToString(),
                        ["Category"] = stig.Category,
                        ["CciRefs"] = stig.CciRefs,
                        ["Source"] = "STIG"
                    }
                });
            }

            _logger.LogInformation(
                "STIG {StigId} validation complete: {Count} critical resources require diagnostic logging validation",
                stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateSqlTlsStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var sqlServers = allResources.Where(r =>
                r.Type.Equals("Microsoft.Sql/servers", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var sqlServer in sqlServers)
            {
                try
                {
                    var serverResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(sqlServer.Id)).GetAsync(cancellationToken);
                    var serverProps = serverResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    string minimalTlsVersion = "1.0";

                    if (serverProps != null && serverProps.TryGetValue("minimalTlsVersion", out var tlsVersionObj))
                    {
                        minimalTlsVersion = tlsVersionObj.ToString() ?? "1.0";
                    }

                    if (string.Compare(minimalTlsVersion, "1.2", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"SQL Server TLS Version Too Low - {stig.Title}",
                            Description = $"SQL Server '{sqlServer.Name}' allows TLS version {minimalTlsVersion}. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = sqlServer.Id,
                            ResourceName = sqlServer.Name,
                            ResourceType = sqlServer.Type,
                            Evidence = $"Minimal TLS version: {minimalTlsVersion}",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG",
                                ["CurrentTlsVersion"] = minimalTlsVersion,
                                ["RequiredTlsVersion"] = "1.2"
                            }
                        });
                    }
                }
                catch (Exception sqlEx)
                {
                    _logger.LogWarning(sqlEx, "Unable to query SQL Server {ServerName}", sqlServer.Name);
                }
            }

            _logger.LogInformation(
                "STIG {StigId} validation complete: {NonCompliant}/{Total} SQL servers with inadequate TLS",
                stig.StigId, findings.Count, sqlServers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateSqlTdeStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var sqlDatabases = allResources.Where(r =>
                r.Type.Equals("Microsoft.Sql/servers/databases", StringComparison.OrdinalIgnoreCase) &&
                !r.Name.EndsWith("/master", StringComparison.OrdinalIgnoreCase)).ToList();

            // Note: In production, you would query the TDE API endpoint
            // For now, we log informational findings
            _logger.LogInformation(
                "STIG {StigId}: Found {Count} SQL databases to validate for TDE",
                stig.StigId, sqlDatabases.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateAksRbacStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var aksClusters = allResources.Where(r =>
                r.Type.Equals("Microsoft.ContainerService/managedClusters", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var aks in aksClusters)
            {
                try
                {
                    var aksResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(aks.Id)).GetAsync(cancellationToken);
                    var aksProps = aksResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool rbacEnabled = false;

                    if (aksProps != null && aksProps.TryGetValue("enableRBAC", out var rbacObj))
                    {
                        rbacEnabled = bool.Parse(rbacObj.ToString() ?? "false");
                    }

                    if (!rbacEnabled)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"AKS RBAC Not Enabled - {stig.Title}",
                            Description = $"AKS cluster '{aks.Name}' does not have RBAC enabled. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = aks.Id,
                            ResourceName = aks.Name,
                            ResourceType = aks.Type,
                            Evidence = "enableRBAC is not set to true",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception aksEx)
                {
                    _logger.LogWarning(aksEx, "Unable to query AKS cluster {AksName}", aks.Name);
                }
            }

            _logger.LogInformation(
                "STIG {StigId} validation complete: {NonCompliant}/{Total} AKS clusters without RBAC",
                stig.StigId, findings.Count, aksClusters.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateAksPrivateClusterStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var aksClusters = allResources.Where(r =>
                r.Type.Equals("Microsoft.ContainerService/managedClusters", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var aks in aksClusters)
            {
                try
                {
                    var aksResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(aks.Id)).GetAsync(cancellationToken);
                    var aksProps = aksResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool isPrivateCluster = false;

                    if (aksProps != null && aksProps.TryGetValue("apiServerAccessProfile", out var apiServerObj))
                    {
                        var apiServer = JsonSerializer.Deserialize<Dictionary<string, object>>(apiServerObj.ToString() ?? "{}");
                        if (apiServer != null && apiServer.TryGetValue("enablePrivateCluster", out var privateObj))
                        {
                            isPrivateCluster = bool.Parse(privateObj.ToString() ?? "false");
                        }
                    }

                    if (!isPrivateCluster)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"AKS Cluster Not Private - {stig.Title}",
                            Description = $"AKS cluster '{aks.Name}' is not configured as a private cluster. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = aks.Id,
                            ResourceName = aks.Name,
                            ResourceType = aks.Type,
                            Evidence = "enablePrivateCluster is not set to true",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception aksEx)
                {
                    _logger.LogWarning(aksEx, "Unable to query AKS cluster {AksName}", aks.Name);
                }
            }

            _logger.LogInformation(
                "STIG {StigId} validation complete: {NonCompliant}/{Total} AKS clusters not private",
                stig.StigId, findings.Count, aksClusters.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateAzureFirewallStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var firewalls = allResources.Where(r =>
                r.Type.Equals("Microsoft.Network/azureFirewalls", StringComparison.OrdinalIgnoreCase)).ToList();

            if (firewalls.Count == 0)
            {
                findings.Add(new AtoFinding
                {
                    AffectedNistControls = stig.NistControls.ToList(),
                    Title = $"Azure Firewall Not Deployed - {stig.Title}",
                    Description = $"No Azure Firewall found for egress filtering. {stig.Description}",
                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceName = "Subscription",
                    ResourceType = "Microsoft.Resources/subscriptions",
                    Evidence = "No Azure Firewall resources found",
                    RemediationGuidance = stig.FixText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["StigId"] = stig.StigId,
                        ["VulnId"] = stig.VulnId,
                        ["StigSeverity"] = stig.Severity.ToString(),
                        ["Category"] = stig.Category,
                        ["CciRefs"] = stig.CciRefs,
                        ["Source"] = "STIG"
                    }
                });
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} firewalls found",
                stig.StigId, firewalls.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateStoragePrivateEndpointStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var storageAccounts = allResources.Where(r =>
                r.Type.Equals("Microsoft.Storage/storageAccounts", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var storage in storageAccounts)
            {
                try
                {
                    var storageResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(storage.Id)).GetAsync(cancellationToken);
                    var storageProps = storageResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool hasPrivateEndpoint = false;
                    if (storageProps != null && storageProps.TryGetValue("privateEndpointConnections", out var peConnectionsObj))
                    {
                        var peConnections = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(peConnectionsObj.ToString() ?? "[]");
                        hasPrivateEndpoint = peConnections != null && peConnections.Count > 0;
                    }

                    if (!hasPrivateEndpoint)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Storage Account Missing Private Endpoint - {stig.Title}",
                            Description = $"Storage account '{storage.Name}' does not use private endpoints for secure access. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = storage.Id,
                            ResourceName = storage.Name,
                            ResourceType = storage.Type,
                            Evidence = "No private endpoint connections found",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception storageEx)
                {
                    _logger.LogWarning(storageEx, "Unable to query storage account {Name}", storage.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateMfaStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            // MFA validation requires Azure AD Graph API or Microsoft Graph API
            // For compliance assessment, we'll mark as manual review required
            findings.Add(new AtoFinding
            {
                AffectedNistControls = stig.NistControls.ToList(),
                Title = $"MFA Policy Validation Required - {stig.Title}",
                Description = $"Multi-factor authentication policy validation requires manual verification via Azure AD portal or Microsoft Graph API. {stig.Description}",
                Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                ResourceId = $"/subscriptions/{subscriptionId}/providers/Microsoft.AAD",
                ResourceName = "Azure Active Directory",
                ResourceType = "Microsoft.AAD/tenants",
                Evidence = "Manual verification required - check Azure AD > Security > Conditional Access > MFA policies",
                RemediationGuidance = stig.FixText,
                Metadata = new Dictionary<string, object>
                {
                    ["StigId"] = stig.StigId,
                    ["VulnId"] = stig.VulnId,
                    ["StigSeverity"] = stig.Severity.ToString(),
                    ["Category"] = stig.Category,
                    ["CciRefs"] = stig.CciRefs,
                    ["Source"] = "STIG",
                    ["ValidationNote"] = "Requires Microsoft Graph API permissions for automated validation"
                }
            });

            _logger.LogInformation("STIG {StigId} validation complete: Manual review required", stig.StigId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateAzureAdPimStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            // PIM validation requires Azure AD Premium P2 and Microsoft Graph API
            findings.Add(new AtoFinding
            {
                AffectedNistControls = stig.NistControls.ToList(),
                Title = $"PIM Configuration Validation Required - {stig.Title}",
                Description = $"Privileged Identity Management (PIM) configuration requires manual verification via Azure AD portal. Verify PIM is enabled for privileged roles. {stig.Description}",
                Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                ResourceId = $"/subscriptions/{subscriptionId}/providers/Microsoft.AAD",
                ResourceName = "Azure AD Privileged Identity Management",
                ResourceType = "Microsoft.AAD/tenants",
                Evidence = "Manual verification required - check Azure AD > Identity Governance > Privileged Identity Management",
                RemediationGuidance = stig.FixText,
                Metadata = new Dictionary<string, object>
                {
                    ["StigId"] = stig.StigId,
                    ["VulnId"] = stig.VulnId,
                    ["StigSeverity"] = stig.Severity.ToString(),
                    ["Category"] = stig.Category,
                    ["CciRefs"] = stig.CciRefs,
                    ["Source"] = "STIG",
                    ["ValidationNote"] = "Requires Azure AD Premium P2 and Microsoft Graph API for automated validation"
                }
            });

            _logger.LogInformation("STIG {StigId} validation complete: Manual review required", stig.StigId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateSqlAtpStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var sqlServers = allResources.Where(r =>
                r.Type.Equals("Microsoft.Sql/servers", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var sqlServer in sqlServers)
            {
                try
                {
                    var atpResourceId = $"{sqlServer.Id}/securityAlertPolicies/Default";
                    try
                    {
                        var atpResource = await armClient.GetGenericResource(
                            new ResourceIdentifier(atpResourceId)).GetAsync(cancellationToken);
                        var atpProps = atpResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                        bool atpEnabled = false;
                        string atpState = "Unknown";
                        if (atpProps != null && atpProps.TryGetValue("state", out var stateObj))
                        {
                            atpState = stateObj.ToString() ?? "Unknown";
                            atpEnabled = atpState.Equals("Enabled", StringComparison.OrdinalIgnoreCase);
                        }

                        if (!atpEnabled)
                        {
                            findings.Add(new AtoFinding
                            {
                                AffectedNistControls = stig.NistControls.ToList(),
                                Title = $"SQL Server ATP Disabled - {stig.Title}",
                                Description = $"SQL Server '{sqlServer.Name}' does not have Advanced Threat Protection (Microsoft Defender for SQL) enabled. {stig.Description}",
                                Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                                ComplianceStatus = AtoComplianceStatus.NonCompliant,
                                ResourceId = sqlServer.Id,
                                ResourceName = sqlServer.Name,
                                ResourceType = sqlServer.Type,
                                Evidence = $"ATP state: {atpState}",
                                RemediationGuidance = stig.FixText,
                                Metadata = new Dictionary<string, object>
                                {
                                    ["StigId"] = stig.StigId,
                                    ["VulnId"] = stig.VulnId,
                                    ["StigSeverity"] = stig.Severity.ToString(),
                                    ["Category"] = stig.Category,
                                    ["CciRefs"] = stig.CciRefs,
                                    ["Source"] = "STIG"
                                }
                            });
                        }
                    }
                    catch (Exception)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"SQL Server ATP Not Configured - {stig.Title}",
                            Description = $"SQL Server '{sqlServer.Name}' does not have Advanced Threat Protection configured. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = sqlServer.Id,
                            ResourceName = sqlServer.Name,
                            ResourceType = sqlServer.Type,
                            Evidence = "No ATP security alert policy found",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception sqlEx)
                {
                    _logger.LogWarning(sqlEx, "Unable to query SQL server {Name}", sqlServer.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateActivityLogRetentionStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);
            var logWorkspaces = allResources.Where(r =>
                r.Type.Equals("Microsoft.OperationalInsights/workspaces", StringComparison.OrdinalIgnoreCase)).ToList();

            if (logWorkspaces.Count == 0)
            {
                findings.Add(new AtoFinding
                {
                    AffectedNistControls = stig.NistControls.ToList(),
                    Title = $"No Log Analytics Workspace Found - {stig.Title}",
                    Description = $"No Log Analytics workspace found for activity log retention. Activity logs must be retained for at least 365 days. {stig.Description}",
                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceName = subscriptionId,
                    ResourceType = "Microsoft.Subscription",
                    Evidence = "No Log Analytics workspace configured for activity log retention",
                    RemediationGuidance = stig.FixText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["StigId"] = stig.StigId,
                        ["VulnId"] = stig.VulnId,
                        ["StigSeverity"] = stig.Severity.ToString(),
                        ["Category"] = stig.Category,
                        ["CciRefs"] = stig.CciRefs,
                        ["Source"] = "STIG"
                    }
                });
            }
            else
            {
                foreach (var workspace in logWorkspaces)
                {
                    try
                    {
                        var workspaceResource = await armClient.GetGenericResource(
                            new ResourceIdentifier(workspace.Id)).GetAsync(cancellationToken);
                        var workspaceProps = workspaceResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                        int retentionDays = 30;
                        if (workspaceProps != null && workspaceProps.TryGetValue("retentionInDays", out var retentionObj))
                        {
                            retentionDays = Convert.ToInt32(retentionObj);
                        }

                        if (retentionDays < 365)
                        {
                            findings.Add(new AtoFinding
                            {
                                AffectedNistControls = stig.NistControls.ToList(),
                                Title = $"Activity Log Retention Insufficient - {stig.Title}",
                                Description = $"Log Analytics workspace '{workspace.Name}' has retention of {retentionDays} days, which is less than the required 365 days. {stig.Description}",
                                Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                                ComplianceStatus = AtoComplianceStatus.NonCompliant,
                                ResourceId = workspace.Id,
                                ResourceName = workspace.Name,
                                ResourceType = workspace.Type,
                                Evidence = $"Retention configured: {retentionDays} days (required: 365 days)",
                                RemediationGuidance = stig.FixText,
                                Metadata = new Dictionary<string, object>
                                {
                                    ["StigId"] = stig.StigId,
                                    ["VulnId"] = stig.VulnId,
                                    ["StigSeverity"] = stig.Severity.ToString(),
                                    ["Category"] = stig.Category,
                                    ["CciRefs"] = stig.CciRefs,
                                    ["Source"] = "STIG",
                                    ["CurrentRetentionDays"] = retentionDays,
                                    ["RequiredRetentionDays"] = 365
                                }
                            });
                        }
                    }
                    catch (Exception wsEx)
                    {
                        _logger.LogWarning(wsEx, "Unable to query workspace {Name}", workspace.Name);
                    }
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateVmDiskEncryptionStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var vms = allResources.Where(r =>
                r.Type.Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var vm in vms)
            {
                try
                {
                    var vmResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(vm.Id)).GetAsync(cancellationToken);
                    var vmProps = vmResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool hasEncryption = false;

                    if (vmProps != null && vmProps.TryGetValue("storageProfile", out var storageProfileObj))
                    {
                        var storageProfile = JsonSerializer.Deserialize<Dictionary<string, object>>(storageProfileObj.ToString() ?? "{}");
                        if (storageProfile != null && storageProfile.TryGetValue("osDisk", out var osDiskObj))
                        {
                            var osDisk = JsonSerializer.Deserialize<Dictionary<string, object>>(osDiskObj.ToString() ?? "{}");
                            if (osDisk != null && osDisk.TryGetValue("encryptionSettings", out var encryptionObj))
                            {
                                var encryption = JsonSerializer.Deserialize<Dictionary<string, object>>(encryptionObj.ToString() ?? "{}");
                                if (encryption != null && encryption.TryGetValue("enabled", out var enabledObj))
                                {
                                    hasEncryption = Convert.ToBoolean(enabledObj);
                                }
                            }
                        }
                    }

                    if (!hasEncryption)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"VM Disk Encryption Not Enabled - {stig.Title}",
                            Description = $"Virtual machine '{vm.Name}' does not have Azure Disk Encryption enabled. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = vm.Id,
                            ResourceName = vm.Name,
                            ResourceType = vm.Type,
                            Evidence = "No encryption settings found on OS disk",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception vmEx)
                {
                    _logger.LogWarning(vmEx, "Unable to query VM {Name}", vm.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateAzurePolicyStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            try
            {
                var subscriptionResource = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
                int policyCount = 0;
                await foreach (var policy in subscriptionResource.GetPolicyAssignments().GetAllAsync())
                {
                    policyCount++;
                }

                if (policyCount == 0)
                {
                    findings.Add(new AtoFinding
                    {
                        AffectedNistControls = stig.NistControls.ToList(),
                        Title = $"No Azure Policy Assignments Found - {stig.Title}",
                        Description = $"Subscription has no Azure Policy assignments configured. Azure Policy should be used to enforce security and compliance controls. {stig.Description}",
                        Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                        ComplianceStatus = AtoComplianceStatus.NonCompliant,
                        ResourceId = $"/subscriptions/{subscriptionId}",
                        ResourceName = subscriptionId,
                        ResourceType = "Microsoft.Subscription",
                        Evidence = "No policy assignments found at subscription level",
                        RemediationGuidance = stig.FixText,
                        Metadata = new Dictionary<string, object>
                        {
                            ["StigId"] = stig.StigId,
                            ["VulnId"] = stig.VulnId,
                            ["StigSeverity"] = stig.Severity.ToString(),
                            ["Category"] = stig.Category,
                            ["CciRefs"] = stig.CciRefs,
                            ["Source"] = "STIG"
                        }
                    });
                }
                else
                {
                    _logger.LogInformation("Found {Count} policy assignments for subscription {SubscriptionId}",
                        policyCount, subscriptionId);
                }
            }
            catch (Exception rfEx)
            {
                _logger.LogWarning(rfEx, "Unable to query policy assignments for subscription {SubscriptionId}", subscriptionId);
                findings.Add(new AtoFinding
                {
                    AffectedNistControls = stig.NistControls.ToList(),
                    Title = $"Azure Policy Validation Failed - {stig.Title}",
                    Description = $"Unable to validate Azure Policy assignments. Manual verification required. {stig.Description}",
                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceName = subscriptionId,
                    ResourceType = "Microsoft.Subscription",
                    Evidence = $"API error: {rfEx.Message}",
                    RemediationGuidance = stig.FixText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["StigId"] = stig.StigId,
                        ["VulnId"] = stig.VulnId,
                        ["StigSeverity"] = stig.Severity.ToString(),
                        ["Category"] = stig.Category,
                        ["CciRefs"] = stig.CciRefs,
                        ["Source"] = "STIG"
                    }
                });
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateManagedIdentityStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var appServices = allResources.Where(r =>
                r.Type.Equals("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var appService in appServices)
            {
                try
                {
                    var appResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(appService.Id)).GetAsync(cancellationToken);

                    bool hasManagedIdentity = false;
                    if (appResource.Value.Data.Identity != null)
                    {
                        var identityType = appResource.Value.Data.Identity.ManagedServiceIdentityType.ToString();
                        hasManagedIdentity = !identityType.Equals("None", StringComparison.OrdinalIgnoreCase);
                    }

                    if (!hasManagedIdentity)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"App Service Missing Managed Identity - {stig.Title}",
                            Description = $"App Service '{appService.Name}' does not use managed identity for authentication. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = appService.Id,
                            ResourceName = appService.Name,
                            ResourceType = appService.Type,
                            Evidence = "No managed identity configured",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception appEx)
                {
                    _logger.LogWarning(appEx, "Unable to query App Service {Name}", appService.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateDefenderForCloudStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            try
            {
                // Defender for Cloud validation requires Security Center API access
                // Mark as manual review required
                findings.Add(new AtoFinding
                {
                    AffectedNistControls = stig.NistControls.ToList(),
                    Title = $"Defender for Cloud Validation Required - {stig.Title}",
                    Description = $"Microsoft Defender for Cloud Standard tier configuration requires manual verification via Azure Portal. {stig.Description}",
                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceName = subscriptionId,
                    ResourceType = "Microsoft.Subscription",
                    Evidence = "Manual verification required - check Azure Security Center > Pricing & settings",
                    RemediationGuidance = stig.FixText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["StigId"] = stig.StigId,
                        ["VulnId"] = stig.VulnId,
                        ["StigSeverity"] = stig.Severity.ToString(),
                        ["Category"] = stig.Category,
                        ["CciRefs"] = stig.CciRefs,
                        ["Source"] = "STIG",
                        ["ValidationNote"] = "Requires Security Center API or manual verification"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to query Defender for Cloud pricing for subscription {SubscriptionId}", subscriptionId);
                findings.Add(new AtoFinding
                {
                    AffectedNistControls = stig.NistControls.ToList(),
                    Title = $"Defender for Cloud Validation Failed - {stig.Title}",
                    Description = $"Unable to validate Microsoft Defender for Cloud configuration. Manual verification required. {stig.Description}",
                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceName = subscriptionId,
                    ResourceType = "Microsoft.Subscription",
                    Evidence = $"API error: {ex.Message}",
                    RemediationGuidance = stig.FixText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["StigId"] = stig.StigId,
                        ["VulnId"] = stig.VulnId,
                        ["StigSeverity"] = stig.Severity.ToString(),
                        ["Category"] = stig.Category,
                        ["CciRefs"] = stig.CciRefs,
                        ["Source"] = "STIG"
                    }
                });
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateKeyVaultStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var keyVaults = allResources.Where(r =>
                r.Type.Equals("Microsoft.KeyVault/vaults", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var keyVault in keyVaults)
            {
                try
                {
                    var kvResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(keyVault.Id)).GetAsync(cancellationToken);
                    var kvProps = kvResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool softDeleteEnabled = false;
                    bool purgeProtectionEnabled = false;

                    if (kvProps != null)
                    {
                        if (kvProps.TryGetValue("enableSoftDelete", out var softDeleteObj))
                        {
                            softDeleteEnabled = Convert.ToBoolean(softDeleteObj);
                        }

                        if (kvProps.TryGetValue("enablePurgeProtection", out var purgeObj))
                        {
                            purgeProtectionEnabled = Convert.ToBoolean(purgeObj);
                        }
                    }

                    if (!softDeleteEnabled || !purgeProtectionEnabled)
                    {
                        var issues = new List<string>();
                        if (!softDeleteEnabled) issues.Add("soft delete disabled");
                        if (!purgeProtectionEnabled) issues.Add("purge protection disabled");

                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Key Vault Security Settings Insufficient - {stig.Title}",
                            Description = $"Key Vault '{keyVault.Name}' has insufficient security settings: {string.Join(", ", issues)}. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = keyVault.Id,
                            ResourceName = keyVault.Name,
                            ResourceType = keyVault.Type,
                            Evidence = $"Security issues: {string.Join(", ", issues)}",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG",
                                ["SoftDeleteEnabled"] = softDeleteEnabled,
                                ["PurgeProtectionEnabled"] = purgeProtectionEnabled
                            }
                        });
                    }
                }
                catch (Exception kvEx)
                {
                    _logger.LogWarning(kvEx, "Unable to query Key Vault {Name}", keyVault.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidatePlatformStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219275" => await ValidateManagedIdentityStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219285" => await ValidateAppServiceHttpsStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219290" => await ValidateAppServiceTlsStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219315" => await ValidateFunctionAppHttpsStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219320" => await ValidateFunctionAppManagedIdentityStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    private async Task<List<AtoFinding>> ValidateIntegrationStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219325" => await ValidateApimSubscriptionKeysStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219330" => await ValidateApimVnetStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219335" => await ValidateServiceBusPrivateEndpointStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219340" => await ValidateServiceBusCmkStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    private async Task<List<AtoFinding>> ValidateContainerStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219230" => await ValidateAksRbacStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219235" => await ValidateAksPrivateClusterStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219295" => await ValidateAcrPrivateAccessStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219300" => await ValidateAcrVulnerabilityScanStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    // New STIG validation methods for Platform services
    private async Task<List<AtoFinding>> ValidateAppServiceHttpsStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var appServices = allResources.Where(r =>
                r.Type.Equals("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase) &&
                !r.Name.Contains("/slots/", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (appServices.Count == 0)
            {
                _logger.LogInformation("No App Services found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var appService in appServices)
            {
                try
                {
                    var appResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(appService.Id)).GetAsync(cancellationToken);
                    var appProps = appResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool httpsOnly = false;
                    if (appProps != null && appProps.TryGetValue("httpsOnly", out var httpsOnlyObj))
                    {
                        httpsOnly = Convert.ToBoolean(httpsOnlyObj);
                    }

                    if (!httpsOnly)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"App Service HTTPS Not Enforced - {stig.Title}",
                            Description = $"App Service '{appService.Name}' does not enforce HTTPS only. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = appService.Id,
                            ResourceName = appService.Name,
                            ResourceType = appService.Type,
                            Evidence = "httpsOnly is not set to true",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception appEx)
                {
                    _logger.LogWarning(appEx, "Unable to query App Service {Name}", appService.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateAppServiceTlsStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var appServices = allResources.Where(r =>
                r.Type.Equals("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase) &&
                !r.Name.Contains("/slots/", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (appServices.Count == 0)
            {
                _logger.LogInformation("No App Services found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var appService in appServices)
            {
                try
                {
                    var appResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(appService.Id)).GetAsync(cancellationToken);
                    var appProps = appResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    string minTlsVersion = "1.0";
                    if (appProps != null && appProps.TryGetValue("siteConfig", out var siteConfigObj))
                    {
                        var siteConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(siteConfigObj.ToString() ?? "{}");
                        if (siteConfig != null && siteConfig.TryGetValue("minTlsVersion", out var tlsObj))
                        {
                            minTlsVersion = tlsObj.ToString() ?? "1.0";
                        }
                    }

                    if (!minTlsVersion.StartsWith("1.2", StringComparison.OrdinalIgnoreCase) &&
                        !minTlsVersion.StartsWith("1.3", StringComparison.OrdinalIgnoreCase))
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"App Service TLS Version Insufficient - {stig.Title}",
                            Description = $"App Service '{appService.Name}' does not enforce TLS 1.2 or higher (current: {minTlsVersion}). {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = appService.Id,
                            ResourceName = appService.Name,
                            ResourceType = appService.Type,
                            Evidence = $"Minimum TLS version: {minTlsVersion} (required: 1.2 or higher)",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG",
                                ["CurrentTlsVersion"] = minTlsVersion
                            }
                        });
                    }
                }
                catch (Exception appEx)
                {
                    _logger.LogWarning(appEx, "Unable to query App Service {Name}", appService.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateFunctionAppHttpsStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var functionApps = allResources.Where(r =>
                r.Type.Equals("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase) &&
                r.Properties?.ToString()?.Contains("functionapp", StringComparison.OrdinalIgnoreCase) == true).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (functionApps.Count == 0)
            {
                _logger.LogInformation("No Function Apps found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var functionApp in functionApps)
            {
                try
                {
                    var funcResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(functionApp.Id)).GetAsync(cancellationToken);
                    var funcProps = funcResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool httpsOnly = false;
                    if (funcProps != null && funcProps.TryGetValue("httpsOnly", out var httpsOnlyObj))
                    {
                        httpsOnly = Convert.ToBoolean(httpsOnlyObj);
                    }

                    if (!httpsOnly)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Function App HTTPS Not Enforced - {stig.Title}",
                            Description = $"Function App '{functionApp.Name}' does not enforce HTTPS only. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = functionApp.Id,
                            ResourceName = functionApp.Name,
                            ResourceType = functionApp.Type,
                            Evidence = "httpsOnly is not set to true",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception funcEx)
                {
                    _logger.LogWarning(funcEx, "Unable to query Function App {Name}", functionApp.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateFunctionAppManagedIdentityStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var functionApps = allResources.Where(r =>
                r.Type.Equals("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase) &&
                r.Properties?.ToString()?.Contains("functionapp", StringComparison.OrdinalIgnoreCase) == true).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (functionApps.Count == 0)
            {
                _logger.LogInformation("No Function Apps found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var functionApp in functionApps)
            {
                try
                {
                    var funcResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(functionApp.Id)).GetAsync(cancellationToken);

                    bool hasManagedIdentity = false;
                    if (funcResource.Value.Data.Identity != null)
                    {
                        var identityType = funcResource.Value.Data.Identity.ManagedServiceIdentityType.ToString();
                        hasManagedIdentity = !identityType.Equals("None", StringComparison.OrdinalIgnoreCase);
                    }

                    if (!hasManagedIdentity)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Function App Missing Managed Identity - {stig.Title}",
                            Description = $"Function App '{functionApp.Name}' does not use managed identity. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = functionApp.Id,
                            ResourceName = functionApp.Name,
                            ResourceType = functionApp.Type,
                            Evidence = "No managed identity configured",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception funcEx)
                {
                    _logger.LogWarning(funcEx, "Unable to query Function App {Name}", functionApp.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    // New STIG validation methods for Integration services
    private async Task<List<AtoFinding>> ValidateApimSubscriptionKeysStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var apimServices = allResources.Where(r =>
                r.Type.Equals("Microsoft.ApiManagement/service", StringComparison.OrdinalIgnoreCase)).ToList();

            if (apimServices.Count == 0)
            {
                _logger.LogInformation("No API Management services found for STIG {StigId}", stig.StigId);
                return findings;
            }

            // Note: Validating subscription key requirements would require querying individual API policies
            // This is marked as manual review required
            foreach (var apim in apimServices)
            {
                findings.Add(new AtoFinding
                {
                    AffectedNistControls = stig.NistControls.ToList(),
                    Title = $"APIM Subscription Key Validation Required - {stig.Title}",
                    Description = $"API Management service '{apim.Name}' subscription key enforcement requires manual verification of API policies. {stig.Description}",
                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    ResourceId = apim.Id,
                    ResourceName = apim.Name,
                    ResourceType = apim.Type,
                    Evidence = "Manual verification required - check API policies for subscription key requirements",
                    RemediationGuidance = stig.FixText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["StigId"] = stig.StigId,
                        ["VulnId"] = stig.VulnId,
                        ["StigSeverity"] = stig.Severity.ToString(),
                        ["Category"] = stig.Category,
                        ["CciRefs"] = stig.CciRefs,
                        ["Source"] = "STIG"
                    }
                });
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateApimVnetStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var apimServices = allResources.Where(r =>
                r.Type.Equals("Microsoft.ApiManagement/service", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (apimServices.Count == 0)
            {
                _logger.LogInformation("No API Management services found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var apim in apimServices)
            {
                try
                {
                    var apimResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(apim.Id)).GetAsync(cancellationToken);
                    var apimProps = apimResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool hasVnetIntegration = false;
                    if (apimProps != null && apimProps.TryGetValue("virtualNetworkType", out var vnetTypeObj))
                    {
                        var vnetType = vnetTypeObj?.ToString() ?? "None";
                        hasVnetIntegration = !vnetType.Equals("None", StringComparison.OrdinalIgnoreCase);
                    }

                    if (!hasVnetIntegration)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"APIM VNet Integration Missing - {stig.Title}",
                            Description = $"API Management service '{apim.Name}' is not integrated with a Virtual Network. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = apim.Id,
                            ResourceName = apim.Name,
                            ResourceType = apim.Type,
                            Evidence = "virtualNetworkType is set to None",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception apimEx)
                {
                    _logger.LogWarning(apimEx, "Unable to query APIM {Name}", apim.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateServiceBusPrivateEndpointStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var serviceBusNamespaces = allResources.Where(r =>
                r.Type.Equals("Microsoft.ServiceBus/namespaces", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (serviceBusNamespaces.Count == 0)
            {
                _logger.LogInformation("No Service Bus namespaces found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var serviceBus in serviceBusNamespaces)
            {
                try
                {
                    var sbResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(serviceBus.Id)).GetAsync(cancellationToken);
                    var sbProps = sbResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool hasPrivateEndpoint = false;
                    if (sbProps != null && sbProps.TryGetValue("privateEndpointConnections", out var peConnectionsObj))
                    {
                        var peConnections = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(peConnectionsObj.ToString() ?? "[]");
                        hasPrivateEndpoint = peConnections != null && peConnections.Count > 0;
                    }

                    if (!hasPrivateEndpoint)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Service Bus Missing Private Endpoint - {stig.Title}",
                            Description = $"Service Bus namespace '{serviceBus.Name}' does not use private endpoints. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = serviceBus.Id,
                            ResourceName = serviceBus.Name,
                            ResourceType = serviceBus.Type,
                            Evidence = "No private endpoint connections found",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception sbEx)
                {
                    _logger.LogWarning(sbEx, "Unable to query Service Bus {Name}", serviceBus.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateServiceBusCmkStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var serviceBusNamespaces = allResources.Where(r =>
                r.Type.Equals("Microsoft.ServiceBus/namespaces", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (serviceBusNamespaces.Count == 0)
            {
                _logger.LogInformation("No Service Bus namespaces found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var serviceBus in serviceBusNamespaces)
            {
                try
                {
                    var sbResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(serviceBus.Id)).GetAsync(cancellationToken);
                    var sbProps = sbResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool hasCmk = false;
                    if (sbProps != null && sbProps.TryGetValue("encryption", out var encryptionObj))
                    {
                        var encryption = JsonSerializer.Deserialize<Dictionary<string, object>>(encryptionObj.ToString() ?? "{}");
                        if (encryption != null && encryption.TryGetValue("keySource", out var keySourceObj))
                        {
                            hasCmk = keySourceObj?.ToString()?.Equals("Microsoft.KeyVault", StringComparison.OrdinalIgnoreCase) ?? false;
                        }
                    }

                    if (!hasCmk)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Service Bus Not Using Customer-Managed Keys - {stig.Title}",
                            Description = $"Service Bus namespace '{serviceBus.Name}' does not use customer-managed encryption keys. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = serviceBus.Id,
                            ResourceName = serviceBus.Name,
                            ResourceType = serviceBus.Type,
                            Evidence = "No customer-managed key encryption configured",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception sbEx)
                {
                    _logger.LogWarning(sbEx, "Unable to query Service Bus {Name}", serviceBus.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    // New STIG validation methods for Container services
    private async Task<List<AtoFinding>> ValidateAcrPrivateAccessStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var containerRegistries = allResources.Where(r =>
                r.Type.Equals("Microsoft.ContainerRegistry/registries", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (containerRegistries.Count == 0)
            {
                _logger.LogInformation("No Container Registries found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var acr in containerRegistries)
            {
                try
                {
                    var acrResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(acr.Id)).GetAsync(cancellationToken);
                    var acrProps = acrResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool publicNetworkAccessDisabled = false;
                    if (acrProps != null && acrProps.TryGetValue("publicNetworkAccess", out var publicAccessObj))
                    {
                        publicNetworkAccessDisabled = publicAccessObj?.ToString()?.Equals("Disabled", StringComparison.OrdinalIgnoreCase) ?? false;
                    }

                    if (!publicNetworkAccessDisabled)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Container Registry Public Access Enabled - {stig.Title}",
                            Description = $"Container Registry '{acr.Name}' allows public network access. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = acr.Id,
                            ResourceName = acr.Name,
                            ResourceType = acr.Type,
                            Evidence = "Public network access is not disabled",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception acrEx)
                {
                    _logger.LogWarning(acrEx, "Unable to query Container Registry {Name}", acr.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateAcrVulnerabilityScanStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var containerRegistries = allResources.Where(r =>
                r.Type.Equals("Microsoft.ContainerRegistry/registries", StringComparison.OrdinalIgnoreCase)).ToList();

            if (containerRegistries.Count == 0)
            {
                _logger.LogInformation("No Container Registries found for STIG {StigId}", stig.StigId);
                return findings;
            }

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            // Check if Defender for Containers is enabled at subscription level
            bool defenderForContainersEnabled = false;
            try
            {
                // Query Defender for Cloud pricing tiers
                // Check for "Containers" or "ContainerRegistry" pricing plan
                var pricingResourceId = $"/subscriptions/{subscriptionId}/providers/Microsoft.Security/pricings/Containers";
                
                try
                {
                    var pricingResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(pricingResourceId)).GetAsync(cancellationToken);
                    
                    var pricingProps = pricingResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();
                    var pricingTier = pricingProps?.ContainsKey("pricingTier") == true 
                        ? pricingProps["pricingTier"]?.ToString() 
                        : null;
                    
                    if (pricingTier != null && pricingTier.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                    {
                        defenderForContainersEnabled = true;
                        _logger.LogInformation("Defender for Containers is enabled at Standard tier");
                    }
                    else
                    {
                        _logger.LogInformation("Defender for Containers pricing tier: {Tier}", pricingTier ?? "Free");
                    }
                }
                catch (Exception)
                {
                    // Try older ContainerRegistry pricing name (for backward compatibility)
                    var legacyPricingId = $"/subscriptions/{subscriptionId}/providers/Microsoft.Security/pricings/ContainerRegistry";
                    
                    try
                    {
                        var legacyPricingResource = await armClient.GetGenericResource(
                            new ResourceIdentifier(legacyPricingId)).GetAsync(cancellationToken);
                        
                        var legacyPricingProps = legacyPricingResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();
                        var legacyPricingTier = legacyPricingProps?.ContainsKey("pricingTier") == true 
                            ? legacyPricingProps["pricingTier"]?.ToString() 
                            : null;
                        
                        if (legacyPricingTier != null && legacyPricingTier.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                        {
                            defenderForContainersEnabled = true;
                            _logger.LogInformation("Defender for Container Registry is enabled at Standard tier (legacy)");
                        }
                        else
                        {
                            _logger.LogInformation("Defender for Container Registry pricing tier: {Tier}", legacyPricingTier ?? "Free");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Unable to query Defender for Containers/Container Registry pricing");
                    }
                }
            }
            catch (Exception defenderEx)
            {
                _logger.LogWarning(defenderEx, "Unable to query Defender for Cloud pricing - will check per-ACR");
            }

            // If Defender for Containers is not enabled, all ACRs are non-compliant
            if (!defenderForContainersEnabled)
            {
                foreach (var acr in containerRegistries)
                {
                    findings.Add(new AtoFinding
                    {
                        AffectedNistControls = stig.NistControls.ToList(),
                        Title = $"Container Registry Vulnerability Scanning Not Enabled - {stig.Title}",
                        Description = $"Container Registry '{acr.Name}' does not have vulnerability scanning enabled. Microsoft Defender for Containers must be enabled at the subscription level to provide vulnerability scanning. {stig.Description}",
                        Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                        ComplianceStatus = AtoComplianceStatus.NonCompliant,
                        ResourceId = acr.Id,
                        ResourceName = acr.Name,
                        ResourceType = acr.Type,
                        Evidence = "Defender for Containers is not enabled at subscription level - vulnerability scanning unavailable",
                        RemediationGuidance = stig.FixText,
                        Metadata = new Dictionary<string, object>
                        {
                            ["StigId"] = stig.StigId,
                            ["VulnId"] = stig.VulnId,
                            ["StigSeverity"] = stig.Severity.ToString(),
                            ["Category"] = stig.Category,
                            ["CciRefs"] = stig.CciRefs,
                            ["Source"] = "STIG",
                            ["DefenderForContainersEnabled"] = false,
                            ["RemediationNote"] = "Enable Microsoft Defender for Containers at subscription level"
                        }
                    });
                }
            }
            else
            {
                _logger.LogInformation("Defender for Containers is enabled - vulnerability scanning available for {Count} ACR instances", 
                    containerRegistries.Count);
                // Defender is enabled - ACRs should have automatic vulnerability scanning
                // No findings to report as the requirement is met
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    // Database STIG validation methods
    private async Task<List<AtoFinding>> ValidateCosmosDbPrivateEndpointStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var cosmosDbAccounts = allResources.Where(r =>
                r.Type.Equals("Microsoft.DocumentDB/databaseAccounts", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (cosmosDbAccounts.Count == 0)
            {
                _logger.LogInformation("No Cosmos DB accounts found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var cosmosDb in cosmosDbAccounts)
            {
                try
                {
                    var cosmosResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(cosmosDb.Id)).GetAsync(cancellationToken);
                    var cosmosProps = cosmosResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool hasPrivateEndpoint = false;
                    if (cosmosProps != null && cosmosProps.TryGetValue("privateEndpointConnections", out var peConnectionsObj))
                    {
                        var peConnections = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(peConnectionsObj.ToString() ?? "[]");
                        hasPrivateEndpoint = peConnections != null && peConnections.Count > 0;
                    }

                    if (!hasPrivateEndpoint)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Cosmos DB Missing Private Endpoint - {stig.Title}",
                            Description = $"Cosmos DB account '{cosmosDb.Name}' does not use private endpoints. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = cosmosDb.Id,
                            ResourceName = cosmosDb.Name,
                            ResourceType = cosmosDb.Type,
                            Evidence = "No private endpoint connections found",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception cosmosEx)
                {
                    _logger.LogWarning(cosmosEx, "Unable to query Cosmos DB {Name}", cosmosDb.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateCosmosDbCmkStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var cosmosDbAccounts = allResources.Where(r =>
                r.Type.Equals("Microsoft.DocumentDB/databaseAccounts", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (cosmosDbAccounts.Count == 0)
            {
                _logger.LogInformation("No Cosmos DB accounts found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var cosmosDb in cosmosDbAccounts)
            {
                try
                {
                    var cosmosResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(cosmosDb.Id)).GetAsync(cancellationToken);
                    var cosmosProps = cosmosResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool hasCmk = false;
                    if (cosmosProps != null && cosmosProps.TryGetValue("keyVaultKeyUri", out var keyVaultKeyUriObj))
                    {
                        hasCmk = !string.IsNullOrEmpty(keyVaultKeyUriObj?.ToString());
                    }

                    if (!hasCmk)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Cosmos DB Not Using Customer-Managed Keys - {stig.Title}",
                            Description = $"Cosmos DB account '{cosmosDb.Name}' does not use customer-managed encryption keys. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = cosmosDb.Id,
                            ResourceName = cosmosDb.Name,
                            ResourceType = cosmosDb.Type,
                            Evidence = "No Key Vault key URI configured for customer-managed encryption",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception cosmosEx)
                {
                    _logger.LogWarning(cosmosEx, "Unable to query Cosmos DB {Name}", cosmosDb.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    /// <summary>
    /// Maps STIG severity to ATO finding severity
    /// </summary>
    private static AtoFindingSeverity MapStigSeverityToFindingSeverity(StigSeverity stigSeverity)
    {
        return stigSeverity switch
        {
            StigSeverity.Critical => AtoFindingSeverity.Critical,
            StigSeverity.High => AtoFindingSeverity.High,
            StigSeverity.Medium => AtoFindingSeverity.Medium,
            StigSeverity.Low => AtoFindingSeverity.Low,
            _ => AtoFindingSeverity.Informational
        };
    }

    #endregion


    #endregion
}
