namespace UET.Commands.Internal.ReparentAdditionalPropertiesInTargets
{
    using Microsoft.Extensions.Logging;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal sealed class ReparentAdditionalPropertiesInTargetsCommand
    {
        public sealed class Options
        {
            public Option<DirectoryInfo> ProjectDirectoryPath = new Option<DirectoryInfo>("--project-directory-path") { IsRequired = true };
        }

        public static Command CreateReparentAdditionalPropertiesInTargetsCommand()
        {
            var options = new Options();
            var command = new Command("reparent-additional-properties-in-targets");
            command.AddAllOptions(options);
            command.AddCommonHandler<ReparentAdditionalPropertiesInTargetsCommandInstance>(options);
            return command;
        }

        private sealed class ReparentAdditionalPropertiesInTargetsCommandInstance : ICommandInstance
        {
            private readonly ILogger<ReparentAdditionalPropertiesInTargetsCommandInstance> _logger;
            private readonly Options _options;

            public ReparentAdditionalPropertiesInTargetsCommandInstance(
                ILogger<ReparentAdditionalPropertiesInTargetsCommandInstance> logger,
                Options options)
            {
                _logger = logger;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var projectDirectoryPath = context.ParseResult.GetValueForOption(_options.ProjectDirectoryPath)!;

                foreach (var targetFile in Directory.GetFiles(
                    Path.Combine(projectDirectoryPath.FullName, "Binaries"),
                    "*.target", 
                    new EnumerationOptions { RecurseSubdirectories = true }))
                {
                    _logger.LogInformation($"Discovered .target file: {targetFile}");

                    // Deserialize just enough that we can read the RedpointUETOriginalProjectDirectory property.
                    var targetText = await File.ReadAllTextAsync(targetFile).ConfigureAwait(false);
                    var json = JsonSerializer.Deserialize(targetText, MinimalUnrealTargetFileJsonSerializerContext.Default.MinimalUnrealTargetFile);
                    var originalProjectDirectoryProperty = (json?.AdditionalProperties ?? new List<MinimalUnrealTargetFileAdditionalProperty>())
                        .FirstOrDefault(x => x.Name == "RedpointUETOriginalProjectDirectory");

                    // If we have it, just do a really simple find-and-replace on the file. We don't serialize
                    // via JSON since then UET would need to be updated whenever Unreal changes the schema
                    // of target files.
                    if (originalProjectDirectoryProperty?.Value != null)
                    {
                        var originalValue = originalProjectDirectoryProperty.Value.Replace("\\", "\\\\", StringComparison.OrdinalIgnoreCase).TrimEnd('\\');
                        var replacedValue = projectDirectoryPath.FullName.Replace("\\", "\\\\", StringComparison.OrdinalIgnoreCase).TrimEnd('\\');

                        _logger.LogInformation($"Reparenting absolute paths in .target file: {targetFile} (Replacing '{originalValue}' with '{replacedValue}')");
                        targetText = targetText.Replace(originalValue, replacedValue, StringComparison.OrdinalIgnoreCase);
                        await File.WriteAllTextAsync(targetFile, targetText).ConfigureAwait(false);
                    }
                }

                return 0;
            }
        }
    }
}
