namespace UET.Commands.Internal.RemoveStalePrecompiledHeaders
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Database;
    using Redpoint.Uet.Database.Models;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal sealed class RemoveStalePrecompiledHeadersCommand
    {
        public sealed class Options
        {
            public Option<DirectoryInfo> EnginePath = new Option<DirectoryInfo>("--engine-path") { IsRequired = true };
            public Option<DirectoryInfo> ProjectPath = new Option<DirectoryInfo>("--project-path") { IsRequired = true };
            public Option<string> TargetName = new Option<string>("--target-name") { IsRequired = true };
            public Option<string> TargetPlatform = new Option<string>("--target-platform") { IsRequired = true };
            public Option<string> TargetConfiguration = new Option<string>("--target-configuration") { IsRequired = true };
        }

        public static Command CreateRemoveStalePrecompiledHeadersCommand()
        {
            var options = new Options();
            var command = new Command("remove-stale-precompiled-headers");
            command.AddAllOptions(options);
            command.AddCommonHandler<RemoveStalePrecompiledHeadersCommandInstance>(options);
            return command;
        }

        private sealed class RemoveStalePrecompiledHeadersCommandInstance : ICommandInstance
        {
            private readonly ILogger<RemoveStalePrecompiledHeadersCommandInstance> _logger;
            private readonly IUetDbConnectionFactory _uetDbConnectionFactory;
            private readonly Options _options;

            public RemoveStalePrecompiledHeadersCommandInstance(
                ILogger<RemoveStalePrecompiledHeadersCommandInstance> logger,
                IUetDbConnectionFactory uetDbConnectionFactory,
                Options options)
            {
                _logger = logger;
                _uetDbConnectionFactory = uetDbConnectionFactory;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var enginePath = context.ParseResult.GetValueForOption(_options.EnginePath)!;
                var projectPath = context.ParseResult.GetValueForOption(_options.ProjectPath)!;
                var targetName = context.ParseResult.GetValueForOption(_options.TargetName)!;
                var targetPlatform = context.ParseResult.GetValueForOption(_options.TargetPlatform)!;
                var targetConfiguration = context.ParseResult.GetValueForOption(_options.TargetConfiguration)!;

                // Compute the intermediate directory path.
                var platformIntermediate = Path.Combine(
                    projectPath.FullName,
                    "Intermediate",
                    "Build",
                    targetPlatform);
                if (!Directory.Exists(platformIntermediate))
                {
                    _logger.LogTrace($"Intermediate folder doesn't exist, so not scanning for stale PCH files: {platformIntermediate}");
                    return 0;
                }

                // We have to check both the generic directory structures, and for possible
                // per-architecture directories.
                var candidates = new List<string>
                {
                    Path.Combine(platformIntermediate, targetName, targetConfiguration),
                };
                foreach (var subdir in Directory.GetDirectories(platformIntermediate))
                {
                    candidates.Add(Path.Combine(subdir, targetName, targetConfiguration));
                }
                _logger.LogTrace($"{candidates.Count} potentially non-existent candidate paths to consider for PCH staleness check.");

                // Connect to our database for storing the last engine path.
                await using var connection = await _uetDbConnectionFactory
                    .ConnectToDefaultDatabaseAsync(context.GetCancellationToken())
                    .ConfigureAwait(false);

                // Iterate through all of the candidates, and search for PCH files
                // in the ones that exist.
                foreach (var candidate in candidates)
                {
                    if (!Directory.Exists(candidate))
                    {
                        _logger.LogTrace($"Candidate folder doesn't exist, so not scanning for stale PCH files underneath it: {candidate}");
                        continue;
                    }

                    string previousEnginePath =
                        (await connection.FindAsync<LastEnginePathModel>(
                            candidate.ToLowerInvariant(),
                            context.GetCancellationToken()).ConfigureAwait(false))?.LastEnginePath ?? string.Empty;
                    if (!string.Equals(enginePath.FullName, previousEnginePath, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogTrace($"Engine version has changed for {targetName} {targetConfiguration}, removing PCH files underneath: {candidate}");
                        var didLogEnginePathChange = false;

                        // The engine has changed. Recursively scan for .pch files and
                        // remove them.
                        foreach (var pch in Directory.EnumerateFiles(candidate, "*.pch", new EnumerationOptions { RecurseSubdirectories = true }))
                        {
                            if (!didLogEnginePathChange)
                            {
                                _logger.LogInformation($"For candidate folder '{candidate}', engine path '{enginePath.FullName}' is not the same as previous engine path '{previousEnginePath}'.");
                                didLogEnginePathChange = true;
                            }

                            _logger.LogInformation($"Removing stale .pch file due to engine path change: {pch}");
                            File.Delete(pch);
                        }
                    }
                    else
                    {
                        _logger.LogTrace($"Engine version up-to-date for {targetName} {targetConfiguration}, not removing PCH files underneath: {candidate}");
                    }

                    await connection.UpsertAsync(
                        new LastEnginePathModel
                        {
                            Key = candidate.ToLowerInvariant(),
                            LastEnginePath = enginePath.FullName,
                        },
                        context.GetCancellationToken()).ConfigureAwait(false);
                }

                return 0;
            }
        }
    }
}
