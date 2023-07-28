namespace Redpoint.OpenGE.PreprocessorCache
{
    public interface ICachingPreprocessorScannerFactory
    {
        ICachingPreprocessorScanner CreateCachingPreprocessorScanner(string dataDirectory);
    }
}
