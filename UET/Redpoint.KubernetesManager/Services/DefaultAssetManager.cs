namespace Redpoint.KubernetesManager.Services
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using System.Formats.Tar;
    using System.IO.Compression;
    using System.Threading.Tasks;

    internal class DefaultAssetManager : IAssetManager
    {
        private readonly ILogger<DefaultAssetManager> _logger;
        private readonly IPathProvider _pathProvider;
        private readonly IAssetConfiguration _configuration;

        public DefaultAssetManager(
            ILogger<DefaultAssetManager> logger,
            IPathProvider pathProvider,
            IAssetConfiguration configuration)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _configuration = configuration;
        }

        public async Task EnsureAsset(string configKey, string filename, CancellationToken cancellationToken)
        {
            var assetsPath = Path.Combine(_pathProvider.RKMRoot, "assets", _pathProvider.RKMVersion);
            var assetsFilename = Path.Combine(assetsPath, filename);

            Directory.CreateDirectory(assetsPath);

            if (!File.Exists(assetsFilename))
            {
                using (var client = new HttpClient())
                {
                    using (var writer = new FileStream(assetsFilename + ".tmp", FileMode.Create, FileAccess.Write))
                    {
                        _logger.LogInformation($"Downloading {_configuration[configKey]} to {assetsFilename + ".tmp"}...");
                        using (var stream = await client.GetStreamAsync(new Uri(_configuration[configKey]!), cancellationToken))
                        {
                            await stream.CopyToAsync(writer, cancellationToken);
                        }
                        _logger.LogInformation($"Downloaded {_configuration[configKey]} to {assetsFilename + ".tmp"}.");
                    }
                }

                _logger.LogInformation($"Moving downloaded asset {assetsFilename + ".tmp"} into place to {assetsFilename}.");
                File.Move(assetsFilename + ".tmp", assetsFilename);
                _logger.LogInformation($"Moved downloaded asset {assetsFilename + ".tmp"} into place to {assetsFilename}.");
            }
        }

        public async Task ExtractAsset(string filename, string target, CancellationToken cancellationToken, string? trimLeading = null)
        {
            var assetsPath = Path.Combine(_pathProvider.RKMRoot, "assets", _pathProvider.RKMVersion);
            var assetsFilename = Path.Combine(assetsPath, filename);

            if (File.Exists(Path.Combine(target, ".rkm-flag")) &&
                File.ReadAllText(Path.Combine(target, ".rkm-flag")) == _pathProvider.RKMVersion)
            {
                _logger.LogInformation($"Asset {assetsFilename} has already been extracted to {target}.");
                return;
            }

            if (assetsFilename.EndsWith(".tar.gz", StringComparison.Ordinal))
            {
                using var archive = new FileStream(assetsFilename, FileMode.Open, FileAccess.Read);
                using var gzip = new GZipStream(archive, CompressionMode.Decompress);
                using var tar = new TarReader(gzip);

                while (tar.GetNextEntry() is TarEntry entry)
                {
                    var entryName = entry.Name;
                    if (trimLeading != null && entryName.StartsWith(trimLeading, StringComparison.Ordinal))
                    {
                        entryName = entryName.Substring(trimLeading.Length + 1);
                        if (string.IsNullOrWhiteSpace(entryName))
                        {
                            continue;
                        }
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(target, entryName))!);
                    if (!entryName.EndsWith('\\') && !entryName.EndsWith('/') && !string.IsNullOrWhiteSpace(entryName))
                    {
                        _logger.LogInformation($"Extracting: {entryName}");
                        await entry.ExtractToFileAsync(Path.Combine(target, entryName + ".tmp"), overwrite: true, cancellationToken);
                        File.Move(Path.Combine(target, entryName + ".tmp"), Path.Combine(target, entryName), true);
                    }
                }
            }
            else if (assetsFilename.EndsWith(".zip", StringComparison.Ordinal))
            {
                using var zip = ZipFile.OpenRead(assetsFilename);
                foreach (var entry in zip.Entries)
                {
                    var entryName = entry.FullName;
                    if (trimLeading != null && entryName.StartsWith(trimLeading, StringComparison.Ordinal))
                    {
                        entryName = entryName.Substring(trimLeading.Length + 1);
                    }

                    if (entryName.EndsWith('/') || entryName.EndsWith('\\'))
                    {
                        // This is a directory, we don't have to "extract" it.
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(entryName))
                    {
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(target, entryName))!);
                    if (!entryName.EndsWith('\\') && !entryName.EndsWith('/') && !string.IsNullOrWhiteSpace(entryName))
                    {
                        _logger.LogInformation($"Extracting: {entryName}");
#pragma warning disable CA5389
                        entry.ExtractToFile(Path.Combine(target, entryName + ".tmp"), true);
#pragma warning restore CA5389
                        File.Move(Path.Combine(target, entryName + ".tmp"), Path.Combine(target, entryName), true);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }
            else
            {
                throw new NotImplementedException($"Encountered archive with unknown extension: {assetsFilename}");
            }

            await File.WriteAllTextAsync(Path.Combine(target, ".rkm-flag"), _pathProvider.RKMVersion);
            _logger.LogInformation($"Asset {assetsFilename} has been extracted to {target}.");
        }
    }
}
