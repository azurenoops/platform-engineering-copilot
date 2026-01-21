using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Engineering.Copilot.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_SqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentConfigurations",
                columns: table => new
                {
                    AgentConfigurationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IconName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ModifiedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Dependencies = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HealthStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, defaultValue: "Unknown")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentConfigurations", x => x.AgentConfigurationId);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalWorkflows",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ToolCallId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Justification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    ResourceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResourceGroupName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequiredApproversJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PolicyViolationsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalToolCallJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DecisionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestPayload = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalWorkflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    EntryId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResourceName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TagsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangeDetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComplianceContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EntryHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.EntryId);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceAssessments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubscriptionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResourceGroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssessmentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ComplianceScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TotalFindings = table.Column<int>(type: "int", nullable: false),
                    CriticalFindings = table.Column<int>(type: "int", nullable: false),
                    HighFindings = table.Column<int>(type: "int", nullable: false),
                    MediumFindings = table.Column<int>(type: "int", nullable: false),
                    LowFindings = table.Column<int>(type: "int", nullable: false),
                    InformationalFindings = table.Column<int>(type: "int", nullable: false),
                    ExecutiveSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RiskProfile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Results = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Recommendations = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InitiatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Duration = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceAssessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InfrastructureTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TemplateType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeploymentTier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MultiRegionSupported = table.Column<bool>(type: "bit", nullable: false),
                    DisasterRecoverySupported = table.Column<bool>(type: "bit", nullable: false),
                    HighAvailabilitySupported = table.Column<bool>(type: "bit", nullable: false),
                    Parameters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    AzureService = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AutoScalingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MonitoringEnabled = table.Column<bool>(type: "bit", nullable: false),
                    BackupEnabled = table.Column<bool>(type: "bit", nullable: false),
                    FilesCount = table.Column<int>(type: "int", nullable: false),
                    MainFileType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfrastructureTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntentPatterns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Pattern = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IntentCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IntentAction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    ParameterExtractionRules = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UsageCount = table.Column<int>(type: "int", nullable: false),
                    SuccessCount = table.Column<int>(type: "int", nullable: false),
                    SuccessRate = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntentPatterns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SemanticIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserInput = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IntentCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IntentAction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    ExtractedParameters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolvedToolCall = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    WasSuccessful = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SemanticIntents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MainTemplateContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdditionalFilesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GitRepositoryUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GitBranch = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GitPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GitCommitSha = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastSyncedFromGit = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GitAutoSync = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    GitSyncIntervalMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 15),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GuardrailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultTagsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ApprovalSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExternalApprovalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExternalApprovalUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ComplianceFrameworks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EnforceCompliance = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DefaultExpirationDays = table.Column<int>(type: "int", nullable: true),
                    Keywords = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UseCases = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AiSelectionHint = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DeploymentCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastDeployedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VersionHistoryJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FindingId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RuleId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ComplianceStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FindingType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResourceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ControlId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ComplianceFrameworks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AffectedNistControls = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Evidence = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remediation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRemediable = table.Column<bool>(type: "bit", nullable: false),
                    IsAutomaticallyFixable = table.Column<bool>(type: "bit", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceFindings_ComplianceAssessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "ComplianceAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InfrastructureDeployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EnvironmentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResourceGroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubscriptionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Configuration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Parameters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeployedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsPollingActive = table.Column<bool>(type: "bit", nullable: false),
                    LastPolledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PollingAttempts = table.Column<int>(type: "int", nullable: false),
                    CurrentPollingInterval = table.Column<TimeSpan>(type: "time", nullable: true),
                    ProgressPercentage = table.Column<int>(type: "int", nullable: false),
                    EstimatedTimeRemaining = table.Column<TimeSpan>(type: "time", nullable: true),
                    EstimatedMonthlyCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    ActualMonthlyCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfrastructureDeployments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InfrastructureDeployments_InfrastructureTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "InfrastructureTemplates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TemplateFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsEntryPoint = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateFiles_InfrastructureTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "InfrastructureTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangeLog = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeprecated = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateVersions_InfrastructureTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "InfrastructureTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntentFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeedbackType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CorrectIntentCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CorrectIntentAction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CorrectParameters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProvidedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntentFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntentFeedback_SemanticIntents_IntentId",
                        column: x => x.IntentId,
                        principalTable: "SemanticIntents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProvisionedEnvironments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TemplateVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubscriptionId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResourceGroup = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ParameterValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TagsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeployedResourcesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Provisioning"),
                    StatusMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DeploymentId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeploymentDurationMinutes = table.Column<int>(type: "int", nullable: true),
                    OwnerEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClonedFromId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastDriftCheck = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HasDrift = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DriftCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DriftItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstimatedMonthlyCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ActualMonthlyCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AutoDelete = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvisionedEnvironments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProvisionedEnvironments_ProvisionedEnvironments_ClonedFromId",
                        column: x => x.ClonedFromId,
                        principalTable: "ProvisionedEnvironments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProvisionedEnvironments_ServiceTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ServiceTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceTemplateAuditLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PerformedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OldValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ServiceTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTemplateAuditLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceTemplateAuditLog_ServiceTemplates_ServiceTemplateId",
                        column: x => x.ServiceTemplateId,
                        principalTable: "ServiceTemplates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DeploymentHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InitiatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentHistory_InfrastructureDeployments_DeploymentId",
                        column: x => x.DeploymentId,
                        principalTable: "InfrastructureDeployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeployedResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProvisionedEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProvisioningState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeployedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeployedResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeployedResources_ProvisionedEnvironments_ProvisionedEnvironmentId",
                        column: x => x.ProvisionedEnvironmentId,
                        principalTable: "ProvisionedEnvironments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentLifecycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Completed"),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvironmentActivities_ProvisionedEnvironments_EnvironmentLifecycleId",
                        column: x => x.EnvironmentLifecycleId,
                        principalTable: "ProvisionedEnvironments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentAuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PerformedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvironmentAuditEntries_ProvisionedEnvironments_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalTable: "ProvisionedEnvironments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentDriftItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProvisionedEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PropertyPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExpectedValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ActualValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DriftType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Configuration"),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Warning"),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CanAutoRemediate = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsRemediated = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RemediatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemediatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentDriftItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvironmentDriftItems_ProvisionedEnvironments_ProvisionedEnvironmentId",
                        column: x => x.ProvisionedEnvironmentId,
                        principalTable: "ProvisionedEnvironments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentConfigurations_AgentName",
                table: "AgentConfigurations",
                column: "AgentName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentConfigurations_Category",
                table: "AgentConfigurations",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_AgentConfigurations_Category_IsEnabled_DisplayOrder",
                table: "AgentConfigurations",
                columns: new[] { "Category", "IsEnabled", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentConfigurations_IsEnabled",
                table: "AgentConfigurations",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_CreatedAt",
                table: "ApprovalWorkflows",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_Environment",
                table: "ApprovalWorkflows",
                column: "Environment");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_ExpiresAt",
                table: "ApprovalWorkflows",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_RequestedBy",
                table: "ApprovalWorkflows",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_ResourceGroup_Environment_Status",
                table: "ApprovalWorkflows",
                columns: new[] { "ResourceGroupName", "Environment", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_ResourceType",
                table: "ApprovalWorkflows",
                column: "ResourceType");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_Status",
                table: "ApprovalWorkflows",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_Status_Priority_CreatedAt",
                table: "ApprovalWorkflows",
                columns: new[] { "Status", "Priority", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Actor_Time",
                table: "AuditLogs",
                columns: new[] { "ActorId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorId",
                table: "AuditLogs",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CorrelationId",
                table: "AuditLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EventType",
                table: "AuditLogs",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Resource_Action_Time",
                table: "AuditLogs",
                columns: new[] { "ResourceId", "Action", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ResourceId",
                table: "AuditLogs",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Severity",
                table: "AuditLogs",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Time_Severity",
                table: "AuditLogs",
                columns: new[] { "Timestamp", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAssessments_StartedAt",
                table: "ComplianceAssessments",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAssessments_Status",
                table: "ComplianceAssessments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAssessments_Status_StartedAt",
                table: "ComplianceAssessments",
                columns: new[] { "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAssessments_SubscriptionId",
                table: "ComplianceAssessments",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAssessments_SubscriptionId_AssessmentType",
                table: "ComplianceAssessments",
                columns: new[] { "SubscriptionId", "AssessmentType" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceFindings_AssessmentId",
                table: "ComplianceFindings",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceFindings_AssessmentId_Severity",
                table: "ComplianceFindings",
                columns: new[] { "AssessmentId", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceFindings_ComplianceStatus",
                table: "ComplianceFindings",
                column: "ComplianceStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceFindings_ComplianceStatus_Severity",
                table: "ComplianceFindings",
                columns: new[] { "ComplianceStatus", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceFindings_DetectedAt",
                table: "ComplianceFindings",
                column: "DetectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceFindings_FindingType",
                table: "ComplianceFindings",
                column: "FindingType");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceFindings_ResourceType",
                table: "ComplianceFindings",
                column: "ResourceType");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceFindings_RuleId",
                table: "ComplianceFindings",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceFindings_Severity",
                table: "ComplianceFindings",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_DeployedResources_ProvisionedEnvironmentId",
                table: "DeployedResources",
                column: "ProvisionedEnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DeployedResources_ProvisioningState",
                table: "DeployedResources",
                column: "ProvisioningState");

            migrationBuilder.CreateIndex(
                name: "IX_DeployedResources_ResourceType",
                table: "DeployedResources",
                column: "ResourceType");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentHistory_Action",
                table: "DeploymentHistory",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentHistory_DeploymentId_StartedAt",
                table: "DeploymentHistory",
                columns: new[] { "DeploymentId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentHistory_Status",
                table: "DeploymentHistory",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentActivities_ActivityType",
                table: "EnvironmentActivities",
                column: "ActivityType");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentActivities_EnvironmentLifecycleId",
                table: "EnvironmentActivities",
                column: "EnvironmentLifecycleId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentActivities_EnvironmentLifecycleId_ActivityType",
                table: "EnvironmentActivities",
                columns: new[] { "EnvironmentLifecycleId", "ActivityType" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentActivities_EnvironmentLifecycleId_Timestamp",
                table: "EnvironmentActivities",
                columns: new[] { "EnvironmentLifecycleId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentActivities_Status",
                table: "EnvironmentActivities",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentActivities_Timestamp",
                table: "EnvironmentActivities",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentAuditEntries_EnvironmentId",
                table: "EnvironmentAuditEntries",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentDriftItems_IsRemediated",
                table: "EnvironmentDriftItems",
                column: "IsRemediated");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentDriftItems_ProvisionedEnvironmentId",
                table: "EnvironmentDriftItems",
                column: "ProvisionedEnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentDriftItems_ProvisionedEnvironmentId_IsRemediated",
                table: "EnvironmentDriftItems",
                columns: new[] { "ProvisionedEnvironmentId", "IsRemediated" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentDriftItems_Severity",
                table: "EnvironmentDriftItems",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_InfrastructureDeployments_CreatedAt",
                table: "InfrastructureDeployments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InfrastructureDeployments_EnvironmentType_Status",
                table: "InfrastructureDeployments",
                columns: new[] { "EnvironmentType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InfrastructureDeployments_IsDeleted",
                table: "InfrastructureDeployments",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_InfrastructureDeployments_Name",
                table: "InfrastructureDeployments",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_InfrastructureDeployments_ResourceGroupName",
                table: "InfrastructureDeployments",
                column: "ResourceGroupName");

            migrationBuilder.CreateIndex(
                name: "IX_InfrastructureDeployments_Subscription_ResourceGroup_Status",
                table: "InfrastructureDeployments",
                columns: new[] { "SubscriptionId", "ResourceGroupName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InfrastructureDeployments_SubscriptionId",
                table: "InfrastructureDeployments",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_InfrastructureDeployments_TemplateId",
                table: "InfrastructureDeployments",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_InfrastructureTemplates_CreatedAt",
                table: "InfrastructureTemplates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InfrastructureTemplates_IsActive",
                table: "InfrastructureTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_InfrastructureTemplates_Name",
                table: "InfrastructureTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InfrastructureTemplates_TemplateType_DeploymentTier",
                table: "InfrastructureTemplates",
                columns: new[] { "TemplateType", "DeploymentTier" });

            migrationBuilder.CreateIndex(
                name: "IX_IntentFeedback_CreatedAt",
                table: "IntentFeedback",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_IntentFeedback_IntentId_FeedbackType",
                table: "IntentFeedback",
                columns: new[] { "IntentId", "FeedbackType" });

            migrationBuilder.CreateIndex(
                name: "IX_IntentPatterns_IntentCategory_IntentAction",
                table: "IntentPatterns",
                columns: new[] { "IntentCategory", "IntentAction" });

            migrationBuilder.CreateIndex(
                name: "IX_IntentPatterns_IsActive",
                table: "IntentPatterns",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_IntentPatterns_SuccessRate",
                table: "IntentPatterns",
                column: "SuccessRate");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionedEnvironments_ClonedFromId",
                table: "ProvisionedEnvironments",
                column: "ClonedFromId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionedEnvironments_CreatedAt",
                table: "ProvisionedEnvironments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionedEnvironments_CreatedBy",
                table: "ProvisionedEnvironments",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionedEnvironments_ExpiresAt",
                table: "ProvisionedEnvironments",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionedEnvironments_HasDrift",
                table: "ProvisionedEnvironments",
                column: "HasDrift");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionedEnvironments_Name",
                table: "ProvisionedEnvironments",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionedEnvironments_OwnerEmail",
                table: "ProvisionedEnvironments",
                column: "OwnerEmail");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionedEnvironments_ResourceGroup",
                table: "ProvisionedEnvironments",
                column: "ResourceGroup");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionedEnvironments_Status",
                table: "ProvisionedEnvironments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionedEnvironments_Status_HasDrift",
                table: "ProvisionedEnvironments",
                columns: new[] { "Status", "HasDrift" });

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionedEnvironments_SubscriptionId",
                table: "ProvisionedEnvironments",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionedEnvironments_SubscriptionId_ResourceGroup",
                table: "ProvisionedEnvironments",
                columns: new[] { "SubscriptionId", "ResourceGroup" });

            migrationBuilder.CreateIndex(
                name: "IX_ProvisionedEnvironments_TemplateId",
                table: "ProvisionedEnvironments",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_SemanticIntents_Confidence",
                table: "SemanticIntents",
                column: "Confidence");

            migrationBuilder.CreateIndex(
                name: "IX_SemanticIntents_IntentCategory_IntentAction",
                table: "SemanticIntents",
                columns: new[] { "IntentCategory", "IntentAction" });

            migrationBuilder.CreateIndex(
                name: "IX_SemanticIntents_UserId_CreatedAt",
                table: "SemanticIntents",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SemanticIntents_WasSuccessful",
                table: "SemanticIntents",
                column: "WasSuccessful");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTemplateAuditLog_Action",
                table: "ServiceTemplateAuditLog",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTemplateAuditLog_EntityType_EntityId",
                table: "ServiceTemplateAuditLog",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTemplateAuditLog_PerformedBy",
                table: "ServiceTemplateAuditLog",
                column: "PerformedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTemplateAuditLog_ServiceTemplateId",
                table: "ServiceTemplateAuditLog",
                column: "ServiceTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTemplateAuditLog_Timestamp",
                table: "ServiceTemplateAuditLog",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTemplates_Category",
                table: "ServiceTemplates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTemplates_Category_Status",
                table: "ServiceTemplates",
                columns: new[] { "Category", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTemplates_CreatedAt",
                table: "ServiceTemplates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTemplates_CreatedBy",
                table: "ServiceTemplates",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTemplates_Name_Version",
                table: "ServiceTemplates",
                columns: new[] { "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTemplates_Status",
                table: "ServiceTemplates",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateFiles_TemplateId",
                table: "TemplateFiles",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateVersions_CreatedAt",
                table: "TemplateVersions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateVersions_TemplateId_Version",
                table: "TemplateVersions",
                columns: new[] { "TemplateId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentConfigurations");

            migrationBuilder.DropTable(
                name: "ApprovalWorkflows");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ComplianceFindings");

            migrationBuilder.DropTable(
                name: "DeployedResources");

            migrationBuilder.DropTable(
                name: "DeploymentHistory");

            migrationBuilder.DropTable(
                name: "EnvironmentActivities");

            migrationBuilder.DropTable(
                name: "EnvironmentAuditEntries");

            migrationBuilder.DropTable(
                name: "EnvironmentDriftItems");

            migrationBuilder.DropTable(
                name: "IntentFeedback");

            migrationBuilder.DropTable(
                name: "IntentPatterns");

            migrationBuilder.DropTable(
                name: "ServiceTemplateAuditLog");

            migrationBuilder.DropTable(
                name: "TemplateFiles");

            migrationBuilder.DropTable(
                name: "TemplateVersions");

            migrationBuilder.DropTable(
                name: "ComplianceAssessments");

            migrationBuilder.DropTable(
                name: "InfrastructureDeployments");

            migrationBuilder.DropTable(
                name: "ProvisionedEnvironments");

            migrationBuilder.DropTable(
                name: "SemanticIntents");

            migrationBuilder.DropTable(
                name: "InfrastructureTemplates");

            migrationBuilder.DropTable(
                name: "ServiceTemplates");
        }
    }
}
