using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class TimestampedBuildsView : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW ""TimestampedBuilds""
AS
SELECT 
	""Build"".""Id"" AS ""BuildId"",
	""Build"".""RanWithTags"" AS ""RanWithTags"",
	COALESCE(""Created"".""StatusChangedAt"", '2100-12-01 00:00:00'::TIMESTAMP) AS ""CreatedAt"",
	COALESCE(""Pending"".""StatusChangedAt"", '2100-12-01 00:00:00'::TIMESTAMP) AS ""PendingAt"",
	COALESCE(""Running"".""StatusChangedAt"", '2100-12-01 00:00:00'::TIMESTAMP) AS ""RunningAt"",
	COALESCE(""Finished"".""StatusChangedAt"", '2100-12-01 00:00:00'::TIMESTAMP) AS ""FinishedAt""
FROM ""Builds"" AS ""Build""
INNER JOIN ""BuildStatusChanges"" AS ""Created""
	ON ""Created"".""BuildId"" = ""Build"".""Id"" AND ""Created"".""NewStatus"" = 'created'
INNER JOIN ""BuildStatusChanges"" AS ""Pending""
	ON ""Pending"".""BuildId"" = ""Build"".""Id"" AND ""Pending"".""NewStatus"" = 'pending'
INNER JOIN ""BuildStatusChanges"" AS ""Running""
	ON ""Running"".""BuildId"" = ""Build"".""Id"" AND ""Running"".""NewStatus"" = 'running'
INNER JOIN ""BuildStatusChanges"" AS ""Finished""
	ON ""Finished"".""BuildId"" = ""Build"".""Id""
	AND ""Finished"".""NewStatus"" != 'created'
	AND ""Finished"".""NewStatus"" != 'pending'
	AND ""Finished"".""NewStatus"" != 'running'
	AND ""Finished"".""NewStatus"" IS NOT NULL
WHERE ARRAY_LENGTH(""Build"".""RanWithTags"", 1) > 0 AND ""Build"".""Duration"" > 60
ORDER BY ""Build"".""Id"" ASC
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(@"
DROP VIEW IF EXISTS ""TimestampedBuilds"";
");
        }
    }
}
