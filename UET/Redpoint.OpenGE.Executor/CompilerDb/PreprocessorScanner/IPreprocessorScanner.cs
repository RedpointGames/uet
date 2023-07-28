namespace Redpoint.OpenGE.Executor.CompilerDb.PreprocessorScanner
{
    using System.Threading.Tasks;

    internal interface IPreprocessorScanner
    {
        Task<PreprocessorScanResult> ParseIncludes(
            string filePath,
            CancellationToken cancellationToken);
    }
}
