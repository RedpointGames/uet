namespace UET.Commands.Internal.SyncEngineFromPerforce
{
    using Microsoft.Extensions.Logging;
    using Perforce.P4;
    using Redpoint.ProgressMonitor;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal sealed class SyncEngineFromPerforceCommand
    {
        public sealed class Options
        {
            public Option<string> Server = new Option<string>("--server", "The Perforce server URI to connect to.") { IsRequired = true };
            public Option<string> User = new Option<string>("--user", "The Perforce user to connect as.") { IsRequired = true };
            public Option<string?> Password = new Option<string?>("--password", "The Perforce password to use for connecting. If not provided, uses P4PASSWD environment variable.");
            public Option<string> Client = new Option<string>("--client", "The Perforce client workspace name to use.") { IsRequired = true };
            public Option<string> Stream = new Option<string>("--stream", "The Perforce stream to checkout to the path.") { IsRequired = true };
            public Option<DirectoryInfo> Path = new Option<DirectoryInfo>("--path", "The path to store the engine at.") { IsRequired = true };
        }

        public static Command CreateSyncEngineFromPerforceCommand()
        {
            var options = new Options();
            var command = new Command("sync-engine-from-perforce");
            command.AddAllOptions(options);
            command.AddCommonHandler<SyncEngineFromPerforceCommandInstance>(options);
            return command;
        }

        private sealed class SyncEngineFromPerforceCommandInstance : ICommandInstance
        {
            private readonly IMonitorFactory _monitorFactory;
            private readonly ILogger<SyncEngineFromPerforceCommandInstance> _logger;
            private readonly Options _options;

            public SyncEngineFromPerforceCommandInstance(
                IMonitorFactory monitorFactory,
                ILogger<SyncEngineFromPerforceCommandInstance> logger,
                Options options)
            {
                _monitorFactory = monitorFactory;
                _logger = logger;
                _options = options;
            }

            public Task<int> ExecuteAsync(InvocationContext context)
            {
                var argServer = context.ParseResult.GetValueForOption(_options.Server)!;
                var argUser = context.ParseResult.GetValueForOption(_options.User)!;
                var argPassword = context.ParseResult.GetValueForOption(_options.Password);
                if (string.IsNullOrWhiteSpace(argPassword))
                {
                    argPassword = Environment.GetEnvironmentVariable("P4PASSWD");
                }
                var argClient = context.ParseResult.GetValueForOption(_options.Client)!;
                var argStream = context.ParseResult.GetValueForOption(_options.Stream)!;
                var argPath = context.ParseResult.GetValueForOption(_options.Path)!;
                argPath.Create();

                // Perforce annoyingly requires the current directory to our
                // target directory.
                var oldWorkingDir = Environment.CurrentDirectory;
                try
                {
                    Environment.CurrentDirectory = argPath.FullName;

                    // Create server connection.
                    _logger.LogInformation($"Connecting to Perforce server: {argServer}");
                    var server = new Server(new ServerAddress(argServer));
                    var repository = new Repository(server);
                    _logger.LogInformation($"Using Perforce username: {argUser}");
                    repository.Connection.UserName = argUser;
                    repository.Connection.Connect(null);
                    _logger.LogInformation($"Syncing to target directory: {Environment.CurrentDirectory}");

                    // Authenticate with Perforce server.
                    if (!string.IsNullOrWhiteSpace(argPassword))
                    {
                        _logger.LogInformation($"Authenticating to Perforce using provided password.");
                        repository.Connection.Login(argPassword, null, null);
                    }
                    else
                    {
                        _logger.LogInformation($"Skipping authentication to Perforce.");
                    }

                    // Check if client workspace exists. If it doesn't, create it.
                    var client = repository.GetClient(argClient);
                    if (client == null)
                    {
                        _logger.LogInformation($"No existing client workspace called '{argClient}' exists, creating it and mapping to stream '{argStream}'...");
                        client = new Client();
                        client.Name = argClient;
                        client.ClientType = ClientType.@readonly;
                        client.Stream = argStream;
                        client.Root = argPath.FullName;
                        client.Options = ClientOption.Clobber;
                        repository.CreateClient(client);
                    }
                    else
                    {
                        if (client.Stream != argStream ||
                            client.Root != argPath.FullName ||
                            client.Options != ClientOption.Clobber)
                        {
                            _logger.LogInformation($"Existing client workspace called '{argClient}' does not have the latest settings, updating it...");
                            client.Stream = argStream;
                            client.Root = argPath.FullName;
                            client.Options = ClientOption.Clobber;
                            repository.UpdateClient(client);
                        }
                        else
                        {
                            _logger.LogInformation($"Found existing client workspace called '{argClient}'.");
                        }
                    }
                    repository.Connection.Client = client;

                    // Synchronise files to the workspace from the latest revision.
                    _logger.LogInformation($"Synchronising files...");
                    _logger.LogInformation("The Perforce library and client have no callbacks for progress reporting, so we can't display progress. You'll just have to wait for this UET command to finish.");
                    var p4Server = repository.Connection.getP4Server();
                    p4Server.CommandEcho += P4Server_CommandEcho;
                    p4Server.ErrorReceived += P4Server_ErrorReceived;
                    var syncOptions = new SyncFilesCmdOptions(SyncFilesCmdFlags.None, pthreads: Environment.ProcessorCount);
                    client.SyncFiles(
                        syncOptions,
                        new FileSpec(
                            new DepotPath($"{argStream}/..."),
                            VersionSpec.Head));
                    _logger.LogInformation("Perform synchronisation complete.");
                }
                finally
                {
                    Environment.CurrentDirectory = oldWorkingDir;
                }

                return Task.FromResult(0);
            }

            private void P4Server_CommandEcho(string data)
            {
                _logger.LogInformation($"Running Perforce command: {data}");
            }

            private void P4Server_ErrorReceived(uint cmdId, int severity, int errorNumber, string data)
            {
                _logger.LogError($"Encountered Perforce error: {cmdId} {severity} {errorNumber} {data}");
            }
        }
    }
}
