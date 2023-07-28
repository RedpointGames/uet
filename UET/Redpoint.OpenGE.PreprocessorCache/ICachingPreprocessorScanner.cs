namespace Redpoint.OpenGE.PreprocessorCache
{
    using System.Threading.Tasks;

    public interface ICachingPreprocessorScanner : IDisposable
    {
        Task<PreprocessorScanResultWithCacheInfo> ParseIncludes(
            string filePath,
            CancellationToken cancellationToken);
    }
}
