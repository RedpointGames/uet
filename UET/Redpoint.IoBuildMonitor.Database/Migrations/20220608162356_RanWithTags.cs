using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class RanWithTags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<string[]>(
                name: "RanWithTags",
                table: "Builds",
                type: "text[]",
                nullable: false,
                defaultValue: Array.Empty<string>());
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "RanWithTags",
                table: "Builds");
        }
    }
}
