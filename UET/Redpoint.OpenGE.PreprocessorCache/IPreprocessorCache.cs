namespace Redpoint.OpenGE.PreprocessorCache
{
    using System.Threading.Tasks;

    public interface IPreprocessorCache
    {
        Task EnsureConnectedAsync();

        Task<PreprocessorScanResultWithCacheInfo> GetUnresolvedDependenciesAsync(
            string filePath,
            CancellationToken cancellationToken);
    }
}
