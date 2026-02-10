using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class TestInstancing : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.RenameColumn(
                name: "Instance",
                table: "Tests",
                newName: "GauntletInstance");

            migrationBuilder.AddColumn<string>(
                name: "AutomationInstance",
                table: "Tests",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LookupId",
                table: "Tests",
                type: "text",
                nullable: false,
                computedColumnSql: "\"BuildId\"::text || ':' || \"FullName\" || ':' || \"Platform\" || ':' || \"GauntletInstance\"",
                stored: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldComputedColumnSql: "\"BuildId\"::text || ':' || \"FullName\" || ':' || \"Platform\" || ':' || \"Instance\"",
                oldStored: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "AutomationInstance",
                table: "Tests");

            migrationBuilder.RenameColumn(
                name: "GauntletInstance",
                table: "Tests",
                newName: "Instance");

            migrationBuilder.AlterColumn<string>(
                name: "LookupId",
                table: "Tests",
                type: "text",
                nullable: false,
                computedColumnSql: "\"BuildId\"::text || ':' || \"FullName\" || ':' || \"Platform\" || ':' || \"Instance\"",
                stored: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldComputedColumnSql: "\"BuildId\"::text || ':' || \"FullName\" || ':' || \"Platform\" || ':' || \"GauntletInstance\"",
                oldStored: true);
        }
    }
}
