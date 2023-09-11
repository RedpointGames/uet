namespace Redpoint.Uefs.Commands.Build
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uefs.Commands.Hash;
    using Redpoint.Uefs.Package;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Globalization;
    using System.Threading.Tasks;

    public static class BuildCommand
    {
        internal sealed class Options
        {
            public Option<FileInfo> PackagePath;
            public Option<DirectoryInfo> DirectoryPath;

            public Options()
            {
                PackagePath = new Option<FileInfo>(
                    "--pkg",
                    description: "The path to create the package at.");
                PackagePath.Arity = ArgumentArity.ExactlyOne;

                DirectoryPath = new Option<DirectoryInfo>(
                    "--dir",
                    description: "The path to build the package from.");
                DirectoryPath.Arity = ArgumentArity.ExactlyOne;
            }
        }

        public static Command CreateBuildCommand()
        {
            var options = new Options();
            var command = new Command("build", "Build a UEFS package.");
            command.AddAllOptions(options);
            command.AddCommonHandler<BuildCommandInstance>(options);
            return command;
        }

        private sealed class BuildCommandInstance : ICommandInstance
        {
            private readonly IEnumerable<IPackageWriterFactory> _writers;
            private readonly IPackageManifestAssembler _packageManifestAssembler;
            private readonly IPackageManifestDataWriter _packageManifestDataWriter;
            private readonly IFileHasher _fileHasher;
            private readonly Options _options;

            public BuildCommandInstance(
                IServiceProvider serviceProvider,
                IPackageManifestAssembler packageManifestAssembler,
                IPackageManifestDataWriter packageManifestDataWriter,
                IFileHasher fileHasher,
                Options options)
            {
                _writers = serviceProvider.GetServices<IPackageWriterFactory>();
                _packageManifestAssembler = packageManifestAssembler;
                _packageManifestDataWriter = packageManifestDataWriter;
                _fileHasher = fileHasher;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var packagePath = context.ParseResult.GetValueForOption(_options.PackagePath);
                var directoryPath = context.ParseResult.GetValueForOption(_options.DirectoryPath);
                if (directoryPath == null || !directoryPath.Exists)
                {
                    Console.Error.WriteLine("error: the source directory must exist");
                    return 1;
                }
                if (packagePath == null)
                {
                    Console.Error.WriteLine("error: you must specify an output package path");
                    return 1;
                }

                // At the moment we only expect one writer per platform.
                var writerFactory = _writers.FirstOrDefault();
                if (writerFactory == null)
                {
                    Console.Error.WriteLine("error: this platform has no usable UEFS package writers");
                    return 1;
                }
                var writer = writerFactory.CreatePackageWriter();

                var packageManifest = _packageManifestAssembler.CreateManifestFromSourceDirectory(writer, directoryPath.FullName);
                Console.WriteLine($"found {packageManifest.Count} total entries, {packageManifest.FileCount} files to read");
                Console.WriteLine($"needs {(packageManifest.IndexSizeBytes / 1024 / 1024)} MB for index");
                Console.WriteLine($"needs {(packageManifest.DataSizeBytes / 1024 / 1024)} MB for data");

                Console.WriteLine($"opening {writerFactory.Format} package file for write and truncating...");
                var startTimestamp = DateTimeOffset.UtcNow;
                using (writer)
                {
                    try
                    {
                        writer.OpenPackageForWriting(packagePath.FullName, packageManifest.IndexSizeBytes, packageManifest.DataSizeBytes);
                        await writer.WritePackageIndex(packageManifest).ConfigureAwait(false);
                        await _packageManifestDataWriter.WriteData(writer, packageManifest).ConfigureAwait(false);
                    }
                    catch (PackageWriterException ex)
                    {
                        Console.Error.WriteLine($"error: {ex.Message}");
                        return 1;
                    }
                }
                var finishTimestamp = DateTimeOffset.UtcNow;

                var duration = finishTimestamp - startTimestamp;
                Console.WriteLine();
                if (duration.Hours == 0)
                {
                    Console.WriteLine($"package created successfully in {duration.Minutes}:{duration.Seconds.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')}");
                }
                else
                {
                    Console.WriteLine($"package created successfully in {duration.Hours}:{duration.Minutes.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')}:{duration.Seconds.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')}");
                }

                // Hash by default now since basically all use cases require
                // a digest file to be present.
                _ = await _fileHasher.ComputeHashAsync(packagePath).ConfigureAwait(false);

                return 0;
            }
        }
    }
}
