namespace Redpoint.XunitFramework
{
    using System.Reflection;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class RedpointTestFramework : XunitTestFramework
    {
        public RedpointTestFramework(Xunit.Abstractions.IMessageSink messageSink) : base(messageSink)
        {
            messageSink.OnMessage(new DiagnosticMessage("Using Redpoint.Xunit testing framework."));
        }

        protected override ITestFrameworkExecutor CreateExecutor(
            AssemblyName assemblyName)
        {
            return new RedpointTestFrameworkExecutor(
                assemblyName,
                SourceInformationProvider,
                DiagnosticMessageSink);
        }
    }
}