using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class Backlinks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_Builds_Runners_RunnerId",
                table: "Builds");

            migrationBuilder.DropIndex(
                name: "IX_Builds_DownstreamPipelineId",
                table: "Builds");

            migrationBuilder.RenameColumn(
                name: "RunnerId",
                table: "Builds",
                newName: "PipelineId");

            migrationBuilder.RenameIndex(
                name: "IX_Builds_RunnerId",
                table: "Builds",
                newName: "IX_Builds_PipelineId");

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

            migrationBuilder.CreateIndex(
                name: "IX_Builds_DownstreamPipelineId",
                table: "Builds",
                column: "DownstreamPipelineId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Builds_Pipelines_PipelineId",
                table: "Builds",
                column: "PipelineId",
                principalTable: "Pipelines",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Runners_Builds_BuildId",
                table: "Runners",
                column: "BuildId",
                principalTable: "Builds",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_Builds_Pipelines_PipelineId",
                table: "Builds");

            migrationBuilder.DropForeignKey(
                name: "FK_Runners_Builds_BuildId",
                table: "Runners");

            migrationBuilder.DropIndex(
                name: "IX_Runners_BuildId",
                table: "Runners");

            migrationBuilder.DropIndex(
                name: "IX_Builds_DownstreamPipelineId",
                table: "Builds");

            migrationBuilder.DropColumn(
                name: "BuildId",
                table: "Runners");

            migrationBuilder.RenameColumn(
                name: "PipelineId",
                table: "Builds",
                newName: "RunnerId");

            migrationBuilder.RenameIndex(
                name: "IX_Builds_PipelineId",
                table: "Builds",
                newName: "IX_Builds_RunnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Builds_DownstreamPipelineId",
                table: "Builds",
                column: "DownstreamPipelineId");

            migrationBuilder.AddForeignKey(
                name: "FK_Builds_Runners_RunnerId",
                table: "Builds",
                column: "RunnerId",
                principalTable: "Runners",
                principalColumn: "Id");
        }
    }
}
