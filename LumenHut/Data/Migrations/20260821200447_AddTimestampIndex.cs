using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumenHut.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimestampIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_Timestamp",
                table: "TestRuns",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TestRuns_Timestamp",
                table: "TestRuns");
        }
    }
}
