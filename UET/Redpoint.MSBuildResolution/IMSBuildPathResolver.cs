namespace Redpoint.MSBuildResolution
{
    public interface IMSBuildPathResolver
    {
        Task<(string path, string[] preargs)> ResolveMSBuildPath();
    }
}