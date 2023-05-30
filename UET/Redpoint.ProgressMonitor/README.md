# Redpoint.ProgressMonitor

This library provides APIs for monitoring and reporting the progress of arbitrary operations in console applications.

## Example

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
	var monitorTask = Task.Run(async =>
	{
		var consoleWidth = 0;
		try
		{
			consoleWidth = Console.BufferWidth;
		}
		catch
		{
			// Not connected to a console, e.g. output is
			// redirected.
		}

		var monitor = _monitorFactory.CreateByteBasedMonitor();
		await monitor.MonitorAsync(
			progress,
            null,
            (message, count) =>
			{
				if (consoleWidth != 0)
				{
					// Emit the progress information in such a
					// way that we overwrite the previous info
					// reported to the console.
					Console.Write($"\r{message}".PadRight(consoleWidth));
				}
				else
				{
					// Emit onto a new line every 5 seconds. This
					// callback is invoked every 100ms.
					if (count % 50 == 0)
					{
						Console.WriteLine(message);
					}
				}
			},
            cts.Token);
	});

	// e.g. hash the stream.
	byte[] hashBytes;
	using (var hasher = SHA256.Create())
	{
		hashBytes = await hasher.ComputeHashAsync(stream);
	}

	// Stop monitoring.
	cts.Cancel();
	try
	{
		await monitorTask;
	}
	catch (TaskCanceledException) { }

	// Emit a newline after our progress message.
	if (consoleWidth != 0)
	{
		Console.WriteLine();
	}
}
```