namespace UET.Commands.Build
{
    using Redpoint.UET.Configuration.Engine;
    using System;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal class BuildEngine : IBuild<BuildConfigEngine>
    {
        public Task<int> ExecuteAsync(InvocationContext context, BuildConfigEngine config)
        {
            throw new NotImplementedException();
        }
    }
}
