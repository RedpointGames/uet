namespace UET.Commands.Internal.CopyAndMutateBuildCs
{
    using Microsoft.Extensions.Logging;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal sealed class CopyAndMutateBuildCsCommand
    {
        internal sealed class Options
        {
            public Option<string> InputBasePath;
            public Option<string> InputFileList;
            public Option<string> OutputPath;
            public Option<bool> Marketplace;

            public Options()
            {
                InputBasePath = new Option<string>("--input-base-path");
                InputFileList = new Option<string>("--input-file-list");
                OutputPath = new Option<string>("--output-path");
                Marketplace = new Option<bool>("--marketplace");
            }
        }

        public static Command CreateCopyAndMutateBuildCsCommand()
        {
            var options = new Options();
            var command = new Command("copy-and-mutate-build-cs");
            command.AddAllOptions(options);
            command.AddCommonHandler<CopyAndMutateBuildCsCommandInstance>(options);
            return command;
        }

        private sealed class CopyAndMutateBuildCsCommandInstance : ICommandInstance
        {
            private readonly ILogger<CopyAndMutateBuildCsCommandInstance> _logger;
            private readonly Options _options;

            public CopyAndMutateBuildCsCommandInstance(
                ILogger<CopyAndMutateBuildCsCommandInstance> logger,
                Options options)
            {
                _logger = logger;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var inputBasePath = context.ParseResult.GetValueForOption(_options.InputBasePath)!.Replace('/', Path.DirectorySeparatorChar);
                var inputFileList = context.ParseResult.GetValueForOption(_options.InputFileList)!;
                var outputPath = context.ParseResult.GetValueForOption(_options.OutputPath)!.Replace('/', Path.DirectorySeparatorChar);
                var marketplace = context.ParseResult.GetValueForOption(_options.Marketplace)!;

                var inputFiles = await File.ReadAllLinesAsync(inputFileList).ConfigureAwait(false);
                var fileMappings = new List<(string input, string output)>();
                foreach (var inputFile in inputFiles.Select(x => x.Replace('/', Path.DirectorySeparatorChar)))
                {
                    if (!inputFile.StartsWith(inputBasePath, StringComparison.Ordinal))
                    {
                        _logger.LogError("Invalid input file list!");
                        return 1;
                    }
                    var relativeInputFile = inputFile[inputBasePath.Length..];
                    fileMappings.Add((inputFile, outputPath + Path.DirectorySeparatorChar + relativeInputFile));
                }

                if (marketplace)
                {
                    // Leave files as-is, since the Marketplace needs to be able to build the plugin.
                    foreach (var mapping in fileMappings)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(mapping.output)!);
                        File.Copy(mapping.input, mapping.output, true);
                        _logger.LogInformation($"Copied: {mapping.input} -> {mapping.output}");
                    }
                }
                else
                {
                    // Mutate each file as we copy it, so that recipients see the plugin as precompiled.
                    var replaceRegex = new Regex("(?ms)^\\s+/\\* PRECOMPILED REMOVE BEGIN \\*/.*?/\\* PRECOMPILED REMOVE END \\*/");
                    foreach (var mapping in fileMappings)
                    {
                        var buildCs = await File.ReadAllTextAsync(mapping.input).ConfigureAwait(false);
                        buildCs = buildCs.Replace("bUsePrecompiled = false;", "bUsePrecompiled = true;", StringComparison.Ordinal);
                        while (buildCs.Contains("/* PRECOMPILED REMOVE BEGIN */", StringComparison.Ordinal))
                        {
                            if (!buildCs.Contains("/* PRECOMPILED REMOVE END */", StringComparison.Ordinal))
                            {
                                _logger.LogError($"Missing PRECOMPILED REMOVE END after PRECOMPILED REMOVE BEGIN in: {mapping.input}");
                                return 1;
                            }
                            var newBuildCs = replaceRegex.Replace(buildCs, "");
                            if (newBuildCs.Trim() == buildCs.Trim())
                            {
                                _logger.LogError($"Did not make progress filtering out PRECOMPILED REMOVE sections in: {mapping.input}");
                                return 1;
                            }
                            buildCs = newBuildCs;
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(mapping.output)!);
                        await File.WriteAllTextAsync(mapping.output, buildCs).ConfigureAwait(false);
                        _logger.LogInformation($"Mutated: {mapping.input} -> {mapping.output}");
                    }
                }

                return 0;
            }
        }
    }
}
