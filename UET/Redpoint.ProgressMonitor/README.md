# Redpoint.ProgressMonitor

This library provides APIs for monitoring and reporting the progress of arbitrary operations in console applications.

Read on for the following examples:

- [Example for a generic stream](#example-for-a-generic-stream)
- [Example for a HTTP download](#example-for-a-http-download)
- [The SystemConsole static class](#the-systemconsole-static-class)

## Example for a generic stream

You can monitor an operation that uses a stream like so:

```csharp
// Inject these services.
IProgressFactory _progressFactory;
IMonitorFactory _monitorFactory;

using (var stream = new FileStream(...))
{
    // Start monitoring.
    var cts = new CancellationTokenSource();
    var progress = _progressFactory.CreateProgressForStream(stream);
    var monitorTask = Task.Run(async () =>
    {
        var monitor = _monitorFactory.CreateByteBasedMonitor();
        await monitor.MonitorAsync(
            progress,
            SystemConsole.ConsoleInformation,
            SystemConsole.WriteProgressToConsole,
            cts.Token);
    });

    // e.g. hash the stream.
    byte[] hashBytes;
    using (var hasher = SHA256.Create())
    {
        hashBytes = await hasher.ComputeHashAsync(stream);
    }

    // Stop monitoring.
    await SystemConsole.CancelAndWaitForConsoleMonitoringTaskAsync(monitorTask, cts);
}
```

## Example for a HTTP download

If you're reporting progress on a HTTP stream, there's a few extra things to keep in mind:

- You need to pass `HttpCompletionOption.ResponseHeadersRead` as the completion option, or `HttpClient` will buffer the entire response by default.
- You need to wrap the stream you read from in `PositionAwareStream`, which is a class provided by this library. Since the underlying HTTP stream
  does not support `Position` or `Length`, this wrapping stream tracks the position as the stream is read from and allows the length to be passed in
  as a constructor parameter (which you should set based on the Content-Length header).

Below is a concise example of how to show the progress of downloading a file:

```csharp
using (var client = new HttpClient())
{
    using (var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
    {
        var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        using (var stream = new PositionAwareStream(
            await response.Content.ReadAsStreamAsync(),
            response.Content.Headers.ContentLength!.Value))
        {
            var cts = new CancellationTokenSource();
            var progress = _progressFactory.CreateProgressForStream(stream);
            var monitorTask = Task.Run(async () =>
            {
                var monitor = _monitorFactory.CreateByteBasedMonitor();
                await monitor.MonitorAsync(
                    progress,
                    SystemConsole.ConsoleInformation,
                    SystemConsole.WriteProgressToConsole,
                    cts.Token);
            });

            await stream.CopyToAsync(target);
            
            await SystemConsole.CancelAndWaitForConsoleMonitoringTaskAsync(monitorTask, cts);
        }
    }
}
```

## The SystemConsole static class

The `SystemConsole` type provides common values for monitoring parameters, such as the current console information and rendering progress information to the console. You should replace the static values in the examples above with your own callbacks and values if you're not rendering progress to the console.
