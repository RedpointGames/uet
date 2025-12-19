namespace Redpoint.KubernetesManager.Manifest.Client
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.KubernetesManager.Manifest;
    using System;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;

    internal class DefaultGenericManifestClient : IGenericManifestClient
    {
        private readonly ILogger<DefaultGenericManifestClient> _logger;
        private readonly IHostApplicationLifetime? _hostApplicationLifetime;

        public DefaultGenericManifestClient(
            ILogger<DefaultGenericManifestClient> logger,
            IHostApplicationLifetime? hostApplicationLifetime = null)
        {
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        private async Task PollManifestAsync<T>(
            Uri uri,
            string? manifestCachePath,
            JsonTypeInfo<T> jsonTypeInfo,
            Func<T, long, CancellationToken, Task> manifestReceived,
            CancellationToken cancellationToken) where T : class, IVersionedManifest
        {
            var buffer = new byte[16 * 1024];
            var gotInitialManifest = false;

            if (manifestCachePath != null && File.Exists(manifestCachePath))
            {
                try
                {
                    var manifestContent = File.ReadAllText(manifestCachePath, Encoding.UTF8);
                    var manifest = JsonSerializer.Deserialize<T>(
                        manifestContent,
                        jsonTypeInfo);
                    if (manifest == null)
                    {
                        _logger.LogWarning("Manifest in cache file was deserialized to a null value and ignored.");
                    }
                    // @note: Only use the cached manifest if the manifest version is compatible.
                    else if (manifest.ManifestVersion == T.ManifestCurrentVersion)
                    {
                        await manifestReceived(
                            manifest,
                            Hashing.Hash.XxHash64(manifestContent, Encoding.UTF8).Hash,
                            cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Unable to read manifest from cache file: {manifestCachePath}");
                }
            }

        retryInitialConnection:
            // Establish an initial connection.
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            ClientWebSocket webSocketClient;
            try
            {
                webSocketClient = new ClientWebSocket();
                await webSocketClient.ConnectAsync(uri, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning($"Failed to connect to WebSocket ({ex.WebSocketErrorCode}).");
                await Task.Delay(10000, cancellationToken);
                goto retryInitialConnection;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to connect to WebSocket: {ex.Message}");
                await Task.Delay(10000, cancellationToken);
                goto retryInitialConnection;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                // Try to get the manifest.
                WebSocketReceiveResult result;
                try
                {
                    result = await webSocketClient.ReceiveAsync(buffer, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to receive message from WebSocket: {ex.Message}");
                    await Task.Delay(10000, cancellationToken);
                    if (webSocketClient.State != WebSocketState.Open)
                    {
                        // WebSocket no longer open, we have to retry the whole connection.
                        goto retryInitialConnection;
                    }
                    else
                    {
                        // Try to get a message again.
                        if (gotInitialManifest)
                        {
                            continue;
                        }
                        else
                        {
                            await webSocketClient
                                .CloseAsync(WebSocketCloseStatus.InvalidMessageType, null, cancellationToken)
                                .ConfigureAwait(false);
                            goto retryInitialConnection;
                        }
                    }
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // WebSocket has closed; attempt to re-establish connection.
                    goto retryInitialConnection;
                }

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Message isn't a type we handle.
                    _logger.LogWarning("WebSocket sent a binary message, which is not expected for manifest monitoring.");
                    if (gotInitialManifest)
                    {
                        continue;
                    }
                    else
                    {
                        await webSocketClient
                            .CloseAsync(WebSocketCloseStatus.InvalidMessageType, null, cancellationToken)
                            .ConfigureAwait(false);
                        goto retryInitialConnection;
                    }
                }

                try
                {
                    var manifestContent = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (manifestCachePath != null)
                    {
                        var manifestCacheDirectoryPath = Path.GetDirectoryName(manifestCachePath);
                        if (manifestCacheDirectoryPath != null)
                        {
                            Directory.CreateDirectory(manifestCacheDirectoryPath);
                        }
                        await File.WriteAllTextAsync(
                            manifestCachePath,
                            manifestContent,
                            cancellationToken);
                    }

                    var manifest = JsonSerializer.Deserialize<T>(manifestContent, jsonTypeInfo);
                    if (manifest == null)
                    {
                        _logger.LogWarning("Received manifest deserialized to a null value.");
                        continue;
                    }
                    gotInitialManifest = true;
                    await manifestReceived(
                        manifest,
                        Hashing.Hash.XxHash64(manifestContent, Encoding.UTF8).Hash,
                        cancellationToken);

                    // Now we'll continue through the loop and try to get another update.
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to deserialize manifest from WebSocket: {ex.Message}");
                    if (gotInitialManifest)
                    {
                        continue;
                    }
                    else
                    {
                        await webSocketClient
                            .CloseAsync(WebSocketCloseStatus.InvalidMessageType, null, cancellationToken)
                            .ConfigureAwait(false);
                        goto retryInitialConnection;
                    }
                }
            }
        }

        private class ExecutionState<T> where T : class
        {
            public CancellationTokenSource? CurrentExecutingCancellationTokenSource;

            public readonly Gate WaitingOnInitialManifest = new Gate();

            public Task? PollingTask;

            public Task? ExecutingTask;

            public long PreviousHash;
        }

        public async Task RegisterAndRunWithManifestAsync<T>(
            Uri uri,
            string? manifestCachePath,
            JsonTypeInfo<T> jsonTypeInfo,
            Func<T, CancellationToken, Task> runWithManifest,
            CancellationToken cancellationToken) where T : class, IVersionedManifest
        {
            var state = new ExecutionState<T>();

            // Handle new manifests when they arrive.
            var manifestArrived = async (T newManifest, long newHash, CancellationToken _) =>
            {
                // If we are already executing a task, and the hash is the same, don't do anything.
                if (state.ExecutingTask != null && state.PreviousHash == newHash)
                {
                    _logger.LogInformation("Ignoring new manifest update as it has the same hash as the previous manifest.");
                }

                // Tell the executing task to cancel it's work via the CTS if there is one.
                if (state.CurrentExecutingCancellationTokenSource != null)
                {
                    state.CurrentExecutingCancellationTokenSource.Cancel();
                }

                // Wait for the executing task to stop if there is one.
                if (state.ExecutingTask != null)
                {
                    try
                    {
                        await state.ExecutingTask;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Unexpected exception from executing task during manifest update handling: {ex.Message}");
                    }
                }

                // Dispose the existing CTS if there is one.
                if (state.CurrentExecutingCancellationTokenSource != null)
                {
                    state.CurrentExecutingCancellationTokenSource.Dispose();
                }

                // If the manifest version doesn't match the version we expect, tell the application to exit if possible.
                if (_hostApplicationLifetime != null && newManifest.ManifestVersion != T.ManifestCurrentVersion)
                {
                    // Clear out the values because they are old and we will not be using them.
                    state.CurrentExecutingCancellationTokenSource = null;
                    state.ExecutingTask = null;

                    // Tell the application to shutdown.
                    _logger.LogInformation($"The received manifest version is {newManifest.ManifestVersion}, and we expect a manifest version of {T.ManifestCurrentVersion}. The application will now shutdown.");
                    _hostApplicationLifetime.StopApplication();
                }
                else
                {
                    // Create a new CTS for starting our new task.
                    state.CurrentExecutingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                    // Log the manifest version and hash.
                    _logger.LogInformation($"Received manifest with manifest version {newManifest.ManifestVersion} and hash {newHash}.");

                    // Execute the task in the background.
                    var executingCancellationToken = state.CurrentExecutingCancellationTokenSource.Token;
                    state.PreviousHash = newHash;
                    state.ExecutingTask = Task.Run(async () => await runWithManifest(newManifest, executingCancellationToken), executingCancellationToken);
                }
            };

            // Create our background task that will poll for manifest updates.
            state.PollingTask = Task.Run(
                async () => await PollManifestAsync(
                    uri,
                    manifestCachePath,
                    jsonTypeInfo,
                    manifestArrived,
                    cancellationToken),
                cancellationToken);

            // Wait for the cancellation token to be cancelled.
            try
            {
                while (true)
                {
                    await Task.Delay(-1, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }

            // Wait for the polling task to be done.
            try
            {
                await state.PollingTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Unexpected exception from polling task during shutdown: {ex.Message}");
            }

            // Tell the executing task to cancel it's work via the CTS if there is one.
            if (state.CurrentExecutingCancellationTokenSource != null)
            {
                state.CurrentExecutingCancellationTokenSource.Cancel();
            }

            // If there is an executing task, wait for that to be done.
            if (state.ExecutingTask != null)
            {
                try
                {
                    await state.ExecutingTask;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Unexpected exception from executing task during shutdown: {ex.Message}");
                }
            }

            // Dispose the existing CTS if there is one.
            if (state.CurrentExecutingCancellationTokenSource != null)
            {
                state.CurrentExecutingCancellationTokenSource.Dispose();
            }
        }
    }
}
