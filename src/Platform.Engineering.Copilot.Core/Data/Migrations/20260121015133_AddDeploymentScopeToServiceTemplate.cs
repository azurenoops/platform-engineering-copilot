using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Engineering.Copilot.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeploymentScopeToServiceTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeploymentScope",
                table: "ServiceTemplates",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeploymentScope",
                table: "ServiceTemplates");
        }
    }
}
