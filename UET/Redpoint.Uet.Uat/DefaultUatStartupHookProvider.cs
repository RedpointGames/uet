namespace Redpoint.Uet.Uat
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Hashing;
    using Redpoint.Reservation;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Reservation;
    using System;
    using System.Globalization;
    using System.Reflection;
    using System.Text;

    internal class DefaultUatStartupHookProvider : IUatStartupHookProvider, IAsyncDisposable
    {
        private readonly IReservationManagerForUet _reservationManagerForUet;
        private readonly ILogger<DefaultUatStartupHookProvider> _logger;
        private SemaphoreSlim _resolvingSemaphore = new SemaphoreSlim(1);
        private string? _resolvedPath;
        private IReservation? _resolvedWorkspace;

        public DefaultUatStartupHookProvider(
            IReservationManagerForUet reservationManagerForUet,
            ILogger<DefaultUatStartupHookProvider> logger)
        {
            _reservationManagerForUet = reservationManagerForUet;
            _logger = logger;
        }

        private static async Task WriteOrUpdateFileAsync(
            string manifestResourceStreamName,
            string fullTargetPath)
        {
            if (File.Exists(fullTargetPath))
            {
                var existingFileHash = await Hash.XxHash64OfFileAsync(fullTargetPath);
                using var manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(manifestResourceStreamName);
                var manifestHash = await Hash.XxHash64Async(manifestResourceStream!);
                if (existingFileHash.Hash == manifestHash.Hash &&
                    existingFileHash.ByteLength == manifestHash.ByteLength)
                {
                    // File is already up-to-date.
                    return;
                }
            }

            using (var fileStream = new FileStream(fullTargetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using var manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(manifestResourceStreamName);
                await manifestResourceStream!.CopyToAsync(fileStream);
            }
        }

        public async Task<string> GetDotnetStartupHooksPathAsync()
        {
            if (_resolvedPath != null)
            {
                return _resolvedPath;
            }

            await _resolvingSemaphore.WaitAsync();
            try
            {
                if (_resolvedPath != null)
                {
                    return _resolvedPath;
                }

                var manifestResourcePrefix = "Redpoint.Uet.Uat.Embedded.";

                // First, hash the embedded contents that we have. This allows us to only instantiate a new copy when we have different binaries compared with a different version of UET.
                XxHash64WithLength hashWithLength;
                {
                    using var streamForHashing = new MemoryStream();
                    foreach (var manifestResourceStreamName in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                    {
                        if (manifestResourceStreamName.StartsWith(manifestResourcePrefix, StringComparison.Ordinal))
                        {
                            var filename = manifestResourceStreamName.Substring(manifestResourcePrefix.Length);
                            await streamForHashing.WriteAsync(Encoding.UTF8.GetBytes(filename));

                            using var manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(manifestResourceStreamName);
                            if (manifestResourceStream != null)
                            {
                                await manifestResourceStream.CopyToAsync(streamForHashing);
                            }
                        }
                    }
                    hashWithLength = await Hash.XxHash64Async(streamForHashing);
                }

                _logger.LogInformation($"Target hash for UAT runtime patches is: {hashWithLength.Hash}");

                var workspace = await _reservationManagerForUet.ReserveAsync("UetRuntimePatches", new string[] { hashWithLength.Hash.ToString(CultureInfo.InvariantCulture) });
                try
                {
                    // Extract only if the workspace isn't already set up.
                    foreach (var manifestResourceStreamName in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                    {
                        if (manifestResourceStreamName.StartsWith(manifestResourcePrefix, StringComparison.Ordinal))
                        {
                            var filename = manifestResourceStreamName.Substring(manifestResourcePrefix.Length);
                            await WriteOrUpdateFileAsync(
                                manifestResourceStreamName,
                                Path.Combine(workspace.ReservedPath, filename));
                        }
                    }

                    _resolvedPath = Path.Combine(workspace.ReservedPath, "Redpoint.Uet.Patching.Runtime.dll");
                    _resolvedWorkspace = workspace;
                }
                finally
                {
                    if (_resolvedPath == null)
                    {
                        await workspace.DisposeAsync();
                    }
                }
            }
            finally
            {
                _resolvingSemaphore.Release();
            }

            return _resolvedPath;
        }

        public async ValueTask DisposeAsync()
        {
            if (_resolvedWorkspace != null)
            {
                await _resolvedWorkspace.DisposeAsync();
                _resolvedWorkspace = null;
            }

            _resolvingSemaphore.Dispose();
        }
    }
}
