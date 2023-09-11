namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories.Msvc
{
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Grpc.Core;
    using System.IO;
    using Redpoint.OpenGE.Component.Dispatcher.PreprocessorCacheAccessor;

    internal class DefaultMsvcResponseFileParser : IMsvcResponseFileParser
    {
        private readonly ILogger<DefaultMsvcResponseFileParser> _logger;
        private readonly IPreprocessorCacheAccessor _preprocessorCacheAccessor;

        public DefaultMsvcResponseFileParser(
            ILogger<DefaultMsvcResponseFileParser> logger,
            IPreprocessorCacheAccessor preprocessorCacheAccessor)
        {
            _logger = logger;
            _preprocessorCacheAccessor = preprocessorCacheAccessor;
        }

        public async Task<MsvcParsedResponseFile?> ParseResponseFileAsync(
            string responseFilePath,
            string workingDirectory,
            bool guaranteedToExecuteLocally,
            long buildStartTicks,
            CompilerArchitype architype,
            CancellationToken cancellationToken)
        {
            // Get the response file path.
            if (!Path.IsPathRooted(responseFilePath))
            {
                responseFilePath = Path.Combine(workingDirectory, responseFilePath);
            }

            // Store the data that we need to figure out how to remote this.
            FileInfo? inputFile = null;
            var includeDirectories = new List<DirectoryInfo>();
            var forceIncludeFiles = new List<FileInfo>();
            var globalDefinitions = new Dictionary<string, string>();
            var isCreatingPch = false;
            FileInfo? pchInputFile = null;
            FileInfo? pchCacheFile = null;
            FileInfo? outputFile = null;
            FileInfo? sourceDependencies = null;
            FileInfo? clangDepfile = null;

            // Read all the lines.
            foreach (var line in File.ReadAllLines(responseFilePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    // Empty line.
                    continue;
                }
                else if (!line.StartsWith('/'))
                {
                    // This is the input file.
                    var path = new PotentiallyQuotedPath(line);
                    path.MakeAbsolutePath(workingDirectory);
                    if (File.Exists(path.Path))
                    {
                        inputFile = new FileInfo(path.Path);
                    }
                }
                else if (line.StartsWith("/D", StringComparison.Ordinal))
                {
                    var define = line["/D".Length..].Split('=', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    globalDefinitions[define[0]] = define.Length >= 2 ? define[1] : "1";
                }
                else if (line.StartsWith("/I", StringComparison.Ordinal) ||
                    line.StartsWith("/external:I", StringComparison.Ordinal))
                {
                    var path = new PotentiallyQuotedPath(line[(line.StartsWith("/I", StringComparison.Ordinal) ? "/I ".Length : "/external:I ".Length)..]);
                    path.MakeAbsolutePath(workingDirectory);
                    var info = new DirectoryInfo(path.Path);
                    if (info.Exists)
                    {
                        if (line.StartsWith("/I", StringComparison.Ordinal))
                        {
                            includeDirectories.Add(info);
                        }
                        else
                        {
                            includeDirectories.Add(info);
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"'{path}' does not exist.");
                    }
                }
                else if (line.StartsWith("/FI", StringComparison.Ordinal))
                {
                    var path = new PotentiallyQuotedPath(line["/FI".Length..]);
                    path.MakeAbsolutePath(workingDirectory);
                    var fi = new FileInfo(path.Path);
                    forceIncludeFiles.Add(fi);
                }
                else if (line.StartsWith("/Yu", StringComparison.Ordinal))
                {
                    var path = new PotentiallyQuotedPath(line["/Yu".Length..]);
                    path.MakeAbsolutePath(workingDirectory);
                    pchInputFile = new FileInfo(path.Path);
                }
                else if (line.StartsWith("/Yc", StringComparison.Ordinal))
                {
                    var path = new PotentiallyQuotedPath(line["/Yc".Length..]);
                    path.MakeAbsolutePath(workingDirectory);
                    pchInputFile = new FileInfo(path.Path);
                    isCreatingPch = true;
                }
                else if (line.StartsWith("/Fp", StringComparison.Ordinal))
                {
                    var path = new PotentiallyQuotedPath(line["/Fp".Length..]);
                    path.MakeAbsolutePath(workingDirectory);
                    pchCacheFile = new FileInfo(path.Path);
                }
                else if (line.StartsWith("/Fo", StringComparison.Ordinal))
                {
                    var path = new PotentiallyQuotedPath(line["/Fo".Length..]);
                    path.MakeAbsolutePath(workingDirectory);
                    outputFile = new FileInfo(path.Path);
                }
                else if (line.StartsWith("/sourceDependencies ", StringComparison.Ordinal))
                {
                    var path = new PotentiallyQuotedPath(line["/sourceDependencies ".Length..]);
                    path.MakeAbsolutePath(workingDirectory);
                    sourceDependencies = new FileInfo(path.Path);
                }
                else if (line.StartsWith("/clang:-MF", StringComparison.Ordinal))
                {
                    var path = new PotentiallyQuotedPath(line["/clang:-MF".Length..]);
                    path.MakeAbsolutePath(workingDirectory);
                    clangDepfile = new FileInfo(path.Path);
                }
            }

            // Check we've got a valid configuration.
            if (inputFile == null || outputFile == null)
            {
                // Delegate to the local executor.
                return null;
            }

            // Determine the dependent header files.
            var preprocessorCache = await _preprocessorCacheAccessor.GetPreprocessorCacheAsync().ConfigureAwait(false);
            PreprocessorResolutionResultWithTimingMetadata dependentFiles;
            if (!guaranteedToExecuteLocally)
            {
                try
                {
                    dependentFiles = await preprocessorCache.GetResolvedDependenciesAsync(
                        inputFile.FullName,
                        forceIncludeFiles.Select(x => x.FullName).ToArray(),
                        includeDirectories.Select(x => x.FullName).ToArray(),
                        globalDefinitions,
                        buildStartTicks,
                        architype,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
                {
                    _logger.LogWarning($"Unable to remote compile this file as the preprocessor cache reported an error while parsing headers: {ex.Status.Detail}");
                    return null;
                }
            }
            else
            {
                dependentFiles = new PreprocessorResolutionResultWithTimingMetadata();
            }

            // Return all of the collected metadata.
            return new MsvcParsedResponseFile
            {
                ResponseFilePath = responseFilePath,
                IsCreatingPch = isCreatingPch,
                PchCacheFile = pchCacheFile,
                DependentFiles = dependentFiles,
                InputFile = inputFile,
                OutputFile = outputFile,
                SourceDependencies = sourceDependencies,
                ClangDepfile = clangDepfile,
                PchOriginalHeaderFile = isCreatingPch ? null : pchInputFile,
            };
        }
    }
}
