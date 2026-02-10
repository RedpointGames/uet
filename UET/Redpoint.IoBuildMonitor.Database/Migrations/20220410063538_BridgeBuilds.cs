using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class BridgeBuilds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_Builds_Pipelines_PipelineEntityId",
                table: "Builds");

            migrationBuilder.RenameColumn(
                name: "PipelineEntityId",
                table: "Builds",
                newName: "DownstreamPipelineId");

            migrationBuilder.RenameIndex(
                name: "IX_Builds_PipelineEntityId",
                table: "Builds",
                newName: "IX_Builds_DownstreamPipelineId");

            migrationBuilder.AddForeignKey(
                name: "FK_Builds_Pipelines_DownstreamPipelineId",
                table: "Builds",
                column: "DownstreamPipelineId",
                principalTable: "Pipelines",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_Builds_Pipelines_DownstreamPipelineId",
                table: "Builds");

            migrationBuilder.RenameColumn(
                name: "DownstreamPipelineId",
                table: "Builds",
                newName: "PipelineEntityId");

            migrationBuilder.RenameIndex(
                name: "IX_Builds_DownstreamPipelineId",
                table: "Builds",
                newName: "IX_Builds_PipelineEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Builds_Pipelines_PipelineEntityId",
                table: "Builds",
                column: "PipelineEntityId",
                principalTable: "Pipelines",
                principalColumn: "Id");
        }
    }
}
