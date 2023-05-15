namespace UET.Commands.Build
{
    using Redpoint.UET.Configuration;
    using System;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal class BuildPlugin : IBuild<BuildConfig>
    {
        public Task<int> ExecuteAsync(InvocationContext context, BuildConfig config)
        {
            throw new NotImplementedException();
        }
    }
}
