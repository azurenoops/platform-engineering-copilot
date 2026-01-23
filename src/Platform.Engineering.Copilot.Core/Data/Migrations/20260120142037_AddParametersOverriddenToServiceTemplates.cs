using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Engineering.Copilot.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParametersOverriddenToServiceTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ParametersOverridden",
                table: "ServiceTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParametersOverridden",
                table: "ServiceTemplates");
        }
    }
}
