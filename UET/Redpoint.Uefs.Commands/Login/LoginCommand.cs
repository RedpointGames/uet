﻿namespace Redpoint.Uefs.Commands.Login
{
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Redpoint.ThirdParty.CredentialManagement;
    using Redpoint.Uefs.ContainerRegistry;

    public static class LoginCommand
    {
        internal class Options
        {
            public Option<string> Host;
            public Option<string> Username;
            public Option<string> Password;

            public Options()
            {
                Host = new Option<string>(
                    "--host",
                    description: "The host to set the credential for.");
                Host.Arity = ArgumentArity.ExactlyOne;

                Username = new Option<string>(
                    "--user",
                    description: "The username to connect.");
                Username.Arity = ArgumentArity.ExactlyOne;

                Password = new Option<string>(
                    "--pass",
                    description: "The password to connect.");
                Password.Arity = ArgumentArity.ExactlyOne;
            }
        }

        public static Command CreateLoginCommand()
        {
            var options = new Options();
            var command = new Command("login", "Authenticate to a container registry.");
            command.AddAllOptions(options);
            command.AddCommonHandler<LoginCommandInstance>(options);
            return command;
        }

        private class LoginCommandInstance : ICommandInstance
        {
            private readonly Options _options;

            public LoginCommandInstance(Options options)
            {
                _options = options;
            }

            public Task<int> ExecuteAsync(InvocationContext context)
            {
                var host = context.ParseResult.GetValueForOption(_options.Host)!;
                var username = context.ParseResult.GetValueForOption(_options.Username)!;
                var password = context.ParseResult.GetValueForOption(_options.Password)!;

                var dockerJson = new DockerConfigJson();
                var dockerJsonPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".docker",
                    "config.json");
                if (!File.Exists(dockerJsonPath))
                {
                    if (!Directory.Exists(Path.GetDirectoryName(dockerJsonPath)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dockerJsonPath)!);
                    }
                }
                else
                {
                    dockerJson = JsonSerializer.Deserialize(File.ReadAllText(dockerJsonPath), UefsRegistryJsonSerializerContext.Default.DockerConfigJson);
                }

                if (dockerJson!.CredsStore == "wincred")
                {
                    var credential = new Credential
                    {
                        Target = host,
                        Username = username,
                        Password = password,
                        PersistanceType = PersistanceType.LocalComputer,
                        Type = CredentialType.Generic,
                    };
                    credential.Save();
                    Console.WriteLine($"set credential for {host} in Windows Credential Store");
                }
                else
                {
                    if (dockerJson!.Auths == null)
                    {
                        dockerJson.Auths = new Dictionary<string, DockerAuthSetting>();
                    }

                    dockerJson.Auths[host] = new DockerAuthSetting
                    {
                        Auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"))
                    };

                    File.WriteAllText(dockerJsonPath, JsonSerializer.Serialize(dockerJson, UefsRegistryJsonSerializerContext.Default.DockerConfigJson));
                    Console.WriteLine($"set credential for {host} in .docker/config.json");
                }

                return Task.FromResult(0);
            }
        }
    }
}
