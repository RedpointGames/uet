namespace Redpoint.CloudFramework.Tests
{
    using Quartz;
    using Redpoint.CloudFramework.Processor;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class ProcessorTests
    {
        public class TestContinuousProcessor : IContinuousProcessor
        {
            public static string RoleName => "test-continuous";

            public Task ExecuteAsync(CancellationToken shutdownCancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        public class TestScheduledProcessor : IScheduledProcessor
        {
            public static string RoleName => "test-scheduled";

            public static void ConfigureSchedule(TriggerBuilder trigger)
            {
                trigger
                    .StartNow()
                    .WithCalendarIntervalSchedule(
                        schedule => schedule.WithIntervalInHours(1));
            }

            public Task ExecuteAsync(IJobExecutionContext context)
            {
                return Task.CompletedTask;
            }
        }

        [Fact]
        public void TestProcessorRegistration()
        {
            CloudFramework
                .ServiceApp
                .AddProcessor<TestContinuousProcessor>()
                .AddProcessor<TestScheduledProcessor>();
        }
    }
}
