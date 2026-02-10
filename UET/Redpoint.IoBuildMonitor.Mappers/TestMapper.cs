namespace Io.Mappers
{
    using Io.Database;
    using Io.Database.Entities;
    using Io.Json.Api;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using NodaTime;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class TestMapper : IMapper<TestJsonWithValidatedContext, TestEntity[]>
    {
        private readonly IoDbContext _db;
        private readonly ILogger<TestMapper> _logger;

        public TestMapper(
            IoDbContext db,
            ILogger<TestMapper> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<TestEntity[]?> Map(TestJsonWithValidatedContext? source, MapperContext context)
        {
            if (source == null) { return null; }
            if (source.Tests == null) { return null; }
            if (source.Tests.Length == 0) { return []; }

            var project = await _db.Projects.FindAsync(source.ProjectId);
            if (project == null)
            {
                // If the project doesn't exist, we don't accept this request.
                // That's because any GitLab CI/CD job will have a valid JWT,
                // but we only want to accept requests for projects that we're
                // monitoring.
                return null;
            }

            var didCreate = false;
            var pipeline = await _db.Pipelines.FindAsync(source.PipelineId);
            if (pipeline == null)
            {
                pipeline = new PipelineEntity
                {
                    Id = source.PipelineId,
                    Project = project,
                    LastUpdatedByWebhookEventId = -1, // Always allow webhooks to update this later.
                };
                _logger.LogInformation("(TestMapper) Creating pipeline entity with ID " + source.PipelineId + " because no such pipeline exists in the database.");
                _db.Pipelines.Add(pipeline);
                didCreate = true;
            }

            var build = await _db.Builds.FindAsync(source.BuildId);
            if (build == null)
            {
                build = new BuildEntity
                {
                    Id = source.BuildId,
                    Pipeline = pipeline,
                    LastUpdatedByWebhookEventId = -1, // Always allow webhooks to update this later.
                };
                _db.Builds.Add(build);
            }

            var existingTestIds = source.Tests.Where(x => x != null).Select(x => TestEntity.ComputeLookup(source.BuildId, x.FullName, x.Platform, (x.IsGauntlet ?? false) ? x.GauntletInstance : string.Empty)).ToHashSet();
            var tests = await _db.Tests.Where(x => existingTestIds.Contains(x.LookupId)).ToDictionaryAsync(k => k.LookupId, v => v);
            var validStatus = new HashSet<string?>
            {
                TestStatus.Listed,
                TestStatus.Running,
                TestStatus.Passed,
                TestStatus.Failed,
            };

            var testResults = new List<TestEntity>();

            foreach (var test in source.Tests)
            {
                if (test == null)
                {
                    _logger.LogWarning("Ignoring submitted test because it is null and has no information.");
                    continue;
                }

                _logger.LogInformation($"{test.FullName} = {test.Status}");

                if (string.IsNullOrWhiteSpace(test.FullName))
                {
                    _logger.LogWarning("Ignoring submitted test because it has no full name set.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(test.Platform))
                {
                    _logger.LogWarning("Ignoring submitted test because it has no platform set.");
                    continue;
                }
                if (test.IsGauntlet ?? false && string.IsNullOrWhiteSpace(test.GauntletInstance))
                {
                    _logger.LogWarning("Ignoring submitted test because it is a Gauntlet test with no Gauntlet instance set.");
                    continue;
                }
                if (!validStatus.Contains(test.Status == "created" ? TestStatus.Listed : test.Status))
                {
                    _logger.LogWarning($"Ignoring submitted test because '{test.Status}' is not a valid test status.");
                    continue;
                }

                var testId = TestEntity.ComputeLookup(source.BuildId, test.FullName, test.Platform, (test.IsGauntlet ?? false) ? test.GauntletInstance : string.Empty);
                TestEntity? testEntity;
                if (!tests.TryGetValue(testId, out testEntity))
                {
                    testEntity = new TestEntity
                    {
                        Build = build,
                        BuildId = build.Id,
                        FullName = test.FullName,
                        Platform = test.Platform,
                        GauntletInstance = (test.IsGauntlet ?? false) ? test.GauntletInstance : string.Empty,
                        IsGauntlet = test.IsGauntlet ?? false,
                        DateCreatedUtc = SystemClock.Instance.GetCurrentInstant(),
                    };
                    tests.Add(testId, testEntity);
                    _db.Tests.Add(testEntity);
                }

                if (!(testEntity.IsGauntlet ?? false))
                {
                    if (testEntity.AutomationInstance == null &&
                        !string.IsNullOrWhiteSpace(test.AutomationInstance))
                    {
                        testEntity.AutomationInstance = test.AutomationInstance;
                    }
                }
                testEntity.Status = test.Status == "created" ? TestStatus.Listed : test.Status;
                if (test.DateStartedUtc != null &&
                    testEntity.DateStartedUtc == null)
                {
                    testEntity.DateStartedUtc = Instant.FromUnixTimeMilliseconds(test.DateStartedUtc.Value);
                }
                if ((testEntity.Status == TestStatus.Running || testEntity.Status == TestStatus.Passed || testEntity.Status == TestStatus.Failed) &&
                    testEntity.DateStartedUtc == null)
                {
                    testEntity.DateStartedUtc = SystemClock.Instance.GetCurrentInstant();
                }
                if (test.DateFinishedUtc != null &&
                    testEntity.DateFinishedUtc == null)
                {
                    testEntity.DateFinishedUtc = Instant.FromUnixTimeMilliseconds(test.DateFinishedUtc.Value);
                }
                if (test.DurationSeconds != null &&
                    testEntity.DateStartedUtc != null &&
                    testEntity.DateFinishedUtc == null)
                {
                    testEntity.DateFinishedUtc = testEntity.DateStartedUtc.Value.Plus(Duration.FromSeconds(test.DurationSeconds.Value));
                    testEntity.DurationSeconds = test.DurationSeconds.Value;
                }
                if ((testEntity.Status == TestStatus.Passed || testEntity.Status == TestStatus.Failed) &&
                    testEntity.DateFinishedUtc == null)
                {
                    testEntity.DateFinishedUtc = SystemClock.Instance.GetCurrentInstant();
                }
                if (testEntity.DurationSeconds == null &&
                    testEntity.DateStartedUtc != null &&
                    testEntity.DateFinishedUtc != null)
                {
                    testEntity.DurationSeconds = (testEntity.DateFinishedUtc.Value - testEntity.DateStartedUtc.Value).TotalSeconds;
                }

                if (test.AppendPrimaryLogLines != null)
                {
                    foreach (var primaryLog in test.AppendPrimaryLogLines)
                    {
                        var logEntity = new TestLogEntity
                        {
                            Test = testEntity,
                            Name = TestLogEntity.NamePrimary,
                            Data = primaryLog,
                        };
                        _db.TestLogs.Add(logEntity);
                    }
                }

                if (test.AppendAdditionalLogLines != null)
                {
                    foreach (var kv in test.AppendAdditionalLogLines)
                    {
                        if (kv.Value != null && !string.IsNullOrWhiteSpace(kv.Key))
                        {
                            foreach (var additionalLog in kv.Value)
                            {
                                var logEntity = new TestLogEntity
                                {
                                    Test = testEntity,
                                    Name = TestLogEntity.NamePrimary,
                                    Data = kv.Key,
                                };
                                _db.TestLogs.Add(logEntity);
                            }
                        }
                    }
                }

                testResults.Add(testEntity);
            }

            if (didCreate)
            {
                // No idea if this will fix things.
                await _db.SaveChangesAsync();
            }

            return testResults.ToArray();
        }
    }
}
