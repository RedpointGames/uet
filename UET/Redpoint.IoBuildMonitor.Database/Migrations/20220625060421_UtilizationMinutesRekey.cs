using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class UtilizationMinutesRekey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropPrimaryKey(
                name: "PK_UtilizationMinutes",
                table: "UtilizationMinutes");

            migrationBuilder.DropIndex(
                name: "IX_UtilizationMinutes_Timestamp_RunnerTag",
                table: "UtilizationMinutes");

            migrationBuilder.AlterColumn<string>(
                name: "RunnerTag",
                table: "UtilizationMinutes",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UtilizationMinutes",
                table: "UtilizationMinutes",
                columns: new[] { "Timestamp", "RunnerTag" });

            migrationBuilder.CreateIndex(
                name: "IX_UtilizationMinutes_Timestamp",
                table: "UtilizationMinutes",
                column: "Timestamp");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropPrimaryKey(
                name: "PK_UtilizationMinutes",
                table: "UtilizationMinutes");

            migrationBuilder.DropIndex(
                name: "IX_UtilizationMinutes_Timestamp",
                table: "UtilizationMinutes");

            migrationBuilder.AlterColumn<string>(
                name: "RunnerTag",
                table: "UtilizationMinutes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UtilizationMinutes",
                table: "UtilizationMinutes",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_UtilizationMinutes_Timestamp_RunnerTag",
                table: "UtilizationMinutes",
                columns: new[] { "Timestamp", "RunnerTag" },
                unique: true);
        }
    }
}
