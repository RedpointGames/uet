namespace Redpoint.MSBuildResolution
{
    using Redpoint.ProcessExecution;

    public interface IMSBuildPathResolver
    {
        Task<(string path, LogicalProcessArgument[] preargs)> ResolveMSBuildPath();
    }
}