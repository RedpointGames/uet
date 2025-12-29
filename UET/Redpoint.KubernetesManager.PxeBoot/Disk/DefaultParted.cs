namespace Redpoint.KubernetesManager.PxeBoot.Disk
{
    using Microsoft.Extensions.Logging;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using UET.Commands.Internal.PxeBoot;

    internal class DefaultParted : IParted
    {
        private readonly ILogger<DefaultParted> _logger;
        private readonly IPathResolver _pathResolver;
        private readonly IProcessExecutor _processExecutor;

        public DefaultParted(
            ILogger<DefaultParted> logger,
            IPathResolver pathResolver,
            IProcessExecutor processExecutor)
        {
            _logger = logger;
            _pathResolver = pathResolver;
            _processExecutor = processExecutor;
        }

        public Task<string[]> GetDiskPathsAsync(CancellationToken cancellationToken)
        {
            var dev = new DirectoryInfo("/dev/disk/by-diskseq");
            var results = new List<string>();
            if (dev.Exists)
            {
                foreach (var fileInfo in dev.GetFiles())
                {
                    if (fileInfo.LinkTarget == null)
                    {
                        // Ignore anything that isn't a link.
                        continue;
                    }
                    if (fileInfo.LinkTarget.Contains("loop", StringComparison.Ordinal))
                    {
                        // Ignore loopback devices.
                        continue;
                    }
                    if (fileInfo.Name.Contains("part", StringComparison.Ordinal))
                    {
                        // Ignore partitions.
                        continue;
                    }

                    _logger.LogInformation($"Discovered disk {fileInfo.FullName} ({fileInfo.LinkTarget})");
                    results.Add(fileInfo.FullName);
                }
            }
            return Task.FromResult(results.ToArray());
        }

        public async Task<PartedDisk> GetDiskAsync(string path, CancellationToken cancellationToken)
        {
            var parted = await _pathResolver.ResolveBinaryPath("parted");

            var diskInfoOutput = new StringBuilder();
            var exitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = parted,
                    Arguments = ["-s", "-j", path, "print"],
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(diskInfoOutput),
                cancellationToken);

            var diskInfo = JsonSerializer.Deserialize(
                diskInfoOutput.ToString(),
                PartedJsonSerializerContext.Default.PartedOutput);

            if (exitCode != 0 && diskInfo?.Disk?.Label != "unknown")
            {
                throw new InvalidOperationException($"'parted print' exited with non-zero exit code {exitCode}");
            }

            _logger.LogInformation($"Queried disk information: {JsonSerializer.Serialize(diskInfo, PartedJsonSerializerContext.Default.PartedOutput)}");

            return diskInfo!.Disk!;
        }

        public async Task RunCommandAsync(string diskPath, string[] args, CancellationToken cancellationToken)
        {
            var parted = await _pathResolver.ResolveBinaryPath("parted");
            var exitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = parted,
                    Arguments = ["-s", "-j", diskPath, .. args],
                },
                CaptureSpecification.Passthrough,
                cancellationToken);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"'parted {string.Join(" ", args)}' exited with non-zero exit code {exitCode}");
            }
        }
    }
}
