using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Engineering.Copilot.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateExpiration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "EnvironmentTemplates",
                type: "TEXT",
                nullable: true);

            // Create index for efficient expired template queries
            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentTemplates_ExpiresAt",
                table: "EnvironmentTemplates",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EnvironmentTemplates_ExpiresAt",
                table: "EnvironmentTemplates");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "EnvironmentTemplates");
        }
    }
}
