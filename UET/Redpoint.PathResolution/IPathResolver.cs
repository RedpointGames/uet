namespace Redpoint.PathResolution
{
    public interface IPathResolver
    {
        Task<string> ResolveBinaryPath(string binaryName);
    }
}