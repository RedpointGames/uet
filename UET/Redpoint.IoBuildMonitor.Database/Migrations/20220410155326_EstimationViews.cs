using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class EstimationViews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW ""BuildEstimations""
AS
SELECT
	""HashedBuilds"".""Id"" AS ""BuildId"",
	""HashedBuilds"".""StartedAt"" + ""Estimator"".""EstimatedDuration"" AS ""EstimatedFinishedAt""
FROM ""HashedBuilds""
LEFT JOIN (
	SELECT
		""Tbl"".""Hash"",
		percentile_disc(0.9) WITHIN GROUP (ORDER BY ""Tbl"".""DurationTimespan"") AS ""EstimatedDuration""
	FROM (
		SELECT 
			""Tbl2"".*,
			RANK() OVER (PARTITION BY ""Tbl2"".""Hash"" ORDER BY ""Tbl2"".""FinishedAt"" DESC) AS ""Rank""
		FROM (
			SELECT 
				""HashedBuilds"".""Hash"",
				""HashedBuilds"".""FinishedAt"" AS ""FinishedAt"",
				""HashedBuilds"".""FinishedAt"" - ""HashedBuilds"".""StartedAt"" AS ""DurationTimespan""
			FROM ""HashedBuilds""
			WHERE ""HashedBuilds"".""FinishedAt"" IS NOT NULL AND ""HashedBuilds"".""Status"" = 'success' AND (""HashedBuilds"".""FinishedAt"" - ""HashedBuilds"".""StartedAt"") IS NOT NULL
			ORDER BY ""Hash"", ""FinishedAt"" DESC
		) AS ""Tbl2""
	) AS ""Tbl""
	WHERE ""Tbl"".""Rank"" <= 5
	GROUP BY ""Tbl"".""Hash""
) AS ""Estimator""
ON ""Estimator"".""Hash"" = ""HashedBuilds"".""Hash""
WHERE ""HashedBuilds"".""Status"" != 'success' AND ""HashedBuilds"".""Status"" != 'failed' AND ""HashedBuilds"".""Status"" != 'canceled' AND ""HashedBuilds"".""StartedAt"" IS NOT NULL
");
            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW ""PipelineEstimations""
AS
SELECT
	""HashedPipelines"".""Id"" AS ""PipelineId"",
	""HashedPipelines"".""CreatedAt"" + ""Estimator"".""EstimatedDuration"" AS ""EstimatedFinishedAt""
FROM ""HashedPipelines""
LEFT JOIN (
	SELECT
		""Tbl"".""Hash"",
		percentile_disc(0.9) WITHIN GROUP (ORDER BY ""Tbl"".""DurationTimespan"") AS ""EstimatedDuration""
	FROM (
		SELECT 
			""Tbl2"".*,
			RANK() OVER (PARTITION BY ""Tbl2"".""Hash"" ORDER BY ""Tbl2"".""FinishedAt"" DESC) AS ""Rank""
		FROM (
			SELECT 
				""HashedPipelines"".""Hash"",
				""HashedPipelines"".""FinishedAt"" AS ""FinishedAt"",
				""HashedPipelines"".""FinishedAt"" - ""HashedPipelines"".""CreatedAt"" AS ""DurationTimespan""
			FROM ""HashedPipelines""
			WHERE ""HashedPipelines"".""FinishedAt"" IS NOT NULL AND ""HashedPipelines"".""Status"" = 'success' AND (""HashedPipelines"".""FinishedAt"" - ""HashedPipelines"".""CreatedAt"") IS NOT NULL
			ORDER BY ""Hash"", ""FinishedAt"" DESC
		) AS ""Tbl2""
	) AS ""Tbl""
	WHERE ""Tbl"".""Rank"" <= 5
	GROUP BY ""Tbl"".""Hash""
) AS ""Estimator""
ON ""Estimator"".""Hash"" = ""HashedPipelines"".""Hash""
WHERE ""HashedPipelines"".""Status"" != 'success' AND ""HashedPipelines"".""Status"" != 'failed' AND ""HashedPipelines"".""Status"" != 'canceled' AND ""HashedPipelines"".""CreatedAt"" IS NOT NULL
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(@"
DROP VIEW IF EXISTS ""BuildEstimations"";
DROP VIEW IF EXISTS ""PipelineEstimations"";
");
        }
    }
}
