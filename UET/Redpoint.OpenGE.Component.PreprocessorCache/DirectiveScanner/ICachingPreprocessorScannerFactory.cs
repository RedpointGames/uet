namespace Redpoint.OpenGE.Component.PreprocessorCache.DirectiveScanner
{
    internal interface ICachingPreprocessorScannerFactory
    {
        ICachingPreprocessorScanner CreateCachingPreprocessorScanner(string dataDirectory);
    }
}
