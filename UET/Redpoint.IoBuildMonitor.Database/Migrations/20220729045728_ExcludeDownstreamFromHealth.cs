using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class ExcludeDownstreamFromHealth : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW ""ProjectHealths""
AS
SELECT 
	""Projects"".""Id"" AS ""ProjectId"",
	""Projects"".""Name"",
	""Projects"".""WebUrl"",
	""Projects"".""DefaultBranch"",
	""Pipelines"".""Id"" AS ""PipelineId"",
	""Pipelines"".""Status"",
	""Pipelines"".""Sha"",
	ARRAY(
		SELECT
			""Status""
		FROM (
			SELECT
				""Status"",
				""Ref"",
				""ProjectId"",
                ""Source"",
				ROW_NUMBER() OVER (PARTITION BY ""Ref"", ""ProjectId"" ORDER BY ""CreatedAt"" DESC) AS ""RN""
			FROM ""Pipelines"" AS ""PipelinesHistory""
			WHERE ""PipelinesHistory"".""Ref"" = ""Projects"".""DefaultBranch"" AND ""PipelinesHistory"".""ProjectId"" = ""Projects"".""Id"" AND ""PipelinesHistory"".""Source"" NOT IN ('pipeline', 'parent_pipeline')
		) AS ""H""
		WHERE ""H"".""RN"" <= 10 AND ""H"".""RN"" > 1
	) AS ""StatusHistory""
FROM ""Projects""
INNER JOIN (
	SELECT
		""Id"",
		""Sha"",
		""Ref"",
		""ProjectId"",
		""Status"",
        ""Source""
	FROM (
		SELECT
			""Id"",
			""Sha"",
			""Ref"",
			""ProjectId"",
			""Status"",
            ""Source"",
			ROW_NUMBER() OVER (PARTITION BY ""Ref"", ""ProjectId"" ORDER BY ""CreatedAt"" DESC) AS ""RN""
		FROM ""Pipelines""
        WHERE ""Pipelines"".""Source"" NOT IN ('pipeline', 'parent_pipeline')
	) AS ""T""
	WHERE ""T"".""RN"" = 1
) AS ""Pipelines""
	ON ""Pipelines"".""Ref"" = ""Projects"".""DefaultBranch"" AND ""Pipelines"".""ProjectId"" = ""Projects"".""Id"" AND ""Pipelines"".""Source"" NOT IN ('pipeline', 'parent_pipeline')
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW ""ProjectHealths""
AS
SELECT 
	""Projects"".""Id"" AS ""ProjectId"",
	""Projects"".""Name"",
	""Projects"".""WebUrl"",
	""Projects"".""DefaultBranch"",
	""Pipelines"".""Id"" AS ""PipelineId"",
	""Pipelines"".""Status"",
	""Pipelines"".""Sha"",
	ARRAY(
		SELECT
			""Status""
		FROM (
			SELECT
				""Status"",
				""Ref"",
				""ProjectId"",
				ROW_NUMBER() OVER (PARTITION BY ""Ref"", ""ProjectId"" ORDER BY ""CreatedAt"" DESC) AS ""RN""
			FROM ""Pipelines"" AS ""PipelinesHistory""
			WHERE ""PipelinesHistory"".""Ref"" = ""Projects"".""DefaultBranch"" AND ""PipelinesHistory"".""ProjectId"" = ""Projects"".""Id""
		) AS ""H""
		WHERE ""H"".""RN"" <= 10 AND ""H"".""RN"" > 1
	) AS ""StatusHistory""
FROM ""Projects""
INNER JOIN (
	SELECT
		""Id"",
		""Sha"",
		""Ref"",
		""ProjectId"",
		""Status""
	FROM (
		SELECT
			""Id"",
			""Sha"",
			""Ref"",
			""ProjectId"",
			""Status"",
			ROW_NUMBER() OVER (PARTITION BY ""Ref"", ""ProjectId"" ORDER BY ""CreatedAt"" DESC) AS ""RN""
		FROM ""Pipelines""
	) AS ""T""
	WHERE ""T"".""RN"" = 1
) AS ""Pipelines""
	ON ""Pipelines"".""Ref"" = ""Projects"".""DefaultBranch"" AND ""Pipelines"".""ProjectId"" = ""Projects"".""Id""
");
        }
    }
}
