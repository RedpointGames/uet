namespace Redpoint.KubernetesManager.PxeBoot.Bootmgr
{
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Reflection.Emit;
    using System.Text;
    using System.Threading.Tasks;
    using Tpm2Lib;

    internal class DefaultEfiBootManager : IEfiBootManager
    {
        private readonly IEfiBootManagerParser _parser;
        private readonly IProcessExecutor _processExecutor;
        private readonly IPathResolver _pathResolver;

        public DefaultEfiBootManager(
            IEfiBootManagerParser parser,
            IProcessExecutor processExecutor,
            IPathResolver pathResolver)
        {
            _parser = parser;
            _processExecutor = processExecutor;
            _pathResolver = pathResolver;
        }

        public async Task<EfiBootManagerConfiguration> GetBootManagerConfigurationAsync(
            CancellationToken cancellationToken)
        {
            var output = new StringBuilder();
            await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = await _pathResolver.ResolveBinaryPath("efibootmgr"),
                    Arguments = [],
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(output),
                cancellationToken);
            return _parser.ParseBootManagerConfiguration(output.ToString());
        }

        public async Task RemoveBootManagerEntryAsync(int bootEntry, CancellationToken cancellationToken)
        {
            await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = await _pathResolver.ResolveBinaryPath("efibootmgr"),
                    Arguments = ["-b", bootEntry.ToString("X4", CultureInfo.InvariantCulture), "-B"],
                },
                CaptureSpecification.Passthrough,
                cancellationToken);
        }

        public async Task AddBootManagerDiskEntryAsync(string disk, int partition, string label, string path, CancellationToken cancellationToken)
        {
            await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = await _pathResolver.ResolveBinaryPath("efibootmgr"),
                    Arguments = [
                        "-C",
                        "-d",
                        disk,
                        "-p",
                        partition.ToString(CultureInfo.InvariantCulture),
                        "-L",
                        label,
                        "-l",
                        path
                    ],
                },
                CaptureSpecification.Passthrough,
                cancellationToken);
        }

        public async Task SetBootManagerBootOrderAsync(IEnumerable<int> bootOrder, CancellationToken cancellationToken)
        {
            var bootOrderString = string.Join(",", bootOrder.Select(x => x.ToString("X4", CultureInfo.InvariantCulture)));
            await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = await _pathResolver.ResolveBinaryPath("efibootmgr"),
                    Arguments = [
                        "-o",
                        bootOrderString,
                    ],
                },
                CaptureSpecification.Passthrough,
                cancellationToken);
        }

        public async Task SetBootManagerEntryActiveAsync(int bootEntry, bool active, CancellationToken cancellationToken)
        {
            await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = await _pathResolver.ResolveBinaryPath("efibootmgr"),
                    Arguments = [
                        "-b",
                        bootEntry.ToString("X4", CultureInfo.InvariantCulture),
                        active ? "-a" : "-A",
                    ],
                },
                CaptureSpecification.Passthrough,
                cancellationToken);
        }
    }
}
