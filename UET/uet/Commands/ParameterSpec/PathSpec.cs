namespace UET.Commands.EngineSpec
{
    using System;
    using System.CommandLine.Parsing;

    internal sealed class PathSpec
    {
        private PathSpec()
        {
        }

        public static PathSpec ParseForDefaultValue()
        {
            return new PathSpec
            {
                Type = PathSpecType.BuildConfig,
                DirectoryPath = Environment.CurrentDirectory,
            };
        }

        public static PathSpec ParsePathSpec(ArgumentResult result)
        {
            return ParsePathSpec(result, false);
        }

        public static PathSpec ParsePathSpec(ArgumentResult result, bool ignoreBuildJson)
        {
            var path = result.Tokens.Count == 0
                ? Environment.CurrentDirectory
                : string.Join(" ", result.Tokens);

            var isCurrentDirectory = path == Environment.CurrentDirectory;

            if (Directory.Exists(path))
            {
                var info = new DirectoryInfo(path);

                if (File.Exists(Path.Combine(info.FullName, "BuildConfig.json")) && !ignoreBuildJson)
                {
                    return new PathSpec
                    {
                        Type = PathSpecType.BuildConfig,
                        DirectoryPath = info.FullName,
                    };
                }

                var project = info.GetFiles("*.uproject").ToArray();
                if (project.Length == 1)
                {
                    return new PathSpec
                    {
                        Type = PathSpecType.UProject,
                        DirectoryPath = info.FullName,
                        UProjectPath = project[0].FullName,
                    };
                }
                else if (project.Length > 1)
                {
                    if (isCurrentDirectory)
                    {
                        result.ErrorMessage = $"The current directory contains multiple '.uproject' files. Specify the exact project to build by using --{result.Argument.Name}.";
                    }
                    else
                    {
                        result.ErrorMessage = $"The --{result.Argument.Name} is ambiguous because there are multiple '.uproject' files in the specified directory.";
                    }
                    return null!;
                }

                var plugin = info.GetFiles("*.uplugin").ToArray();
                if (plugin.Length == 1)
                {
                    return new PathSpec
                    {
                        Type = PathSpecType.UPlugin,
                        DirectoryPath = info.FullName,
                        UPluginPath = plugin[0].FullName,
                    };
                }
                else if (plugin.Length > 1)
                {
                    if (isCurrentDirectory)
                    {
                        result.ErrorMessage = $"The current directory contains multiple '.uplugin' files. Specify the exact plugin to build by using --{result.Argument.Name}.";
                    }
                    else
                    {
                        result.ErrorMessage = $"The --{result.Argument.Name} is ambiguous because there are multiple '.uplugin' files in the specified directory.";
                    }
                    return null!;
                }

                var containExpectations = "contain a BuildConfig.json file, a '.uproject' file or a '.uplugin' file";
                if (ignoreBuildJson)
                {
                    containExpectations = "contain a '.uproject' or a '.uplugin' file";
                }

                if (isCurrentDirectory)
                {
                    result.ErrorMessage = $"The current directory does not {containExpectations}.";
                }
                else
                {
                    result.ErrorMessage = $"The --{result.Argument.Name} is invalid because it does not {containExpectations}.";
                }
                return null!;
            }
            else if (File.Exists(path))
            {
                var info = new FileInfo(path);

                if (info.Name.Equals("BuildConfig.json", StringComparison.OrdinalIgnoreCase) && !ignoreBuildJson)
                {
                    return new PathSpec
                    {
                        Type = PathSpecType.BuildConfig,
                        DirectoryPath = info.Directory!.FullName,
                    };
                }

                if (info.Name.EndsWith(".uproject", StringComparison.OrdinalIgnoreCase))
                {
                    return new PathSpec
                    {
                        Type = PathSpecType.UProject,
                        DirectoryPath = info.Directory!.FullName,
                        UProjectPath = info.FullName,
                    };
                }

                if (info.Name.EndsWith(".uplugin", StringComparison.OrdinalIgnoreCase))
                {
                    return new PathSpec
                    {
                        Type = PathSpecType.UPlugin,
                        DirectoryPath = info.Directory!.FullName,
                        UPluginPath = info.FullName,
                    };
                }

                if (ignoreBuildJson)
                {
                    result.ErrorMessage = $"The --{result.Argument.Name} is invalid because it must either be a '.uproject' file, a '.uplugin' file, or a directory that contains one of those files.";
                }
                else
                {
                    result.ErrorMessage = $"The --{result.Argument.Name} is invalid because it must either be a BuildConfig.json file, a '.uproject' file, a '.uplugin' file, or a directory that contains one of those files.";
                }
                return null!;
            }
            else
            {
                result.ErrorMessage = $"The path specified by --{result.Argument.Name} does not exist.";
                return null!;
            }
        }

        public required PathSpecType Type { get; init; }

        public required string DirectoryPath { get; init; }

        public string? UProjectPath { get; private init; }

        public string? UPluginPath { get; private init; }

        public override string ToString()
        {
            switch (Type)
            {
                case PathSpecType.BuildConfig:
                    return $"{DirectoryPath} (BuildConfig.json)";
                case PathSpecType.UProject:
                    return $"{DirectoryPath} ({Path.GetRelativePath(DirectoryPath, UProjectPath!)})";
                case PathSpecType.UPlugin:
                    return $"{DirectoryPath} ({Path.GetRelativePath(DirectoryPath, UPluginPath!)})";
            }
            return "(unknown)";
        }
    }
}
