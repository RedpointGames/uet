namespace Redpoint.OpenGE.Component.PreprocessorCache.DirectiveScanner
{
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    internal interface ICachingPreprocessorScanner : IDisposable
    {
        PreprocessorScanResultWithCacheMetadata ParseIncludes(string filePath);
    }
}
