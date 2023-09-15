# Redpoint.Logging.File

This library provides a really simple file logger for Microsoft.Extensions.Logging. Unlike Serilog, this library is trim-compatible.

To create a file logger, pass in a `FileStream` instance to `AddFile`. The file stream will be automatically disposed as needed:

```csharp
services.AddLogging(builder =>
{
    builder.ClearProviders();
	builder.AddFile(new FileStream(
		"path/to/log/file.txt", 
		FileMode.Create, 
		FileAccess.ReadWrite, 
		FileShare.Read | FileShare.Delete));
});
```