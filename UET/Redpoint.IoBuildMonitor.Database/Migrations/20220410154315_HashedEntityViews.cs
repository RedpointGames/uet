using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class HashedEntityViews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW ""HashedBuilds""
AS
SELECT 
	encode(sha256(CONCAT(
		""Builds"".""Stage"",
		'|',
		""Builds"".""Name"",
		'|',
		""Project"".""PathWithNamespace"",
		'|',
		""ParentBuild"".""Stage"",
		'|',
		""ParentBuild"".""Name"",
		'|',
		""ParentProject"".""PathWithNamespace"",
		'|',
		""GrandparentBuild"".""Stage"",
		'|',
		""GrandparentBuild"".""Name"",
		'|',
		""GrandparentProject"".""PathWithNamespace""
	)::BYTEA), 'hex') AS ""Hash"",
	""Builds"".*
FROM ""Builds""
	
LEFT JOIN ""Pipelines"" AS ""Pipeline""
	ON ""Pipeline"".""Id"" = ""Builds"".""PipelineId""
LEFT JOIN ""Projects"" AS ""Project""
	ON ""Project"".""Id"" = ""Pipeline"".""ProjectId""
	
LEFT JOIN ""Builds"" AS ""ParentBuild""
	ON ""ParentBuild"".""DownstreamPipelineId"" = ""Pipeline"".""Id""
LEFT JOIN ""Pipelines"" AS ""ParentPipeline""
	ON ""ParentPipeline"".""Id"" = ""ParentBuild"".""PipelineId""
LEFT JOIN ""Projects"" AS ""ParentProject""
	ON ""ParentProject"".""Id"" = ""ParentPipeline"".""ProjectId""
	
LEFT JOIN ""Builds"" AS ""GrandparentBuild""
	ON ""GrandparentBuild"".""DownstreamPipelineId"" = ""ParentPipeline"".""Id""
LEFT JOIN ""Pipelines"" AS ""GrandparentPipeline""
	ON ""GrandparentPipeline"".""Id"" = ""GrandparentBuild"".""PipelineId""
LEFT JOIN ""Projects"" AS ""GrandparentProject""
	ON ""GrandparentProject"".""Id"" = ""GrandparentPipeline"".""ProjectId""
;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW ""HashedPipelines""
AS
SELECT 
	encode(sha256(CONCAT(
		""Project"".""PathWithNamespace"",
		'|',
		""ParentBuild"".""Stage"",
		'|',
		""ParentBuild"".""Name"",
		'|',
		""ParentProject"".""PathWithNamespace"",
		'|',
		""GrandparentBuild"".""Stage"",
		'|',
		""GrandparentBuild"".""Name"",
		'|',
		""GrandparentProject"".""PathWithNamespace""
	)::BYTEA), 'hex') AS ""Hash"",
	""Pipelines"".*
FROM ""Pipelines""
	
LEFT JOIN ""Projects"" AS ""Project""
	ON ""Project"".""Id"" = ""Pipelines"".""ProjectId""
	
LEFT JOIN ""Builds"" AS ""ParentBuild""
	ON ""ParentBuild"".""DownstreamPipelineId"" = ""Pipelines"".""Id""
LEFT JOIN ""Pipelines"" AS ""ParentPipeline""
	ON ""ParentPipeline"".""Id"" = ""ParentBuild"".""PipelineId""
LEFT JOIN ""Projects"" AS ""ParentProject""
	ON ""ParentProject"".""Id"" = ""ParentPipeline"".""ProjectId""
	
LEFT JOIN ""Builds"" AS ""GrandparentBuild""
	ON ""GrandparentBuild"".""DownstreamPipelineId"" = ""ParentPipeline"".""Id""
LEFT JOIN ""Pipelines"" AS ""GrandparentPipeline""
	ON ""GrandparentPipeline"".""Id"" = ""GrandparentBuild"".""PipelineId""
LEFT JOIN ""Projects"" AS ""GrandparentProject""
	ON ""GrandparentProject"".""Id"" = ""GrandparentPipeline"".""ProjectId""
;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(@"
DROP VIEW IF EXISTS ""HashedBuilds"";
DROP VIEW IF EXISTS ""HashedPipelines"";
");
        }
    }
}
