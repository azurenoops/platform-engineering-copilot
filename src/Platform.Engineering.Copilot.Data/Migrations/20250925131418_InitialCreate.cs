using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Engineering.Copilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnvironmentTemplates",
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
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntentPatterns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Pattern = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IntentCategory = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IntentAction = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    ParameterExtractionRules = table.Column<string>(type: "TEXT", nullable: true),
                    UsageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SuccessCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SuccessRate = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntentPatterns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SemanticIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserInput = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IntentCategory = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IntentAction = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    ExtractedParameters = table.Column<string>(type: "TEXT", nullable: true),
                    ResolvedToolCall = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    WasSuccessful = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SemanticIntents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentDeployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EnvironmentType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ResourceGroupName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TemplateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Configuration = table.Column<string>(type: "TEXT", nullable: true),
                    Parameters = table.Column<string>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    DeployedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    EstimatedMonthlyCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    ActualMonthlyCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true)
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
                name: "TemplateVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TemplateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    ChangeLog = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeprecated = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateVersions_EnvironmentTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "EnvironmentTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntentFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IntentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FeedbackType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CorrectIntentCategory = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CorrectIntentAction = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CorrectParameters = table.Column<string>(type: "TEXT", nullable: true),
                    ProvidedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                name: "ComplianceScans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScanType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Standard = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TotalChecks = table.Column<int>(type: "INTEGER", nullable: false),
                    PassedChecks = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedChecks = table.Column<int>(type: "INTEGER", nullable: false),
                    WarningChecks = table.Column<int>(type: "INTEGER", nullable: false),
                    ComplianceScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Results = table.Column<string>(type: "TEXT", nullable: true),
                    Recommendations = table.Column<string>(type: "TEXT", nullable: true),
                    InitiatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceScans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceScans_EnvironmentDeployments_DeploymentId",
                        column: x => x.DeploymentId,
                        principalTable: "EnvironmentDeployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    InitiatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentHistory_EnvironmentDeployments_DeploymentId",
                        column: x => x.DeploymentId,
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
                    MetricType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MetricName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Labels = table.Column<string>(type: "TEXT", nullable: true)
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
                name: "ScalingPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PolicyType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MinReplicas = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxReplicas = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetCpuUtilization = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetMemoryUtilization = table.Column<int>(type: "INTEGER", nullable: false),
                    ScaleUpCooldown = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ScaleDownCooldown = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AutoScalingEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CostOptimizationEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    TrafficBasedScalingEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CustomMetrics = table.Column<string>(type: "TEXT", nullable: true),
                    Schedule = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
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
                name: "ComplianceFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RuleId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ResourceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Evidence = table.Column<string>(type: "TEXT", nullable: true),
                    Remediation = table.Column<string>(type: "TEXT", nullable: true),
                    IsRemediable = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsAutomaticallyFixable = table.Column<bool>(type: "INTEGER", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceFindings_ComplianceScans_ScanId",
                        column: x => x.ScanId,
                        principalTable: "ComplianceScans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScalingEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PolicyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PreviousReplicas = table.Column<int>(type: "INTEGER", nullable: false),
                    NewReplicas = table.Column<int>(type: "INTEGER", nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TriggerDetails = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true)
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
                name: "IX_ComplianceFindings_IsRemediable",
                table: "ComplianceFindings",
                column: "IsRemediable");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceFindings_RuleId",
                table: "ComplianceFindings",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceFindings_ScanId_Severity",
                table: "ComplianceFindings",
                columns: new[] { "ScanId", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceFindings_Status",
                table: "ComplianceFindings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceScans_ComplianceScore",
                table: "ComplianceScans",
                column: "ComplianceScore");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceScans_Deployment_Standard_Completed",
                table: "ComplianceScans",
                columns: new[] { "DeploymentId", "Standard", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceScans_DeploymentId_StartedAt",
                table: "ComplianceScans",
                columns: new[] { "DeploymentId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceScans_Standard_Status",
                table: "ComplianceScans",
                columns: new[] { "Standard", "Status" });

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
                name: "IX_EnvironmentMetrics_Deployment_Type_Time",
                table: "EnvironmentMetrics",
                columns: new[] { "DeploymentId", "MetricType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentMetrics_MetricName_Timestamp",
                table: "EnvironmentMetrics",
                columns: new[] { "MetricName", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentTemplates_CreatedAt",
                table: "EnvironmentTemplates",
                column: "CreatedAt");

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
                name: "ComplianceFindings");

            migrationBuilder.DropTable(
                name: "DeploymentHistory");

            migrationBuilder.DropTable(
                name: "EnvironmentMetrics");

            migrationBuilder.DropTable(
                name: "IntentFeedback");

            migrationBuilder.DropTable(
                name: "IntentPatterns");

            migrationBuilder.DropTable(
                name: "ScalingEvents");

            migrationBuilder.DropTable(
                name: "TemplateVersions");

            migrationBuilder.DropTable(
                name: "ComplianceScans");

            migrationBuilder.DropTable(
                name: "SemanticIntents");

            migrationBuilder.DropTable(
                name: "ScalingPolicies");

            migrationBuilder.DropTable(
                name: "EnvironmentDeployments");

            migrationBuilder.DropTable(
                name: "EnvironmentTemplates");
        }
    }
}
