namespace Redpoint.OpenGE.Executor.CompilerDb.PreprocessorScanner
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal class OnDiskPreprocessorScanner : IPreprocessorScanner
    {
        private readonly ILogger<OnDiskPreprocessorScanner> _logger;

        public OnDiskPreprocessorScanner(
            ILogger<OnDiskPreprocessorScanner> logger)
        {
            _logger = logger;
        }

        public async Task<PreprocessorScanResult> ParseIncludes(
            string filePath,
            CancellationToken cancellationToken)
        {
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            DateTimeOffset ticks = File.GetLastWriteTimeUtc(filePath);
            var includes = new List<string>();
            var systemIncludes = new List<string>();
            var compiledPlatformHeaderIncludes = new List<string>();
            foreach (var line in lines)
            {
                var l = line.TrimStart();
                if (l.StartsWith("#include"))
                {
                    var c = l.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (c.Length >= 2)
                    {
                        var include = c[1];
                        if (include[0] == '"')
                        {
                            includes.Add(include.Trim('"'));
                        }
                        else if (include[0] == '<')
                        {
                            systemIncludes.Add(include.TrimStart('<').TrimEnd('>'));
                        }
                        else if (include.StartsWith("COMPILED_PLATFORM_HEADER("))
                        {
                            compiledPlatformHeaderIncludes.Add(include.Substring("COMPILED_PLATFORM_HEADER(".Length).TrimEnd(')'));
                            /*
                            if (!globalDefinitions.ContainsKey("UBT_COMPILED_PLATFORM"))
                            {
                                Console.WriteLine($"{globalDefinitions.Count} definitions");
                                foreach (var kv in globalDefinitions)
                                {
                                    Console.WriteLine($"{kv.Key}={kv.Value}");
                                }
                            }

                            var platformName = globalDefinitions.ContainsKey("OVERRIDE_PLATFORM_HEADER_NAME")
                                ? globalDefinitions["OVERRIDE_PLATFORM_HEADER_NAME"]
                                : globalDefinitions["UBT_COMPILED_PLATFORM"];
                            var platformHeader = include.Substring("COMPILED_PLATFORM_HEADER(".Length).TrimEnd(')');
                            includes.Add($"{platformName}/{platformName}{platformHeader}");
                            */
                        }
                        else
                        {
                            // @todo: We need to emit these to yet another category, and evaluate other simple defines.
                            _logger.LogWarning($"Unknown #include line: {line}");
                        }
                    }
                }
            }
            return new PreprocessorScanResult
            {
                FileLastWriteTicks = ticks.UtcTicks,
                Includes = includes.ToArray(),
                SystemIncludes = systemIncludes.ToArray(),
                CompiledPlatformHeaderIncludes = compiledPlatformHeaderIncludes.ToArray(),
            };
        }
    }
}
