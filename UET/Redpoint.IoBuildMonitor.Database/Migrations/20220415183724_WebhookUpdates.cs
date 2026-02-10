using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class WebhookUpdates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropPrimaryKey(
                name: "PK_WebhookEventEntity",
                table: "WebhookEventEntity");

            migrationBuilder.RenameTable(
                name: "WebhookEventEntity",
                newName: "WebhookEvents");

            migrationBuilder.AddColumn<string>(
                name: "ObjectKind",
                table: "WebhookEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReservationTimeout",
                table: "WebhookEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReservedBy",
                table: "WebhookEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_WebhookEvents",
                table: "WebhookEvents",
                column: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropPrimaryKey(
                name: "PK_WebhookEvents",
                table: "WebhookEvents");

            migrationBuilder.DropColumn(
                name: "ObjectKind",
                table: "WebhookEvents");

            migrationBuilder.DropColumn(
                name: "ReservationTimeout",
                table: "WebhookEvents");

            migrationBuilder.DropColumn(
                name: "ReservedBy",
                table: "WebhookEvents");

            migrationBuilder.RenameTable(
                name: "WebhookEvents",
                newName: "WebhookEventEntity");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WebhookEventEntity",
                table: "WebhookEventEntity",
                column: "Id");
        }
    }
}
