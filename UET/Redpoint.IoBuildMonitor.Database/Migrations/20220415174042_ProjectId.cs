using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class ProjectId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_WebhookEventEntity_Projects_ProjectId",
                table: "WebhookEventEntity");

            migrationBuilder.DropIndex(
                name: "IX_WebhookEventEntity_ProjectId",
                table: "WebhookEventEntity");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEventEntity_ProjectId",
                table: "WebhookEventEntity",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_WebhookEventEntity_Projects_ProjectId",
                table: "WebhookEventEntity",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");
        }
    }
}
