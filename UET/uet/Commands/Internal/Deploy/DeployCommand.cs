namespace UET.Commands.Internal.Deploy
{
    using Microsoft.Extensions.Logging;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal sealed class DeployCommand
    {
        internal sealed class Options
        {
            public Option<string> Target;
            public Option<string> PackageType;
            public Option<string> PackageTarget;
            public Option<string> PackagePlatform;
            public Option<string> PackageConfiguration;
            public Option<string> SteamAppID;
            public Option<string> SteamDepotID;
            public Option<string> SteamChannel;
            public Option<string> StagedData;

            public Options()
            {
                Target = new Option<string>("--target");
                PackageType = new Option<string>("--package-type");
                PackageTarget = new Option<string>("--package-target");
                PackagePlatform = new Option<string>("--package-platform");
                PackageConfiguration = new Option<string>("--package-configuration");
                SteamAppID = new Option<string>("--steam-app-id");
                SteamDepotID = new Option<string>("--steam-depot-id");
                SteamChannel = new Option<string>("--steam-channel");
                StagedData = new Option<string>("--staged-data");
            }
        }

        public static Command CreateDeployCommand()
        {
            var options = new Options();
            var command = new Command("deploy");
            command.AddAllOptions(options);
            command.AddCommonHandler<DeployCommandInstance>(options);
            return command;
        }

        private sealed class DeployCommandInstance : ICommandInstance
        {
            private readonly ILogger<DeployCommandInstance> _logger;

            public DeployCommandInstance(
                ILogger<DeployCommandInstance> logger)
            {
                _logger = logger;
            }

            public Task<int> ExecuteAsync(InvocationContext context)
            {
                _logger.LogError("Not yet implemented.");
                return Task.FromResult(1);
            }
        }
    }
}
