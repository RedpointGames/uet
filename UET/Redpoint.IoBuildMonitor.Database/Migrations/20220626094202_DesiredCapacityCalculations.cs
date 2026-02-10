using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Io.Migrations
{
    public partial class DesiredCapacityCalculations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW ""DesiredCapacityCalculations""
AS
SELECT
	""RunnerTag"",
	(1 - ""PercentileOriginal"") AS ""Percentile"",
	PERCENTILE_CONT(""PercentileOriginal"") WITHIN GROUP (ORDER BY ""Pending"" + ""Running"") AS ""DesiredCapacity""
FROM ""UtilizationMinutes""
LEFT JOIN GENERATE_SERIES(0.00, 1.00, 0.025) AS ""PercentileOriginal"" ON 1 = 1
WHERE (""Created"" + ""Pending"" + ""Running"") > 1 AND ""Pending"" >= 1
GROUP BY ""RunnerTag"", ""PercentileOriginal""
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(@"
DROP VIEW IF EXISTS ""DesiredCapacityCalculations"";
");
        }
    }
}
