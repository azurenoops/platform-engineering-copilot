using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Engineering.Copilot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentPollingInterval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoScalingEnabled",
                table: "EnvironmentTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AzureService",
                table: "EnvironmentTemplates",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BackupEnabled",
                table: "EnvironmentTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MonitoringEnabled",
                table: "EnvironmentTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "EnvironmentDeployments",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "CurrentPollingInterval",
                table: "EnvironmentDeployments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "EstimatedTimeRemaining",
                table: "EnvironmentDeployments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPollingActive",
                table: "EnvironmentDeployments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPolledAt",
                table: "EnvironmentDeployments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PollingAttempts",
                table: "EnvironmentDeployments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProgressPercentage",
                table: "EnvironmentDeployments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoScalingEnabled",
                table: "EnvironmentTemplates");

            migrationBuilder.DropColumn(
                name: "AzureService",
                table: "EnvironmentTemplates");

            migrationBuilder.DropColumn(
                name: "BackupEnabled",
                table: "EnvironmentTemplates");

            migrationBuilder.DropColumn(
                name: "MonitoringEnabled",
                table: "EnvironmentTemplates");

            migrationBuilder.DropColumn(
                name: "CurrentPollingInterval",
                table: "EnvironmentDeployments");

            migrationBuilder.DropColumn(
                name: "EstimatedTimeRemaining",
                table: "EnvironmentDeployments");

            migrationBuilder.DropColumn(
                name: "IsPollingActive",
                table: "EnvironmentDeployments");

            migrationBuilder.DropColumn(
                name: "LastPolledAt",
                table: "EnvironmentDeployments");

            migrationBuilder.DropColumn(
                name: "PollingAttempts",
                table: "EnvironmentDeployments");

            migrationBuilder.DropColumn(
                name: "ProgressPercentage",
                table: "EnvironmentDeployments");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "EnvironmentDeployments",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }
    }
}
