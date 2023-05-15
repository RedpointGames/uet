namespace UET.Commands.Build
{
    using Redpoint.UET.Configuration;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal interface IBuild<T> where T : BuildConfig
    {
        Task<int> ExecuteAsync(InvocationContext context, T config);
    }
}
