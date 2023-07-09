namespace Redpoint.Xunit.Parallel
{
    using global::Xunit.Sdk;
    using global::Xunit.Abstractions;
    using System.Reflection;
    using System.Diagnostics;

    public class ParallelXunitTestFramework : XunitTestFramework
    {
        public ParallelXunitTestFramework(IMessageSink messageSink) : base(messageSink)
        {
        }

        protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
        {
            return new ParallelXunitTestFrameworkExecutor(
                assemblyName,
                SourceInformationProvider,
                DiagnosticMessageSink);
        }
    }
}