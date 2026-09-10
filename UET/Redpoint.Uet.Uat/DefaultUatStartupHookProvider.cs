namespace Redpoint.Uet.Uat
{
    using Redpoint.Hashing;
    using Redpoint.Reservation;
    using Redpoint.Uet.Workspace.Reservation;
    using System;
    using System.Reflection;
    using System.Text;

    internal class DefaultUatStartupHookProvider : IUatStartupHookProvider, IAsyncDisposable
    {
        private readonly IReservationManagerForUet _reservationManagerForUet;
        private SemaphoreSlim _resolvingSemaphore = new SemaphoreSlim(1);
        private string? _resolvedPath;
        private IReservation? _resolvedWorkspace;

        public DefaultUatStartupHookProvider(
            IReservationManagerForUet reservationManagerForUet)
        {
            _reservationManagerForUet = reservationManagerForUet;
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
                XxHash64WithLength hash;
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
                    hash = await Hash.XxHash64Async(streamForHashing);
                }

                var workspace = await _reservationManagerForUet.ReserveAsync("UetRuntimePatches", new string[] { hash.ToString() });
                try
                {
                    // Extract only if the workspace isn't already set up.
                    if (!File.Exists(Path.Combine(workspace.ReservedPath, "extracted")))
                    {
                        foreach (var manifestResourceStreamName in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                        {
                            if (manifestResourceStreamName.StartsWith(manifestResourcePrefix, StringComparison.Ordinal))
                            {
                                var filename = manifestResourceStreamName.Substring(manifestResourcePrefix.Length);
                                using (var fileStream = new FileStream(Path.Combine(workspace.ReservedPath, filename), FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    using var manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(manifestResourceStreamName);
                                    await manifestResourceStream!.CopyToAsync(fileStream);
                                }
                            }
                        }

                        File.WriteAllText(
                            Path.Combine(workspace.ReservedPath, "extracted"),
                            "ok");
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
