namespace Redpoint.MSBuildResolution
{
    using Microsoft.Win32;

    public interface IMSBuildPathResolver
    {
        Task<(string path, string[] preargs)> ResolveMSBuildPath();
    }
}