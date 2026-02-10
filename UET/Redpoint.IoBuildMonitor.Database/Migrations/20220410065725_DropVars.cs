using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class DropVars : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "Variables",
                table: "Pipelines");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "Variables",
                table: "Pipelines",
                type: "hstore",
                nullable: true);
        }
    }
}
