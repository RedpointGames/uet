using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Io.Migrations
{
    public partial class NullableUpdate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<long>(
                name: "LastUpdatedByWebhookEventId",
                table: "Users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<string[]>(
                name: "Tags",
                table: "Runners",
                type: "text[]",
                nullable: false,
                defaultValue: Array.Empty<string>(),
                oldClrType: typeof(string[]),
                oldType: "text[]",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsShared",
                table: "Runners",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "Runners",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<long>(
                name: "LastUpdatedByWebhookEventId",
                table: "Runners",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<long>(
                name: "VisibilityLevel",
                table: "Projects",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "LastUpdatedByWebhookEventId",
                table: "Projects",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<bool>(
                name: "Tag",
                table: "Pipelines",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.Sql("UPDATE \"Pipelines\" SET \"Stages\" = ARRAY[]::text[] WHERE \"Stages\" IS NULL;");

            migrationBuilder.AlterColumn<string[]>(
                name: "Stages",
                table: "Pipelines",
                type: "text[]",
                nullable: false,
                defaultValue: Array.Empty<string>(),
                oldClrType: typeof(string[]),
                oldType: "text[]",
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastUpdatedByWebhookEventId",
                table: "Pipelines",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<long>(
                name: "TargetProjectId",
                table: "MergeRequests",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "SourceProjectId",
                table: "MergeRequests",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "LastUpdatedByWebhookEventId",
                table: "MergeRequests",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "LastUpdatedByWebhookEventId",
                table: "Commits",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<bool>(
                name: "Manual",
                table: "Builds",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "AllowFailure",
                table: "Builds",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<long>(
                name: "LastUpdatedByWebhookEventId",
                table: "Builds",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "WebhookEventEntity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now() at time zone 'utc'"),
                    Data = table.Column<string>(type: "json", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookEventEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookEventEntity_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEventEntity_ProjectId",
                table: "WebhookEventEntity",
                column: "ProjectId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "WebhookEventEntity");

            migrationBuilder.DropColumn(
                name: "LastUpdatedByWebhookEventId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastUpdatedByWebhookEventId",
                table: "Runners");

            migrationBuilder.DropColumn(
                name: "LastUpdatedByWebhookEventId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "LastUpdatedByWebhookEventId",
                table: "Pipelines");

            migrationBuilder.DropColumn(
                name: "LastUpdatedByWebhookEventId",
                table: "MergeRequests");

            migrationBuilder.DropColumn(
                name: "LastUpdatedByWebhookEventId",
                table: "Commits");

            migrationBuilder.DropColumn(
                name: "LastUpdatedByWebhookEventId",
                table: "Builds");

            migrationBuilder.AlterColumn<string[]>(
                name: "Tags",
                table: "Runners",
                type: "text[]",
                nullable: true,
                oldClrType: typeof(string[]),
                oldType: "text[]");

            migrationBuilder.AlterColumn<bool>(
                name: "IsShared",
                table: "Runners",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "Runners",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "VisibilityLevel",
                table: "Projects",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Tag",
                table: "Pipelines",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<string[]>(
                name: "Stages",
                table: "Pipelines",
                type: "text[]",
                nullable: true,
                oldClrType: typeof(string[]),
                oldType: "text[]");

            migrationBuilder.AlterColumn<long>(
                name: "TargetProjectId",
                table: "MergeRequests",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "SourceProjectId",
                table: "MergeRequests",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Manual",
                table: "Builds",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "AllowFailure",
                table: "Builds",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);
        }
    }
}
