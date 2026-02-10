using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class UtilizationMinutesUniqueConstraint : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.CreateIndex(
                name: "IX_UtilizationMinutes_Timestamp_RunnerTag",
                table: "UtilizationMinutes",
                columns: new[] { "Timestamp", "RunnerTag" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "IX_UtilizationMinutes_Timestamp_RunnerTag",
                table: "UtilizationMinutes");
        }
    }
}
