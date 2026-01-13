using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Engineering.Copilot.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceTemplatesAndProvisionedEnvironments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentHistory_EnvironmentDeployments_DeploymentId",
                table: "DeploymentHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplateFiles_EnvironmentTemplates_TemplateId",
                table: "TemplateFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplateVersions_EnvironmentTemplates_TemplateId",
                table: "TemplateVersions");

            migrationBuilder.DropTable(
                name: "EnvironmentActivities");

            migrationBuilder.DropTable(
                name: "EnvironmentClones");

            migrationBuilder.DropTable(
                name: "EnvironmentCostTrackings");

            migrationBuilder.DropTable(
                name: "EnvironmentMetrics");

            migrationBuilder.DropTable(
                name: "EnvironmentSynchronizations");

            migrationBuilder.DropTable(
                name: "ScalingEvents");

            migrationBuilder.DropTable(
                name: "EnvironmentLifecycles");

            migrationBuilder.DropTable(
                name: "ScalingPolicies");

            migrationBuilder.DropTable(
                name: "EnvironmentDeployments");

            migrationBuilder.DropTable(
                name: "EnvironmentTemplates");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ApprovalWorkflows",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValueSql: "datetime('now')");

            migrationBuilder.CreateTable(
                name: "AgentConfigurations",
                columns: table => new
                {
                    AgentConfigurationId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgentName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IconName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Dependencies = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LastExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HealthStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, defaultValue: "Unknown")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentConfigurations", x => x.AgentConfigurationId);
                });

            migrationBuilder.CreateTable(
                name: "InfrastructureTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TemplateType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Format = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DeploymentTier = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MultiRegionSupported = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisasterRecoverySupported = table.Column<bool>(type: "INTEGER", nullable: false),
                    HighAvailabilitySupported = table.Column<bool>(type: "INTEGER", nullable: false),
                    Parameters = table.Column<string>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    AzureService = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    AutoScalingEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MonitoringEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    BackupEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    FilesCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MainFileType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfrastructureTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Format = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MainTemplateContent = table.Column<string>(type: "TEXT", nullable: false),
                    AdditionalFilesJson = table.Column<string>(type: "TEXT", nullable: true),
                    GitRepositoryUrl = table.Column<string>(type: "TEXT", nullable: true),
                    GitBranch = table.Column<string>(type: "TEXT", nullable: true),
                    GitPath = table.Column<string>(type: "TEXT", nullable: true),
                    GitCommitSha = table.Column<string>(type: "TEXT", nullable: true),
                    LastSyncedFromGit = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GitAutoSync = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    GitSyncIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 15),
                    ParametersJson = table.Column<string>(type: "TEXT", nullable: true),
                    GuardrailsJson = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultTagsJson = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    ApprovalSource = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ApprovedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovalComments = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ExternalApprovalId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExternalApprovalUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ComplianceFrameworks = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    EnforceCompliance = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    DefaultExpirationDays = table.Column<int>(type: "INTEGER", nullable: true),
                    Keywords = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    UseCases = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    AiSelectionHint = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    DeploymentCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    LastDeployedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VersionHistoryJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InfrastructureDeployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EnvironmentType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ResourceGroupName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TemplateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Configuration = table.Column<string>(type: "TEXT", nullable: true),
                    Parameters = table.Column<string>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    DeployedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPollingActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastPolledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PollingAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentPollingInterval = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    ProgressPercentage = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedTimeRemaining = table.Column<TimeSpan>(type: "TEXT", nullable: true),
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
                name: "ProvisionedEnvironments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    TemplateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TemplateName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TemplateVersion = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ResourceGroup = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ParameterValuesJson = table.Column<string>(type: "TEXT", nullable: true),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: true),
                    DeployedResourcesJson = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Provisioning"),
                    StatusMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    DeploymentId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DeploymentDurationMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    OwnerEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ClonedFromId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    LastDriftCheck = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HasDrift = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DriftCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    DriftItemsJson = table.Column<string>(type: "TEXT", nullable: true),
                    EstimatedMonthlyCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ActualMonthlyCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AutoDelete = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvisionedEnvironments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProvisionedEnvironments_ProvisionedEnvironments_ClonedFromId",
                        column: x => x.ClonedFromId,
                        principalTable: "ProvisionedEnvironments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PerformedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OldValuesJson = table.Column<string>(type: "TEXT", nullable: true),
                    NewValuesJson = table.Column<string>(type: "TEXT", nullable: true),
                    ServiceTemplateId = table.Column<Guid>(type: "TEXT", nullable: true)
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
                name: "DeployedResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProvisionedEnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ProvisioningState = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DeployedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()")
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
                name: "EnvironmentDriftItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProvisionedEnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ResourceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PropertyPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ExpectedValue = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ActualValue = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    DriftType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "Configuration"),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Warning"),
                    DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CanAutoRemediate = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsRemediated = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    RemediatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RemediatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
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

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentHistory_InfrastructureDeployments_DeploymentId",
                table: "DeploymentHistory",
                column: "DeploymentId",
                principalTable: "InfrastructureDeployments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TemplateFiles_InfrastructureTemplates_TemplateId",
                table: "TemplateFiles",
                column: "TemplateId",
                principalTable: "InfrastructureTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TemplateVersions_InfrastructureTemplates_TemplateId",
                table: "TemplateVersions",
                column: "TemplateId",
                principalTable: "InfrastructureTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentHistory_InfrastructureDeployments_DeploymentId",
                table: "DeploymentHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplateFiles_InfrastructureTemplates_TemplateId",
                table: "TemplateFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplateVersions_InfrastructureTemplates_TemplateId",
                table: "TemplateVersions");

            migrationBuilder.DropTable(
                name: "AgentConfigurations");

            migrationBuilder.DropTable(
                name: "DeployedResources");

            migrationBuilder.DropTable(
                name: "EnvironmentDriftItems");

            migrationBuilder.DropTable(
                name: "InfrastructureDeployments");

            migrationBuilder.DropTable(
                name: "ServiceTemplateAuditLog");

            migrationBuilder.DropTable(
                name: "ProvisionedEnvironments");

            migrationBuilder.DropTable(
                name: "InfrastructureTemplates");

            migrationBuilder.DropTable(
                name: "ServiceTemplates");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ApprovalWorkflows",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "datetime('now')",
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.CreateTable(
                name: "EnvironmentTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AutoScalingEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AzureService = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BackupEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DeploymentTier = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    DisasterRecoverySupported = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FilesCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Format = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    HighAvailabilitySupported = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    MainFileType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    MonitoringEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MultiRegionSupported = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Parameters = table.Column<string>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    TemplateType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Version = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentDeployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TemplateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActualMonthlyCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Configuration = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CurrentPollingInterval = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeployedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EnvironmentType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EstimatedMonthlyCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    EstimatedTimeRemaining = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPollingActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastPolledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Parameters = table.Column<string>(type: "TEXT", nullable: true),
                    PollingAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    ProgressPercentage = table.Column<int>(type: "INTEGER", nullable: false),
                    ResourceGroupName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentDeployments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvironmentDeployments_EnvironmentTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "EnvironmentTemplates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentClones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceEnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetEnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CloneOperationLog = table.Column<string>(type: "TEXT", nullable: true),
                    CloneType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataMaskingRules = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorDetails = table.Column<string>(type: "TEXT", nullable: true),
                    ExcludedResources = table.Column<string>(type: "TEXT", nullable: true),
                    IncludeData = table.Column<bool>(type: "INTEGER", nullable: false),
                    IncludeSecrets = table.Column<bool>(type: "INTEGER", nullable: false),
                    InitiatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MaskSensitiveData = table.Column<bool>(type: "INTEGER", nullable: false),
                    Progress = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentClones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvironmentClones_EnvironmentDeployments_SourceEnvironmentId",
                        column: x => x.SourceEnvironmentId,
                        principalTable: "EnvironmentDeployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnvironmentClones_EnvironmentDeployments_TargetEnvironmentId",
                        column: x => x.TargetEnvironmentId,
                        principalTable: "EnvironmentDeployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentLifecycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AutoDestroyPolicy = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    CostCenter = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CostThreshold = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    InactivityThresholdHours = table.Column<int>(type: "INTEGER", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LifecycleType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NotificationEmails = table.Column<string>(type: "TEXT", nullable: true),
                    NotificationHours = table.Column<int>(type: "INTEGER", nullable: false),
                    NotifyBeforeDestroy = table.Column<bool>(type: "INTEGER", nullable: false),
                    OwnerTeam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Project = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ScheduledEndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ScheduledStartTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentLifecycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvironmentLifecycles_EnvironmentDeployments_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalTable: "EnvironmentDeployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Labels = table.Column<string>(type: "TEXT", nullable: true),
                    MetricName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MetricType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Value = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvironmentMetrics_EnvironmentDeployments_DeploymentId",
                        column: x => x.DeploymentId,
                        principalTable: "EnvironmentDeployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentSynchronizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceEnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetEnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConflictResolution = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBidirectional = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncLog = table.Column<string>(type: "TEXT", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    NextSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SyncFrequency = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SyncRules = table.Column<string>(type: "TEXT", nullable: true),
                    SyncType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentSynchronizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvironmentSynchronizations_EnvironmentDeployments_SourceEnvironmentId",
                        column: x => x.SourceEnvironmentId,
                        principalTable: "EnvironmentDeployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnvironmentSynchronizations_EnvironmentDeployments_TargetEnvironmentId",
                        column: x => x.TargetEnvironmentId,
                        principalTable: "EnvironmentDeployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScalingPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AutoScalingEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CostOptimizationEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CustomMetrics = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxReplicas = table.Column<int>(type: "INTEGER", nullable: false),
                    MinReplicas = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PolicyType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ScaleDownCooldown = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ScaleUpCooldown = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Schedule = table.Column<string>(type: "TEXT", nullable: true),
                    TargetCpuUtilization = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetMemoryUtilization = table.Column<int>(type: "INTEGER", nullable: false),
                    TrafficBasedScalingEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScalingPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScalingPolicies_EnvironmentDeployments_DeploymentId",
                        column: x => x.DeploymentId,
                        principalTable: "EnvironmentDeployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvironmentLifecycleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActivityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvironmentActivities_EnvironmentLifecycles_EnvironmentLifecycleId",
                        column: x => x.EnvironmentLifecycleId,
                        principalTable: "EnvironmentLifecycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentCostTrackings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvironmentLifecycleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BillingResourceGroup = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CostBreakdown = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CumulativeCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    DailyCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentCostTrackings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvironmentCostTrackings_EnvironmentLifecycles_EnvironmentLifecycleId",
                        column: x => x.EnvironmentLifecycleId,
                        principalTable: "EnvironmentLifecycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScalingEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PolicyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    NewReplicas = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousReplicas = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TriggerDetails = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScalingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScalingEvents_ScalingPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "ScalingPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentActivities_ActivityType",
                table: "EnvironmentActivities",
                column: "ActivityType");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentActivities_EnvironmentLifecycleId_Timestamp",
                table: "EnvironmentActivities",
                columns: new[] { "EnvironmentLifecycleId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentActivities_UserId",
                table: "EnvironmentActivities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentClones_SourceEnvironmentId_TargetEnvironmentId",
                table: "EnvironmentClones",
                columns: new[] { "SourceEnvironmentId", "TargetEnvironmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentClones_StartedAt",
                table: "EnvironmentClones",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentClones_Status",
                table: "EnvironmentClones",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentClones_TargetEnvironmentId",
                table: "EnvironmentClones",
                column: "TargetEnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentCostTrackings_DailyCost",
                table: "EnvironmentCostTrackings",
                column: "DailyCost");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentCostTrackings_Date",
                table: "EnvironmentCostTrackings",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentCostTrackings_Date_Cost",
                table: "EnvironmentCostTrackings",
                columns: new[] { "Date", "DailyCost" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentCostTrackings_EnvironmentLifecycleId_Date",
                table: "EnvironmentCostTrackings",
                columns: new[] { "EnvironmentLifecycleId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentDeployments_CreatedAt",
                table: "EnvironmentDeployments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentDeployments_EnvironmentType_Status",
                table: "EnvironmentDeployments",
                columns: new[] { "EnvironmentType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentDeployments_IsDeleted",
                table: "EnvironmentDeployments",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentDeployments_Name",
                table: "EnvironmentDeployments",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentDeployments_ResourceGroupName",
                table: "EnvironmentDeployments",
                column: "ResourceGroupName");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentDeployments_Subscription_ResourceGroup_Status",
                table: "EnvironmentDeployments",
                columns: new[] { "SubscriptionId", "ResourceGroupName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentDeployments_SubscriptionId",
                table: "EnvironmentDeployments",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentDeployments_TemplateId",
                table: "EnvironmentDeployments",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentLifecycles_EnvironmentId",
                table: "EnvironmentLifecycles",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentLifecycles_LastActivityAt",
                table: "EnvironmentLifecycles",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentLifecycles_LifecycleType_Status",
                table: "EnvironmentLifecycles",
                columns: new[] { "LifecycleType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentLifecycles_OwnerTeam",
                table: "EnvironmentLifecycles",
                column: "OwnerTeam");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentLifecycles_Project",
                table: "EnvironmentLifecycles",
                column: "Project");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentLifecycles_ScheduledEndTime",
                table: "EnvironmentLifecycles",
                column: "ScheduledEndTime");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentLifecycles_Team_Project_Status",
                table: "EnvironmentLifecycles",
                columns: new[] { "OwnerTeam", "Project", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentMetrics_Deployment_Type_Time",
                table: "EnvironmentMetrics",
                columns: new[] { "DeploymentId", "MetricType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentMetrics_MetricName_Timestamp",
                table: "EnvironmentMetrics",
                columns: new[] { "MetricName", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentSynchronizations_IsActive",
                table: "EnvironmentSynchronizations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentSynchronizations_NextSyncAt",
                table: "EnvironmentSynchronizations",
                column: "NextSyncAt");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentSynchronizations_SourceEnvironmentId_TargetEnvironmentId",
                table: "EnvironmentSynchronizations",
                columns: new[] { "SourceEnvironmentId", "TargetEnvironmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentSynchronizations_SyncType",
                table: "EnvironmentSynchronizations",
                column: "SyncType");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentSynchronizations_TargetEnvironmentId",
                table: "EnvironmentSynchronizations",
                column: "TargetEnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentTemplates_CreatedAt",
                table: "EnvironmentTemplates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentTemplates_ExpiresAt",
                table: "EnvironmentTemplates",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentTemplates_IsActive",
                table: "EnvironmentTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentTemplates_Name",
                table: "EnvironmentTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentTemplates_TemplateType_DeploymentTier",
                table: "EnvironmentTemplates",
                columns: new[] { "TemplateType", "DeploymentTier" });

            migrationBuilder.CreateIndex(
                name: "IX_ScalingEvents_EventType",
                table: "ScalingEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_ScalingEvents_PolicyId_CreatedAt",
                table: "ScalingEvents",
                columns: new[] { "PolicyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScalingEvents_Status",
                table: "ScalingEvents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ScalingPolicies_DeploymentId_PolicyType",
                table: "ScalingPolicies",
                columns: new[] { "DeploymentId", "PolicyType" });

            migrationBuilder.CreateIndex(
                name: "IX_ScalingPolicies_IsActive",
                table: "ScalingPolicies",
                column: "IsActive");

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentHistory_EnvironmentDeployments_DeploymentId",
                table: "DeploymentHistory",
                column: "DeploymentId",
                principalTable: "EnvironmentDeployments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TemplateFiles_EnvironmentTemplates_TemplateId",
                table: "TemplateFiles",
                column: "TemplateId",
                principalTable: "EnvironmentTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TemplateVersions_EnvironmentTemplates_TemplateId",
                table: "TemplateVersions",
                column: "TemplateId",
                principalTable: "EnvironmentTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
