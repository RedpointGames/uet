using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class UtilizationBlocks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.CreateTable(
                name: "UtilizationBlocks",
                columns: table => new
                {
                    Week = table.Column<long>(type: "bigint", nullable: false),
                    DayInWeek = table.Column<long>(type: "bigint", nullable: false),
                    HourQuarter = table.Column<long>(type: "bigint", nullable: false),
                    RunnerId = table.Column<long>(type: "bigint", nullable: false),
                    InUse = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilizationBlocks", x => new { x.Week, x.DayInWeek, x.HourQuarter, x.RunnerId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_UtilizationBlocks_RunnerId",
                table: "UtilizationBlocks",
                column: "RunnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilizationBlocks_Week",
                table: "UtilizationBlocks",
                column: "Week");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "UtilizationBlocks");
        }
    }
}
