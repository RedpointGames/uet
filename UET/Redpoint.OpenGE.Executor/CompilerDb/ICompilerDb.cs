namespace Redpoint.OpenGE.Executor.CompilerDb
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    internal interface ICompilerDb
    {
        Task<IEnumerable<string>> ProcessRootFileAsync(
            string filePath,
            IReadOnlyList<DirectoryInfo> includeDirectories,
            IReadOnlyList<DirectoryInfo> systemIncludeDirectories,
            IReadOnlyDictionary<string, string> globalDefinitions,
            CancellationToken cancellationToken);
    }
}
