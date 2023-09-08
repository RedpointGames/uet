namespace Redpoint.Uefs.Commands.Unmount
{
    using Redpoint.GrpcPipes;
    using Redpoint.Uefs.Protocol;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Threading.Tasks;
    using static Redpoint.Uefs.Protocol.Uefs;

    public static class UnmountCommand
    {
        internal class Options
        {
            public Option<string> MountPath = new Option<string>("--dir", description: "The path that is mounted. You must specify this, the ID or --all.");
            public Option<string> Id = new Option<string>("--id", description: "The ID of the mount.");
            public Option<bool> All = new Option<bool>("--all", description: "The Git commit to pull.");
        }

        public static Command CreateUnmountCommand()
        {
            var options = new Options();
            var command = new Command("unmount", "Unmounts a UEFS package from the local system.");
            command.AddAllOptions(options);
            command.AddCommonHandler<UnmountCommandInstance>(options);
            return command;
        }

        private class UnmountCommandInstance : ICommandInstance
        {
            private readonly IRetryableGrpc _retryableGrpc;
            private readonly UefsClient _uefsClient;
            private readonly Options _options;

            public UnmountCommandInstance(
                IRetryableGrpc retryableGrpc,
                UefsClient uefsClient,
                Options options)
            {
                _retryableGrpc = retryableGrpc;
                _uefsClient = uefsClient;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var mountPath = context.ParseResult.GetValueForOption(_options.MountPath);
                var id = context.ParseResult.GetValueForOption(_options.Id);
                var all = context.ParseResult.GetValueForOption(_options.All);

                var response = await _retryableGrpc.RetryableGrpcAsync(
                    _uefsClient.ListAsync,
                    new ListRequest(),
                    new GrpcRetryConfiguration { RequestTimeout = TimeSpan.FromMinutes(60) },
                    context.GetCancellationToken());

                if (all)
                {
                    foreach (var mount in response.Mounts)
                    {
                        await _retryableGrpc.RetryableGrpcAsync(
                            _uefsClient.UnmountAsync,
                            new UnmountRequest
                            {
                                MountId = mount.Id,
                            },
                            new GrpcRetryConfiguration { RequestTimeout = TimeSpan.FromMinutes(60) },
                            context.GetCancellationToken());
                        Console.WriteLine($"successfully unmounted: {mount.MountPath}");
                    }

                    return 0;
                }
                else if (string.IsNullOrWhiteSpace(id))
                {
                    var mount = response.Mounts.FirstOrDefault(x => x.Id == id);
                    if (mount == null)
                    {
                        Console.Error.WriteLine($"error: the ID is not mounted according to the local daemon");
                        return 1;
                    }

                    await _retryableGrpc.RetryableGrpcAsync(
                        _uefsClient.UnmountAsync,
                        new UnmountRequest
                        {
                            MountId = mount.Id,
                        },
                        new GrpcRetryConfiguration { RequestTimeout = TimeSpan.FromMinutes(60) },
                        context.GetCancellationToken());
                    Console.WriteLine($"successfully unmounted: {mount.MountPath}");
                    return 0;
                }
                else if (string.IsNullOrWhiteSpace(mountPath))
                {
                    var mount = response.Mounts.FirstOrDefault(x => x.MountPath.Equals(mountPath, StringComparison.InvariantCultureIgnoreCase));
                    if (mount == null)
                    {
                        Console.Error.WriteLine($"error: the path is not mounted according to the local daemon");
                        return 1;
                    }

                    await _retryableGrpc.RetryableGrpcAsync(
                        _uefsClient.UnmountAsync,
                        new UnmountRequest
                        {
                            MountId = mount.Id,
                        },
                        new GrpcRetryConfiguration { RequestTimeout = TimeSpan.FromMinutes(60) },
                        context.GetCancellationToken());
                    Console.WriteLine($"successfully unmounted: {mount.MountPath}");
                    return 0;
                }

                Console.Error.WriteLine($"error: you must specify --all, --id or --dir");
                return 1;
            }
        }
    }
}
