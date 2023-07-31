namespace Redpoint.OpenGE.PreprocessorCache
{
    using PreprocessorCacheApi;
    using System.Threading.Tasks;

    public interface ICachingPreprocessorScanner : IDisposable
    {
        Task<PreprocessorScanResultWithCacheMetadata> ParseIncludes(
            string filePath,
            CancellationToken cancellationToken);
    }
}
