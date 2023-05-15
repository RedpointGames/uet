namespace BuildRunner.Commands.Build
{
    using BuildRunner.Configuration;
    using System;
    using System.Collections.Generic;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal interface IBuild<T> where T : BuildConfig
    {
        Task<int> ExecuteAsync(InvocationContext context, T config);
    }
}
