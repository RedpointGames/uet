# Redpoint.Uba

This library provides an implementation of `IProcessExecutor` that dispatches processes to Unreal Build Accelerator agents.

## Example

After registering the services by calling `.AddUba()` on your service collection, you can inject `IUbaServerFactory`. With this, you can create a UBA server, connect it to remote agents, and then run processes:

```csharp
// Provide the path to the directory that contains UbaHost.dll, libUbaHost.dylib or libUbaHost.so depending on the current platform.
// These files are available by downloading Unreal Engine: https://www.unrealengine.com/
UbaNative.Init(/* ... */);

// Set up the server that will dispatch processes.
await using (_ubaServerFactory
    .CreateServer(
        cachePath /* A path that UBA can use for storage locally. */,
        traceFilePath /* The path that UBA should write the trace file out to. */)
    .AsAsyncDisposable(out var server)
    .ConfigureAwait(false))
{
    // Connect to a remote agent that will run processes. You can call this multiple times, and
    // at any time processes are being executed.
    if (!server.AddRemoteAgent(ip, port))
    {
        // Failed to add remote agent.
    }

    // Run a command through UBA. Commands are put into a queue and then either run locally
    // or on a remote agent depending on which picks it up first.
    try
    {
        var exitCode = await server.ExecuteAsync(
            new UbaProcessSpecification /* Inherits from ProcessSpecification. */
            {
                FilePath = /* ... */,
                Arguments = /* ... */,
                // Optional setting; if true, the UBA server will prefer to wait and run this command
                // on a remote agent rather than running it locally.
                PreferRemote = true,
            },
            CaptureSpecification.Passthrough,
            cancellationToken).ConfigureAwait(false);

        return exitCode;
    }
    catch (OperationCanceledException)
    {
        // Only need to catch this if you're passing cancellationToken to ExecuteAsync (i.e. this
        // exception only fires if you request the process to be cancelled, and not from any UBA
        // internal events).
        return 1;
    }
}
```
