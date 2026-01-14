namespace UET.Commands.Build
{
    using Redpoint.Uet.Commands.ParameterSpec;
    using System;
    using System.CommandLine;

    internal sealed class BuildCommandOptions
    {
        public Option<EngineSpec> Engine;
        public Option<PathSpec> Path;
        public Option<string[]> Test;
        public Option<string[]> Deploy;
        public Option<bool> StrictIncludes;

        public Option<DistributionSpec?> Distribution;

        public Option<bool> Shipping;
        public Option<string[]> Platform;
        public Option<string> PluginPackage;

        public Option<string?> PluginVersionName;
        public Option<long?> PluginVersionNumber;

        public Option<string> ProjectStagingDirectory;

        public Option<string> Executor;
        public Option<string> ExecutorOutputFile;
        public Option<Uri> ExecutorGitUrl;
        public Option<string> ExecutorGitBranch;
        public Option<string?> WindowsSharedStoragePath;
        public Option<string?> WindowsSharedGitCachePath;
        public Option<string?> WindowsSdksPath;
        public Option<string?> MacSharedStoragePath;
        public Option<string?> MacSharedGitCachePath;
        public Option<string?> MacSdksPath;

        public BuildCommandOptions(
            IServiceProvider serviceProvider)
        {
            const string buildConfigOptions = "Options when targeting a BuildConfig.json file:";
            const string uprojectpluginOptions = "Options when targeting a .uplugin or .uproject file:";
            const string pluginOptions = "Options when building a plugin:";
            const string projectOptions = "Options when building a project:";
            const string cicdOptions = "Options when building on CI/CD:";
            const string cicdEngineOptions = "Options when building the engine on CI/CD:";

            // ==== General options

            Path = new Option<PathSpec>(
                "--path",
                description: "The directory path that contains a .uproject file, a .uplugin file, or a BuildConfig.json file. If this parameter isn't provided, defaults to the current working directory.",
                parseArgument: PathSpec.ParsePathSpec,
                isDefault: true);
            Path.AddAlias("-p");
            Path.Arity = ArgumentArity.ExactlyOne;

            Distribution = new Option<DistributionSpec?>(
                "--distribution",
                description: "The distribution to build if targeting a BuildConfig.json file.",
                parseArgument: DistributionSpec.ParseDistributionSpec(serviceProvider, Path),
                isDefault: true);
            Distribution.AddAlias("-d");
            Distribution.Arity = ArgumentArity.ExactlyOne;
            Distribution.ArgumentGroupName = buildConfigOptions;

            Engine = new Option<EngineSpec>(
                "--engine",
                description: "The engine to use for the build.",
                parseArgument: EngineSpec.ParseEngineSpec(Path, Distribution),
                isDefault: true);
            Engine.AddAlias("-e");
            Engine.Arity = ArgumentArity.ExactlyOne;

            Test = new Option<string[]>(
                "--test",
                description: "Executes the specified tests after building. If specifying --test without arguments, all tests are run.");
            Test.Arity = ArgumentArity.ZeroOrMore;

            Deploy = new Option<string[]>(
                "--deploy",
                description: "Executes the specified deployments after building (and testing if --test is set). If specifying --deploy without arguments, all deployments are run.");
            Deploy.Arity = ArgumentArity.ZeroOrMore;

            StrictIncludes = new Option<bool>(
                "--strict-includes",
                description: "If set, disables unity and PCH builds. This forces all files to have the correct #include directives, at the cost of increased build time.");

            // ==== .uproject / .uplugin options

            Shipping = new Option<bool>(
                "--shipping",
                description: "If set, builds for Shipping instead of Development.");
            Shipping.AddValidator(result =>
            {
                PathSpec? pathSpec;
                try
                {
                    pathSpec = result.GetValueForOption(Path);
                }
                catch
                {
                    result.ErrorMessage = $"Can't use --{result.Option.Name} because --{Path.Name} is invalid.";
                    return;
                }
                if (pathSpec == null)
                {
                    result.ErrorMessage = $"Can't use --{result.Option.Name} because --{Path.Name} is invalid.";
                    return;
                }
                if (pathSpec.Type == PathSpecType.BuildConfig)
                {
                    result.ErrorMessage = $"Can't use --{result.Option.Name} because --{Path.Name} points to a BuildConfig.json.";
                    return;
                }
            });
            Shipping.ArgumentGroupName = uprojectpluginOptions;

            Platform = new Option<string[]>(
                "--platform",
                description: "Add this platform to the build. You can pass this option multiple times to target many platforms. The host platform is always built.")
            {
                ArgumentGroupName = uprojectpluginOptions
            };

            PluginPackage = new Option<string>(
                "--plugin-package",
                description: "When building a .uplugin file, specifies if and how the plugin should be packaged (defaults to 'none'). When building from a BuildConfig.json file, it can be explicitly set to 'none' to turn off the plugin packaging steps (values other than 'none' are not permitted; the BuildConfig.json file controls how the plugin is packaged).");
            PluginPackage.FromAmong("none", "generic", "marketplace", "fab");
            PluginPackage.ArgumentGroupName = uprojectpluginOptions;

            // ==== Plugin options, regardless of build type

            PluginVersionName = new Option<string?>(
                "--plugin-version-name",
                description:
                    """
                        Set the plugin package to use this version name instead of the auto-generated default.
                        If this option is not provided, and you are not building on a CI server, the version will be set to 'Unversioned'.
                        If this option is not provided, and you are building on a CI server, UET will use the format, generating versions such as '2023.12.30-5.2-1aeb4233'.
                        If you are building on a CI server and only want to override the date component of the auto-generated version, you can set the 'OVERRIDE_DATE_VERSION' environment variable instead of using this option.
                        """)
            {
                ArgumentGroupName = pluginOptions
            };

            PluginVersionNumber = new Option<long?>(
                "--plugin-version-number",
                description:
                    """
                        Set the plugin package to use this version number instead of the auto-generated default.
                        If this option is not provided, and you are not building on a CI server, the version number will be set to 10000.
                        If this option is not provided, and you are building on a CI server, UET will compute a version number from the UNIX timestamp and engine version number.
                        """)
            {
                ArgumentGroupName = pluginOptions
            };

            // ==== Project options, regardless of build type

            ProjectStagingDirectory = new Option<string>(
                "--project-staging-directory",
                description: "When building a project, either as a .uproject or via BuildConfig.json, overrides the path that project builds are staged to. The default is __REPOSITORY_ROOT__/Saved/StagedBuilds which places builds underneath the 'Saved/StagedBuilds' folder in the project. You can use absolute paths here and you can use __REPOSITORY_ROOT__ to refer to the project folder.")
            {
                ArgumentGroupName = projectOptions
            };

            // ==== CI/CD options

            Executor = new Option<string>(
                "--executor",
                description: "The executor to use.",
                getDefaultValue: () => "local");
            Executor.AddAlias("-x");
            Executor.FromAmong("local", "gitlab", "jenkins");
            Executor.ArgumentGroupName = cicdOptions;

            ExecutorOutputFile = new Option<string>(
                "--executor-output-file",
                description: "If the executor runs the build externally (e.g. a build server), this is the path to the emitted file that should be passed as the job or build description into the build server.")
            {
                ArgumentGroupName = cicdOptions
            };

            ExecutorGitUrl = new Option<Uri>(
                "--executor-git-url",
                description: "If the executor runs the build externally and is not implicitly integrated with git (e.g. Jenkins), the URL to the git repository to clone (e.g. 'https://user-name:access-token@git.example.com/folders/project.git').")
            {
                ArgumentGroupName = cicdOptions
            };

            ExecutorGitBranch = new Option<string>(
                "--executor-git-branch",
                description: "If the executor runs the build externally and is not implicitly integrated with git (e.g. Jenkins), the branch of the git repository to checkout.")
            {
                ArgumentGroupName = cicdOptions
            };

            WindowsSharedStoragePath = new Option<string?>(
                "--windows-shared-storage-path",
                description: "If the build is running across multiple machines (depending on the executor), this is the network share for Windows machines to access.")
            {
                ArgumentGroupName = cicdOptions
            };

            WindowsSdksPath = new Option<string?>(
                "--windows-sdks-path",
                description: "The path that UET will automatically manage and install platform SDKs, and store them in the provided path on Windows machines. This should be a local path; the SDKs will be installed on each machine as they're needed.")
            {
                ArgumentGroupName = cicdOptions
            };

            MacSharedStoragePath = new Option<string?>(
                "--mac-shared-storage-path",
                description: "If the build is running across multiple machines (depending on the executor), this is the local path on macOS pre-mounted to the network share.")
            {
                ArgumentGroupName = cicdOptions
            };

            MacSdksPath = new Option<string?>(
                "--mac-sdks-path",
                description: "The path that UET will automatically manage and install platform SDKs, and store them in the provided path on macOS machines. This should be a local path; the SDKs will be installed on each machine as they're needed.")
            {
                ArgumentGroupName = cicdOptions
            };

            WindowsSharedGitCachePath = new Option<string?>(
                "--windows-shared-git-cache-path",
                description: "If the build is running across multiple machines (depending on the executor), this is the network share where Git commits and Git dependencies are cached, so that they don't need to be re-downloaded on each machine. If not specified, each machine will download their own copy of the commits and Git dependencies.")
            {
                ArgumentGroupName = cicdEngineOptions
            };

            MacSharedGitCachePath = new Option<string?>(
                "--mac-shared-git-cache-path",
                description: "If the build is running across multiple machines (depending on the executor), this is the local path on macOS pre-mounted to the network share where Git commits and Git dependencies are cached, so that they don't need to be re-downloaded on each machine. If not specified, each machine will download their own copy of the commits and Git dependencies.")
            {
                ArgumentGroupName = cicdEngineOptions
            };
        }
    }
}
