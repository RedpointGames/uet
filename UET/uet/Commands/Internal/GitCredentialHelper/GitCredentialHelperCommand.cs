namespace UET.Commands.Internal.GitCredentialHelper
{
    using Redpoint.CredentialDiscovery;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal sealed class GitCredentialHelperCommand
    {
        internal sealed class Options
        {
            public Argument<string> Operation;

            public Options()
            {
                Operation = new Argument<string>("operation");
                Operation.AddCompletions("get", "store", "erase");
            }
        }

        public static Command CreateGitCredentialHelperCommand()
        {
            var options = new Options();
            var command = new Command(
                "git-credential-helper",
                "Used as the credential helper for Git when running on a build server. This allows us to provide credentials from environment variables without setting the username and password in remote URLs.");
            command.AddAllOptions(options);
            command.AddCommonHandler<GitCredentialHelperCommandInstance>(options);
            return command;
        }

        private sealed class GitCredentialHelperCommandInstance : ICommandInstance
        {
            private readonly ICredentialDiscovery _credentialDiscovery;
            private readonly Options _options;

            public GitCredentialHelperCommandInstance(
                ICredentialDiscovery credentialDiscovery,
                Options options)
            {
                _credentialDiscovery = credentialDiscovery;
                _options = options;
            }

            public Task<int> ExecuteAsync(InvocationContext context)
            {
                var operation = context.ParseResult.GetValueForArgument(_options.Operation);
                if (operation != "get")
                {
                    // We don't handle anything other than "get".
                    return Task.FromResult(0);
                }

                // Read input request.
                var input = new Dictionary<string, string>();
                string? line;
                do
                {
                    line = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var components = line.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        input[components[0]] = components[1];
                    }
                } while (!string.IsNullOrWhiteSpace(line));

                // Attempt to perform credential discovery.
                try
                {
                    var credential = _credentialDiscovery.GetGitCredential($"{input["protocol"]}://{input["host"]}/{input["path"]}");
                    if (!string.IsNullOrWhiteSpace(credential.Username) &&
                        !string.IsNullOrWhiteSpace(credential.Password))
                    {
                        Console.WriteLine($"protocol={input["protocol"]}");
                        Console.WriteLine($"host={input["host"]}");
                        Console.WriteLine($"username={credential.Username}");
                        Console.WriteLine($"password={credential.Password}");

                        // Found credential for HTTP/HTTPS.
                        return Task.FromResult(0);
                    }

                    // The git-credential flow can't handle SSH.
                    return Task.FromResult(0);
                }
                catch (UnableToDiscoverCredentialException)
                {
                    // We don't have a useful credential.
                    return Task.FromResult(0);
                }
            }
        }

    }
}
