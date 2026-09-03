using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumenHut.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRunContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OsDescription",
                table: "TestRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProxyUsed",
                table: "TestRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ToolVersion",
                table: "TestRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Viewport",
                table: "TestRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EngineVersion",
                table: "BrowserResults",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OsDescription",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "ProxyUsed",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "ToolVersion",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "Viewport",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "EngineVersion",
                table: "BrowserResults");
        }
    }
}
