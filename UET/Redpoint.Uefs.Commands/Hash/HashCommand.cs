namespace Redpoint.Uefs.Commands.Hash
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.CommandLine;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    public class HashCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<HashCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("hash", "Generate a hash digest for an existing package.");
                })
            .Build();

        internal sealed class Options
        {
            public Option<FileInfo> PackagePath;

            public Options()
            {
                PackagePath = new Option<FileInfo>(
                    "--pkg",
                    description: "The path to the package to hash.");
                PackagePath.Arity = ArgumentArity.ExactlyOne;
            }
        }

        private sealed class HashCommandInstance : ICommandInstance
        {
            private readonly IFileHasher _fileHasher;
            private readonly Options _options;

            public HashCommandInstance(
                IFileHasher fileHasher,
                Options options)
            {
                _fileHasher = fileHasher;
                _options = options;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var packagePath = context.ParseResult.GetValueForOption(_options.PackagePath);
                if (packagePath == null || !packagePath.Exists)
                {
                    Console.Error.WriteLine("error: input package does not exist.");
                    return 1;
                }

                await _fileHasher.ComputeHashAsync(packagePath).ConfigureAwait(false);

                Console.WriteLine("hash complete");
                return 0;
            }
        }
    }
}
