namespace UET.Commands.Config
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using System.CommandLine;
    using System.Linq;
    using System.Threading.Tasks;

    internal static class ConfigCommand
    {
        internal sealed class Options
        {
            public Option<bool> List;
            public Option<bool> On;
            public Option<bool> Off;
            public Argument<string> Name;

            public Options(IBooleanConfigSetting[] booleanConfigSettings)
            {
                List = new Option<bool>(
                    "--list",
                    description: "List all configuration options available.");
                On = new Option<bool>(
                    "--on",
                    description: "Turn the specified configuration option on.");
                Off = new Option<bool>(
                    "--off",
                    description: "Turn the specified configuration option off.");

                Name = new Argument<string>(
                    "name",
                    description: "The configuration option name.");
                Name.FromAmong(booleanConfigSettings.Select(x => x.Name).ToArray());
                Name.Arity = ArgumentArity.ZeroOrOne;
            }
        }

        public static ICommandLineBuilder RegisterConfigCommand(this ICommandLineBuilder rootBuilder)
        {
            rootBuilder.AddCommand<ConfigCommandInstance, Options>(
                _ =>
                {
                    return new Command("config", "Quickly change settings that affect how Unreal Engine projects and plugins are built.");
                },
                additionalParsingServices: (_, services) =>
                {
                    services.AddSingleton<IBooleanConfigSetting, IwyuBooleanConfigSetting>();
                    services.AddSingleton<IBooleanConfigSetting, MaxCpuBooleanConfigSetting>();
                    services.AddSingleton(sp => sp.GetServices<IBooleanConfigSetting>().ToArray());
                });
            return rootBuilder;
        }

        private sealed class ConfigCommandInstance : ICommandInstance
        {
            private readonly ILogger<ConfigCommandInstance> _logger;
            private readonly IBooleanConfigSetting[] _booleanConfigSettings;
            private readonly Options _options;

            public ConfigCommandInstance(
                ILogger<ConfigCommandInstance> logger,
                IBooleanConfigSetting[] booleanConfigSettings,
                Options options)
            {
                _logger = logger;
                _booleanConfigSettings = booleanConfigSettings;
                _options = options;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var name = context.ParseResult.GetValueForArgument(_options.Name);
                var list = context.ParseResult.GetValueForOption(_options.List);
                var on = context.ParseResult.GetValueForOption(_options.On);
                var off = context.ParseResult.GetValueForOption(_options.Off);

                if (list || name == null)
                {
                    _logger.LogInformation("The following settings are available:");
                    foreach (var setting in _booleanConfigSettings)
                    {
                        _logger.LogInformation($"'{setting.Name}' = {setting.Description}");
                    }
                    return 0;
                }
                else if (name != null)
                {
                    var setting = _booleanConfigSettings.FirstOrDefault(x => x.Name == name);
                    if (setting == null)
                    {
                        _logger.LogError($"There is no such setting with the name '{name}'.");
                        return 1;
                    }
                    else if (on && off)
                    {
                        _logger.LogError($"You must specify either --on or --off, but not both.");
                        return 1;
                    }
                    else if (on || off)
                    {
                        await setting.SetValueAsync(on, context.GetCancellationToken()).ConfigureAwait(false);
                        _logger.LogInformation($"'{name}' has been turned {(on ? "on" : "off")}.");
                        return 0;
                    }
                    else
                    {
                        var value = await setting.GetValueAsync(context.GetCancellationToken()).ConfigureAwait(false);
                        _logger.LogInformation($"'{name}' is currently turned {(value ? "on" : "off")}.");
                        return 0;
                    }
                }
                else
                {
                    _logger.LogError("You must specify a configuration setting name or pass --list.");
                    return 1;
                }
            }
        }
    }
}
