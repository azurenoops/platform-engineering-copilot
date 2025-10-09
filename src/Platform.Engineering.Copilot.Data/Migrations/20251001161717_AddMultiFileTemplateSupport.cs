using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Engineering.Copilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiFileTemplateSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FilesCount",
                table: "EnvironmentTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MainFileType",
                table: "EnvironmentTemplates",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "EnvironmentTemplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EnvironmentClones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceEnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetEnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CloneType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InitiatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IncludeData = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaskSensitiveData = table.Column<bool>(type: "INTEGER", nullable: false),
                    IncludeSecrets = table.Column<bool>(type: "INTEGER", nullable: false),
                    DataMaskingRules = table.Column<string>(type: "TEXT", nullable: true),
                    ExcludedResources = table.Column<string>(type: "TEXT", nullable: true),
                    CloneOperationLog = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorDetails = table.Column<string>(type: "TEXT", nullable: true),
                    Progress = table.Column<int>(type: "INTEGER", nullable: false)
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
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LifecycleType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ScheduledStartTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ScheduledEndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AutoDestroyPolicy = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    InactivityThresholdHours = table.Column<int>(type: "INTEGER", nullable: false),
                    CostThreshold = table.Column<decimal>(type: "TEXT", nullable: false),
                    OwnerTeam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Project = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CostCenter = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    NotificationEmails = table.Column<string>(type: "TEXT", nullable: true),
                    NotifyBeforeDestroy = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotificationHours = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastActivityAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
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
                name: "EnvironmentSynchronizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceEnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetEnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SyncType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SyncFrequency = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBidirectional = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    SyncRules = table.Column<string>(type: "TEXT", nullable: true),
                    ConflictResolution = table.Column<string>(type: "TEXT", nullable: true),
                    LastSyncLog = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
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
                name: "TemplateFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TemplateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    FileType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsEntryPoint = table.Column<bool>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateFiles_EnvironmentTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "EnvironmentTemplates",
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
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
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
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DailyCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CumulativeCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    CostBreakdown = table.Column<string>(type: "TEXT", nullable: true),
                    BillingResourceGroup = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                name: "IX_TemplateFiles_TemplateId",
                table: "TemplateFiles",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnvironmentActivities");

            migrationBuilder.DropTable(
                name: "EnvironmentClones");

            migrationBuilder.DropTable(
                name: "EnvironmentCostTrackings");

            migrationBuilder.DropTable(
                name: "EnvironmentSynchronizations");

            migrationBuilder.DropTable(
                name: "TemplateFiles");

            migrationBuilder.DropTable(
                name: "EnvironmentLifecycles");

            migrationBuilder.DropColumn(
                name: "FilesCount",
                table: "EnvironmentTemplates");

            migrationBuilder.DropColumn(
                name: "MainFileType",
                table: "EnvironmentTemplates");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "EnvironmentTemplates");
        }
    }
}
