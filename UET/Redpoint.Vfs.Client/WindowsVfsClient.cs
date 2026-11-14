namespace Redpoint.Vfs.Client
{
    using Redpoint.Concurrency;
    using System;
    using System.IO.Pipes;
    using System.Runtime.Versioning;
    using System.Security.Principal;
    using System.Text.Json;

    [SupportedOSPlatform("windows6.2")]
    internal class WindowsVfsClient : IVfsClient
    {
        public async Task MountAsync(string vfsHostName, Uri sourceUri, string mountPath)
        {
            await using (new NamedPipeClientStream(
                ".",
                $"RedpointVFS_{vfsHostName}",
                PipeDirection.InOut,
                PipeOptions.None,
                TokenImpersonationLevel.Delegation)
                    .AsAsyncDisposable(out var pipeClient)
                    .ConfigureAwait(false))
            {
                await pipeClient.ConnectAsync().ConfigureAwait(false);

                await JsonSerializer.SerializeAsync(
                    pipeClient,
                    new WindowsJsonRequest
                    {
                        Mount = true,
                        SourceUri = sourceUri.ToString(),
                        MountPath = mountPath,
                    },
                    WindowsJsonSerializerContext.Default.WindowsJsonRequest).ConfigureAwait(false);

                var response = await JsonSerializer.DeserializeAsync(
                    pipeClient,
                    WindowsJsonSerializerContext.Default.WindowsJsonResponse).ConfigureAwait(false);

                if (!response?.Ok ?? false)
                {
                    throw new MountFailureException();
                }
            }
        }

        public async Task UnmountAsync(string vfsHostName, string mountPath)
        {
            await using (new NamedPipeClientStream(
                ".",
                $"RedpointVFS_{vfsHostName}",
                PipeDirection.InOut,
                PipeOptions.None,
                TokenImpersonationLevel.Delegation)
                    .AsAsyncDisposable(out var pipeClient)
                    .ConfigureAwait(false))
            {
                await pipeClient.ConnectAsync().ConfigureAwait(false);

                await JsonSerializer.SerializeAsync(
                    pipeClient,
                    new WindowsJsonRequest
                    {
                        Mount = false,
                        SourceUri = string.Empty,
                        MountPath = mountPath,
                    },
                    WindowsJsonSerializerContext.Default.WindowsJsonRequest).ConfigureAwait(false);

                var response = await JsonSerializer.DeserializeAsync(
                    pipeClient,
                    WindowsJsonSerializerContext.Default.WindowsJsonResponse).ConfigureAwait(false);

                if (!response?.Ok ?? false)
                {
                    throw new MountFailureException();
                }
            }
        }
    }
}
