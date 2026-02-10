using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class Update2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AlterColumn<long>(
                name: "Duration",
                table: "Pipelines",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "DetailedStatus",
                table: "Pipelines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "QueuedDuration",
                table: "Pipelines",
                type: "bigint",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "DetailedStatus",
                table: "Pipelines");

            migrationBuilder.DropColumn(
                name: "QueuedDuration",
                table: "Pipelines");

            migrationBuilder.AlterColumn<long>(
                name: "Duration",
                table: "Pipelines",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
