namespace UET.Commands.New.Plugin
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal sealed class NewPluginCommand
    {
        internal sealed class Options
        {
            public Argument<string> Name;
            public Option<DirectoryInfo> Path;
            public Option<bool> Force;

            public Options()
            {
                Name = new Argument<string>(
                    "name",
                    () => "UntitledPlugin",
                    description: "The name of the new plugin. This must not contain spaces and must not start with a number.");

                Path = new Option<DirectoryInfo>(
                    "--path",
                    description: "The path to create the new plugin in. If not set, defaults to the current directory.",
                    getDefaultValue: () => new DirectoryInfo(Environment.CurrentDirectory));
                Path.AddAlias("-p");

                Force = new Option<bool>(
                    "--force",
                    description: "Create the plugin even if files already exist in the target directory.");
                Path.AddAlias("-f");
            }
        }

        public static Command CreateNewPluginCommand()
        {
            var options = new Options();
            var command = new Command("plugin", "Create a new Unreal Engine plugin.");
            command.AddAllOptions(options);
            command.AddCommonHandler<NewPluginCommandInstance>(options);
            return command;
        }

        private sealed class NewPluginCommandInstance : ICommandInstance
        {
            private readonly Options _options;
            private readonly ILogger<NewPluginCommandInstance> _logger;

            public NewPluginCommandInstance(
                Options options,
                ILogger<NewPluginCommandInstance> logger)
            {
                _options = options;
                _logger = logger;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var name = context.ParseResult.GetValueForArgument(_options.Name) ?? "UntitledPlugin";
                var path = context.ParseResult.GetValueForOption(_options.Path) ?? new DirectoryInfo(Environment.CurrentDirectory);
                var force = context.ParseResult.GetValueForOption(_options.Force);

                var regex = new Regex("^[A-Za-z][A-Za-z_0-9]*$");
                if (!regex.IsMatch(name))
                {
                    _logger.LogError($"The provided plugin name '{name}' does not match the regex '$[A-Za-z][A-Za-z_0-9]*^'.");
                    return 1;
                }

                if (!force)
                {
                    if (File.Exists(Path.Combine(path.FullName, $"{name}.uplugin")) ||
                        File.Exists(Path.Combine(path.FullName, $"BuildConfig.json")))
                    {
                        _logger.LogError("Another plugin or project already exists in the current directory; refusing to run without --force.");
                        return 1;
                    }
                }

                _logger.LogInformation($"Creating new plugin '{name}' in '{path.FullName}'...");

                var currentVersionAttributeValue = RedpointSelfVersion.GetInformationalVersion();
                var targetVersion = currentVersionAttributeValue != null && !currentVersionAttributeValue.EndsWith("-pre", StringComparison.Ordinal)
                    ? currentVersionAttributeValue
                    : "BleedingEdge";

                await File.WriteAllTextAsync(
                    Path.Combine(path.FullName, $"{name}.uplugin"),
                    $$"""
                    {
                        "FileVersion": 3,
                        "Version": 10000,
                        "VersionName": "Unversioned",
                        "FriendlyName": "{{name}}",
                        "Description": "",
                        "Category": "Networking",
                        "CreatedBy": "",
                        "CreatedByURL": "",
                        "DocsURL": "",
                        "MarketplaceURL": "com.epicgames.launcher://ue/marketplace/product/",
                        "SupportURL": "",
                        "CanContainContent": false,
                        "IsBetaVersion": false,
                        "IsExperimentalVersion": false,
                        "Installed": false,
                        "Modules": [
                    	    {
                    		    "Name": "{{name}}",
                    		    "Type": "Runtime",
                    		    "LoadingPhase": "Default",
                    		    "PlatformAllowList": [ "Win64", "Mac", "IOS", "Android", "Linux", "LinuxArm64" ]
                    	    }
                        ]
                    }
                    """);
                await File.WriteAllTextAsync(
                    Path.Combine(path.FullName, $"BuildConfig.json"),
                    $$"""
                    {
                        "$schema": "https://raw.githubusercontent.com/RedpointGames/uet-schema/main/root.json",
                        "UETVersion": "{{targetVersion}}",
                        "Type": "Plugin",
                        "PluginName": "{{name}}",
                        "Copyright": {
                            "Header": "Copyright (Your Name) %Y. All Rights Reserved."
                        },
                        "Distributions": [
                            {
                                "Name": "Fab",
                                "Build": {
                                    "StripDebugSymbols": true
                                },
                                "Package": {
                                    "Type": "Fab",
                                    "OutputFolderName": "P-M"
                                }
                            }
                        ]
                    }
                    """);
                Directory.CreateDirectory(Path.Combine(path.FullName, "Source", name));
                Directory.CreateDirectory(Path.Combine(path.FullName, "Source", name, "Public", name));
                Directory.CreateDirectory(Path.Combine(path.FullName, "Source", name, "Private", name));
                await File.WriteAllTextAsync(
                    Path.Combine(path.FullName, $"Source", name, $"{name}.Build.cs"),
                    $$"""
                    using UnrealBuildTool;

                    public class {{name}} : ModuleRules
                    {
                        public {{name}}(ReadOnlyTargetRules Target) : base(Target)
                        {
                            PublicDependencyModuleNames.AddRange(new[]
                            {
                                "Core",
                            });

                            PrivateDependencyModuleNames.AddRange(new[]
                            {
                                "CoreUObject",
                                "CoreOnline",
                                "Engine",
                            });
                        }
                    }
                    """);
                await File.WriteAllTextAsync(
                    Path.Combine(path.FullName, $"Source", name, "Public", name, $"Module.h"),
                    $$"""
                    #pragma once

                    #include "Modules/ModuleManager.h"
                    """);
                await File.WriteAllTextAsync(
                    Path.Combine(path.FullName, $"Source", name, "Private", name, $"Module.cpp"),
                    $$"""
                    #include "{{name}}/Module.h"

                    IMPLEMENT_MODULE(FDefaultModuleImpl, {{name}})
                    """);

                _logger.LogInformation($"Plugin created successfully.");
                return 0;
            }
        }
    }
}
