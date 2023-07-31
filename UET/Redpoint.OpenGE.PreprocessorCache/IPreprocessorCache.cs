namespace Redpoint.OpenGE.PreprocessorCache
{
    using PreprocessorCacheApi;
    using System.Threading.Tasks;

    public interface IPreprocessorCache
    {
        Task EnsureConnectedAsync();

        Task<PreprocessorScanResultWithCacheMetadata> GetUnresolvedDependenciesAsync(
            string filePath,
            CancellationToken cancellationToken);
    }
}
