namespace UET.Commands.Internal.UpdateUPlugin
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Configuration.Plugin;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;

    internal sealed class UpdateUPluginCommand
    {
        internal sealed class Options
        {
            public Option<string> InputPath;
            public Option<string> OutputPath;
            public Option<string> EngineVersion;
            public Option<string> VersionName;
            public Option<string> VersionNumber;
            public Option<BuildConfigPluginPackageType> PackageType;

            public Options()
            {
                InputPath = new Option<string>("--input-path");
                OutputPath = new Option<string>("--output-path");
                EngineVersion = new Option<string>("--engine-version");
                VersionName = new Option<string>("--version-name");
                VersionNumber = new Option<string>("--version-number");
                PackageType = new Option<BuildConfigPluginPackageType>("--package-type");
            }
        }

        public static Command CreateUpdateUPluginCommand()
        {
            var options = new Options();
            var command = new Command("update-uplugin");
            command.AddAllOptions(options);
            command.AddCommonHandler<UpdateUPluginCommandInstance>(options);
            return command;
        }

        private sealed class UpdateUPluginCommandInstance : ICommandInstance
        {
            private readonly ILogger<UpdateUPluginCommandInstance> _logger;
            private readonly Options _options;

            public UpdateUPluginCommandInstance(
                ILogger<UpdateUPluginCommandInstance> logger,
                Options options)
            {
                _logger = logger;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var inputPath = context.ParseResult.GetValueForOption(_options.InputPath)!;
                var outputPath = context.ParseResult.GetValueForOption(_options.OutputPath)!;
                var engineVersion = context.ParseResult.GetValueForOption(_options.EngineVersion)!;
                var versionName = context.ParseResult.GetValueForOption(_options.VersionName)!;
                var versionNumber = context.ParseResult.GetValueForOption(_options.VersionNumber)!;
                var packageType = context.ParseResult.GetValueForOption(_options.PackageType)!;

                JsonNode? node;
                using (var stream = new StreamReader(new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    node = JsonNode.Parse(await stream.ReadToEndAsync().ConfigureAwait(false));
                }

                if (node == null)
                {
                    _logger.LogError("The .uplugin file used as input is not valid.");
                    return 1;
                }


                //if (packageType != BuildConfigPluginPackageType.Fab)
                {
                    node["EngineVersion"] = engineVersion;
                }
                /*else
                {
                    var obj = node.AsObject();
                    if (obj.ContainsKey("EngineVersion"))
                    {
                        obj.Remove("EngineVersion");
                    }
                }*/
                node["VersionName"] = versionName;
                node["Version"] = ulong.Parse(versionNumber, CultureInfo.InvariantCulture);
                node["Installed"] = true;

                if (packageType == BuildConfigPluginPackageType.None ||
                    packageType == BuildConfigPluginPackageType.Generic)
                {
                    node["EnabledByDefault"] = false;
                }
                else
                {
                    var obj = node.AsObject();
                    obj.Remove("EnabledByDefault");

                    foreach (var module in node["Modules"]?.AsArray() ?? new JsonArray())
                    {
                        if (module == null)
                        {
                            continue;
                        }

                        if (module["WhitelistPlatforms"] != null)
                        {
                            _logger.LogWarning("Plugin uses deprecated 'WhitelistPlatforms' setting for a module. Use 'PlatformAllowList' instead.");
                        }
                        if (module["BlacklistPlatforms"] != null)
                        {
                            _logger.LogWarning("Plugin uses deprecated 'BlacklistPlatforms' setting for a module. Use 'PlatformDenyList' instead.");
                        }

                        if (module["WhitelistPlatforms"] == null &&
                            module["BlacklistPlatforms"] == null &&
                            module["PlatformAllowList"] == null &&
                            module["PlatformDenyList"] == null)
                        {
                            module["PlatformDenyList"] = new JsonArray();
                        }
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                using (var stream = new StreamWriter(new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None)))
                {
                    await stream.WriteAsync(node.ToJsonString(new JsonSerializerOptions
                    {
                        WriteIndented = true
                    })).ConfigureAwait(false);
                }

                return 0;
            }
        }
    }
}
