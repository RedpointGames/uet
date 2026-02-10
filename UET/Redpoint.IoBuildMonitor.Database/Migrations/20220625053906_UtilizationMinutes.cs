using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class UtilizationMinutes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.CreateTable(
                name: "UtilizationMinutes",
                columns: table => new
                {
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RunnerTag = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<long>(type: "bigint", nullable: true),
                    Pending = table.Column<long>(type: "bigint", nullable: true),
                    Running = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilizationMinutes", x => x.Timestamp);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UtilizationMinutes_RunnerTag",
                table: "UtilizationMinutes",
                column: "RunnerTag");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "UtilizationMinutes");
        }
    }
}
