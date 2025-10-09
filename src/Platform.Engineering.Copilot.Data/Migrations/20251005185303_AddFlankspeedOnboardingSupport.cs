using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Engineering.Copilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFlankspeedOnboardingSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OnboardingRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MissionName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MissionOwner = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MissionOwnerEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MissionOwnerRank = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Command = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ClassificationLevel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "UNCLASS"),
                    RequestedSubscriptionName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RequestedVNetCidr = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RequiredServicesJson = table.Column<string>(type: "TEXT", nullable: false),
                    Region = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "usgovvirginia"),
                    EstimatedUserCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DataResidency = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "US"),
                    EstimatedDataVolumeTB = table.Column<decimal>(type: "TEXT", nullable: false),
                    RequiresPki = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresCac = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    RequiresAto = table.Column<bool>(type: "INTEGER", nullable: false),
                    SecurityContactEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ComplianceFrameworksJson = table.Column<string>(type: "TEXT", nullable: false),
                    BusinessJustification = table.Column<string>(type: "TEXT", nullable: false),
                    UseCase = table.Column<string>(type: "TEXT", nullable: false),
                    RequestedStartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FundingSource = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EstimatedMonthlyCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    MissionDurationMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProvisionedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    ApprovedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ApprovalComments = table.Column<string>(type: "TEXT", nullable: true),
                    RejectedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RejectionReason = table.Column<string>(type: "TEXT", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 3),
                    ProvisionedSubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ProvisionedVNetId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ProvisionedResourceGroupId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ProvisionedResourcesJson = table.Column<string>(type: "TEXT", nullable: false),
                    ProvisioningJobId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ProvisioningError = table.Column<string>(type: "TEXT", nullable: true),
                    NotificationSent = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotificationSentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NotificationHistoryJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingRequests_ClassificationLevel",
                table: "OnboardingRequests",
                column: "ClassificationLevel");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingRequests_Command",
                table: "OnboardingRequests",
                column: "Command");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingRequests_CreatedAt",
                table: "OnboardingRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingRequests_MissionOwnerEmail",
                table: "OnboardingRequests",
                column: "MissionOwnerEmail");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingRequests_Status",
                table: "OnboardingRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingRequests_Status_Priority_CreatedAt",
                table: "OnboardingRequests",
                columns: new[] { "Status", "Priority", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnboardingRequests");
        }
    }
}
