using Platform.Engineering.Copilot.Core.Models.KnowledgeBase;

namespace Platform.Engineering.Copilot.Core.Interfaces.KnowledgeBase;

/// <summary>
/// Service for RMF process and documentation guidance
/// </summary>
public interface IRmfKnowledgeService
{
    Task<RmfProcess?> GetRmfStepAsync(string step, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RmfProcess>> GetAllRmfStepsAsync(CancellationToken cancellationToken = default);
    Task<string> ExplainRmfProcessAsync(string? specificStep = null, CancellationToken cancellationToken = default);
    Task<List<string>> GetRmfOutputsForStepAsync(string step, CancellationToken cancellationToken = default);
    
    // Enhanced: Service-specific workflows from rmf-process-enhanced.json
    Task<string> GetServiceSpecificGuidanceAsync(string service, CancellationToken cancellationToken = default);
    Task<string> GetRmfTimelineAsync(string authorizationType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetRmfArtifactsAsync(bool requiredOnly = false, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for STIG controls and compliance checking
/// </summary>
public interface IStigKnowledgeService
{
    Task<StigControl?> GetStigControlAsync(string stigId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StigControl>> GetStigsByNistControlAsync(string nistControlId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StigControl>> SearchStigsAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StigControl>> GetStigsBySeverityAsync(StigSeverity severity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StigControl>> GetAllStigsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StigControl>> GetStigsByServiceTypeAsync(StigServiceType serviceType, CancellationToken cancellationToken = default);
    Task<ControlMapping?> GetControlMappingAsync(string nistControlId, CancellationToken cancellationToken = default);
    Task<string> ExplainStigAsync(string stigId, CancellationToken cancellationToken = default);
    
    // NEW: Reverse mapping and cross-reference methods
    Task<IReadOnlyList<string>> GetNistControlsForStigAsync(string stigId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StigControl>> GetAzureStigsAsync(string azureService, CancellationToken cancellationToken = default);
    Task<string> GetStigCrossReferenceAsync(string stigId, CancellationToken cancellationToken = default);
    
    // Enhanced: Windows Server STIG support from windows-server-stig-azure.json
    Task<string> GetWindowsServerStigGuidanceAsync(string stigId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> SearchWindowsStigsAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<string> GetGuestConfigurationPolicyAsync(string stigCategory, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for DoD instructions and policy guidance
/// </summary>
public interface IDoDInstructionService
{
    Task<DoDInstruction?> GetInstructionAsync(string instructionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DoDInstruction>> GetInstructionsByControlAsync(string nistControlId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DoDInstruction>> SearchInstructionsAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<string> ExplainInstructionAsync(string instructionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for Navy/DoD workflow documentation
/// </summary>
public interface IDoDWorkflowService
{
    Task<DoDWorkflow?> GetWorkflowAsync(string workflowId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DoDWorkflow>> GetWorkflowsByOrganizationAsync(DoDOrganization organization, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DoDWorkflow>> GetWorkflowsByImpactLevelAsync(string impactLevel, CancellationToken cancellationToken = default);
    Task<string> ExplainWorkflowAsync(string workflowId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for Impact Level requirements and guidance
/// </summary>
public interface IImpactLevelService
{
    Task<ImpactLevel?> GetImpactLevelAsync(string level, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImpactLevel>> GetAllImpactLevelsAsync(CancellationToken cancellationToken = default);
    Task<BoundaryProtectionRequirement?> GetBoundaryRequirementsAsync(string impactLevel, CancellationToken cancellationToken = default);
    Task<string> ExplainImpactLevelAsync(string level, CancellationToken cancellationToken = default);
    
    // Enhanced: Detailed IL data from impact-levels.json
    Task<string> GetImpactLevelComparisonAsync(CancellationToken cancellationToken = default);
    Task<string> GetMigrationGuidanceAsync(string fromLevel, string toLevel, CancellationToken cancellationToken = default);
    Task<string> GetAzureImplementationAsync(string level, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for FedRAMP templates and authorization guidance
/// </summary>
public interface IFedRampTemplateService
{
    Task<string> GetSspSectionTemplateAsync(string sectionNumber, CancellationToken cancellationToken = default);
    Task<string> GetControlNarrativeAsync(string controlId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetAuthorizationPackageChecklistAsync(CancellationToken cancellationToken = default);
    Task<string> GetPoamTemplateAsync(CancellationToken cancellationToken = default);
    Task<string> GetContinuousMonitoringRequirementsAsync(CancellationToken cancellationToken = default);
    Task<string> ExplainAuthorizationPathAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Unified knowledge base service for all DoD/Navy compliance information
/// </summary>
public interface IComplianceKnowledgeBaseService
{
    Task<IReadOnlyList<KnowledgeBaseSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<string> ExplainControlAsync(string nistControlId, bool includeRmf = true, bool includeStig = true, bool includeDoDInstructions = true, CancellationToken cancellationToken = default);
    Task<AtoPackageRequirement?> GetAtoRequirementAsync(string documentType, string impactLevel, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AtoPackageRequirement>> GetAtoPackageRequirementsAsync(string impactLevel, CancellationToken cancellationToken = default);
    Task<EmassRequirement?> GetEmassRequirementsAsync(string systemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CcriRequirement>> GetCcriRequirementsAsync(CancellationToken cancellationToken = default);
}
