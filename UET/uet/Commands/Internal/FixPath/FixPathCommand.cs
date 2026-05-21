namespace UET.Commands.Internal.FixPath
{
    using Redpoint.CommandLine;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Security.Principal;
    using System.Text;

    internal sealed class FixPathCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<FixPathCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("fix-path");
                })
            .Build();

        internal sealed class Options
        {
        }

        private sealed class FixPathCommandInstance : ICommandInstance
        {
            private static bool IsAdministrator
            {
                get
                {
                    if (OperatingSystem.IsWindows())
                    {
                        using (var identity = WindowsIdentity.GetCurrent())
                        {
                            var principal = new WindowsPrincipal(identity);
                            return principal.IsInRole(WindowsBuiltInRole.Administrator);
                        }
                    }
                    return false;
                }
            }

            private static void ProcessPath(EnvironmentVariableTarget target)
            {
                var path = new HashSet<string>((Environment.GetEnvironmentVariable("PATH", target) ?? "").Split(Path.PathSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

                foreach (var p in path)
                {
                    Console.WriteLine(p);
                }

                Environment.SetEnvironmentVariable("PATH", string.Join(Path.PathSeparator, path), target);
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                ProcessPath(EnvironmentVariableTarget.User);

                if (IsAdministrator)
                {
                    ProcessPath(EnvironmentVariableTarget.Machine);
                }

                return 0;
            }
        }
    }
}
