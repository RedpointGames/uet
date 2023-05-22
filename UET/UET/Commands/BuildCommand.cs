namespace UET.Commands
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using UET.Commands.Build;

    internal class BuildCommand
    {
        internal class Options
        {
            public Option<string> Distribution = new Option<string>(
                "--distribution",
                "The distribution to build, as defined in BuildConfig.json.");

            public Option<bool> ThenTest = new Option<bool>(
                "--then-test",
                "If set, executes the tests after building.");

            public Option<bool> ThenDeploy = new Option<bool>(
                "--then-deploy",
                "If set, executes the deployment after building.");

            public Option<bool> StrictIncludes = new Option<bool>(
                "--strict-includes",
                "If set, enables IWYU building to ensure all headers are correct.");
        }

        public static Command CreateBuildCommand(ServiceCollection services)
        {
            var options = new Options();
            var buildCommand = new Command("build", "Execute a build on the local machine.");
            buildCommand.AddOption(options.Distribution);
            buildCommand.AddOption(options.ThenTest);
            buildCommand.AddOption(options.ThenDeploy);
            buildCommand.AddOption(options.StrictIncludes);
            buildCommand.SetHandler(async (context) =>
            {
                services.AddSingleton(sp => context);
                services.AddSingleton(sp => options);
                services.AddSingleton<BuildCommandInstance>();
                services.AddSingleton<BuildEngine>();
                services.AddSingleton<BuildProject>();
                services.AddSingleton<BuildPlugin>();
                var sp = services.BuildServiceProvider();
                var instance = sp.GetRequiredService<BuildCommandInstance>();
                context.ExitCode = await instance.ExecuteAsync(context);
            });
            return buildCommand;
        }

        private class BuildCommandInstance
        {
            private readonly IServiceProvider _serviceProvider;
            private readonly Options _options;
            private readonly ILogger<BuildCommandInstance> _logger;

            public BuildCommandInstance(
                IServiceProvider serviceProvider,
                Options options,
                ILogger<BuildCommandInstance> logger)
            {
                _serviceProvider = serviceProvider;
                _options = options;
                _logger = logger;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                _logger.LogInformation($"Starting build...");
                _logger.LogInformation($"--distribution:       {context.ParseResult.GetValueForOption(_options.Distribution)}");
                _logger.LogInformation($"--then-test:          {context.ParseResult.GetValueForOption(_options.ThenTest)}");
                _logger.LogInformation($"--then-deploy:        {context.ParseResult.GetValueForOption(_options.ThenDeploy)}");
                _logger.LogInformation($"--strict-includes:    {context.ParseResult.GetValueForOption(_options.StrictIncludes)}");

                /*
                var buildConfig = _buildConfigProvider.GetBuildConfig();

                switch (buildConfig.Type)
                {
                    case BuildConfigType.Engine:
                        return await _serviceProvider.GetRequiredService<BuildEngine>().ExecuteAsync(context, (BuildConfigEngine)buildConfig);
                    case BuildConfigType.Project:
                        return await _serviceProvider.GetRequiredService<BuildProject>().ExecuteAsync(context, buildConfig);
                    case BuildConfigType.Plugin:
                        return await _serviceProvider.GetRequiredService<BuildPlugin>().ExecuteAsync(context, buildConfig);
                    default:
                        throw new NotSupportedException();
                }
                */

                return 1;
            }
        }
    }
}
