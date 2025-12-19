namespace Redpoint.Uefs.Commands.List
{
    using Redpoint.CommandLine;
    using Redpoint.Uefs.Protocol;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using static Redpoint.Uefs.Protocol.Uefs;

    public class ListCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithInstance<ListCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("list", "List UEFS mounts on the local system daemon.");
                })
            .Build();

        private sealed class ListCommandInstance : ICommandInstance
        {
            private readonly UefsClient _client;

            public ListCommandInstance(UefsClient client)
            {
                _client = client;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var mounts = (await _client.ListAsync(new ListRequest())).Mounts;

                if (mounts.Count > 0)
                {
                    Console.WriteLine("current mounts:");
                    foreach (var mount in mounts)
                    {
                        var persistenceInfo = string.Empty;
                        switch (mount.StartupBehaviour)
                        {
                            case StartupBehaviour.MountOnStartup:
                                switch (mount.WriteScratchPersistence)
                                {
                                    case WriteScratchPersistence.DiscardOnUnmount:
                                        persistenceInfo = " (persistent, ro)";
                                        break;
                                    case WriteScratchPersistence.Keep:
                                        persistenceInfo = " (persistent, rw)";
                                        break;
                                }
                                break;
                        }

                        string source;
                        if (!string.IsNullOrWhiteSpace(mount.TagHint))
                        {
                            source = mount.TagHint;
                        }
                        else if (!string.IsNullOrWhiteSpace(mount.PackagePath))
                        {
                            source = mount.PackagePath;
                        }
                        else if (!string.IsNullOrWhiteSpace(mount.GitUrl))
                        {
                            source = $"{mount.GitCommit} in {mount.GitUrl}";
                        }
                        else
                        {
                            source = "(unknown)";
                        }
                        Console.WriteLine($"{mount.Id}: {source} -> {mount.MountPath}{persistenceInfo}");
                    }
                }
                else
                {
                    Console.WriteLine("there are no paths mounted on this system");
                }

                return 0;
            }
        }
    }
}
