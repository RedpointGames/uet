using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class TestsDurationHash : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<string>(
                name: "DurationEstimationHash",
                table: "Tests",
                type: "text",
                nullable: true,
                computedColumnSql: "\"FullName\" || ':' || \"Platform\"",
                stored: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "DurationEstimationHash",
                table: "Tests");
        }
    }
}
