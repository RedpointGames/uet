using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class MultipleBuildsPerRunner : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_Runners_Builds_BuildId",
                table: "Runners");

            migrationBuilder.DropIndex(
                name: "IX_Runners_BuildId",
                table: "Runners");

            migrationBuilder.DropColumn(
                name: "BuildId",
                table: "Runners");

            migrationBuilder.AddColumn<long>(
                name: "RunnerId",
                table: "Builds",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Builds_RunnerId",
                table: "Builds",
                column: "RunnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Builds_Runners_RunnerId",
                table: "Builds",
                column: "RunnerId",
                principalTable: "Runners",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_Builds_Runners_RunnerId",
                table: "Builds");

            migrationBuilder.DropIndex(
                name: "IX_Builds_RunnerId",
                table: "Builds");

            migrationBuilder.DropColumn(
                name: "RunnerId",
                table: "Builds");

            migrationBuilder.AddColumn<long>(
                name: "BuildId",
                table: "Runners",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Runners_BuildId",
                table: "Runners",
                column: "BuildId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Runners_Builds_BuildId",
                table: "Runners",
                column: "BuildId",
                principalTable: "Builds",
                principalColumn: "Id");
        }
    }
}
