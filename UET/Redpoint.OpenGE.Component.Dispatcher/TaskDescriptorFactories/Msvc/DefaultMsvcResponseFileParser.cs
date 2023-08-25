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
                else if (line.StartsWith("/D"))
                {
                    var define = line.Substring("/D".Length).Split('=', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    globalDefinitions[define[0]] = define.Length >= 2 ? define[1] : "1";
                }
                else if (line.StartsWith("/I") ||
                    line.StartsWith("/external:I"))
                {
                    var path = new PotentiallyQuotedPath(line.Substring(line.StartsWith("/I") ? "/I ".Length : "/external:I ".Length));
                    path.MakeAbsolutePath(workingDirectory);
                    var info = new DirectoryInfo(path.Path);
                    if (info.Exists)
                    {
                        if (line.StartsWith("/I"))
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
                else if (line.StartsWith("/FI"))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/FI".Length));
                    path.MakeAbsolutePath(workingDirectory);
                    var fi = new FileInfo(path.Path);
                    forceIncludeFiles.Add(fi);
                }
                else if (line.StartsWith("/Yu"))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/Yu".Length));
                    path.MakeAbsolutePath(workingDirectory);
                    pchInputFile = new FileInfo(path.Path);
                }
                else if (line.StartsWith("/Yc"))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/Yc".Length));
                    path.MakeAbsolutePath(workingDirectory);
                    pchInputFile = new FileInfo(path.Path);
                    isCreatingPch = true;
                }
                else if (line.StartsWith("/Fp"))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/Fp".Length));
                    path.MakeAbsolutePath(workingDirectory);
                    pchCacheFile = new FileInfo(path.Path);
                }
                else if (line.StartsWith("/Fo"))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/Fo".Length));
                    path.MakeAbsolutePath(workingDirectory);
                    outputFile = new FileInfo(path.Path);
                }
                else if (line.StartsWith("/sourceDependencies "))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/sourceDependencies ".Length));
                    path.MakeAbsolutePath(workingDirectory);
                    sourceDependencies = new FileInfo(path.Path);
                }
                else if (line.StartsWith("/clang:-MF"))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/clang:-MF".Length));
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
            var preprocessorCache = await _preprocessorCacheAccessor.GetPreprocessorCacheAsync();
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
                        cancellationToken);
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
            };
        }
    }
}
