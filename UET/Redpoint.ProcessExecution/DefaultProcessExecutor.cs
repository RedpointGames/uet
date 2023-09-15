namespace Redpoint.ProcessExecution
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution.Enumerable;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    internal class DefaultProcessExecutor : IProcessExecutor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DefaultProcessExecutor> _logger;

        public DefaultProcessExecutor(
            IServiceProvider serviceProvider,
            ILogger<DefaultProcessExecutor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        private string EscapeArgumentForLogging(string argument)
        {
            if (!argument.Contains(' ', StringComparison.Ordinal))
            {
                return argument;
            }
            return $"\"{argument.Replace("\\", "\\\\", StringComparison.Ordinal)}\"";
        }

        public async Task<int> ExecuteAsync(
            ProcessSpecification processSpecification,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            var executionId = Guid.NewGuid().ToString();
            var enableTracing = _logger.IsEnabled(LogLevel.Trace);

            var disposables = new List<IAsyncDisposable>();
            foreach (var hook in _serviceProvider.GetServices<IProcessExecutorHook>())
            {
                if (enableTracing)
                {
                    _logger.LogTrace($"{executionId}: Calling ModifyProcessSpecificationWithCleanupAsync on {hook.GetType().FullName}...");
                }
                var disposable = await hook.ModifyProcessSpecificationWithCleanupAsync(processSpecification, cancellationToken).ConfigureAwait(false);
                if (disposable != null)
                {
                    disposables.Add(disposable);
                }
                if (enableTracing)
                {
                    _logger.LogTrace($"{executionId}: Called ModifyProcessSpecificationWithCleanupAsync on {hook.GetType().FullName}.");
                }
            }

            // @note: Used during clean up to ensure standard output/error tasks don't stall the parent
            // process after the child process exits.
            using var processExitWindDownCancellationTokenSource = new CancellationTokenSource();

            try
            {
                Task? outputReadingTask = null;
                Task? errorReadingTask = null;
                var argumentsEvaluated = processSpecification.Arguments.ToArray();
                var startInfo = new ProcessStartInfo
                {
                    FileName = processSpecification.FilePath,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                };
                startInfo.RedirectStandardInput = captureSpecification.InterceptStandardInput || processSpecification.StdinData != null;
                startInfo.RedirectStandardOutput = captureSpecification.InterceptStandardOutput;
                startInfo.RedirectStandardError = captureSpecification.InterceptStandardError;
                if (processSpecification.WorkingDirectory != null)
                {
                    startInfo.WorkingDirectory = processSpecification.WorkingDirectory;
                }
                foreach (var arg in argumentsEvaluated)
                {
                    startInfo.ArgumentList.Add(arg);
                }
                if (processSpecification.EnvironmentVariables != null)
                {
                    foreach (var kv in processSpecification.EnvironmentVariables)
                    {
                        if (startInfo.EnvironmentVariables.ContainsKey(kv.Key))
                        {
                            startInfo.EnvironmentVariables[kv.Key] = kv.Value;
                        }
                        else
                        {
                            startInfo.EnvironmentVariables.Add(kv.Key, kv.Value);
                        }
                    }
                }
                if (enableTracing)
                {
                    _logger.LogTrace($"{executionId}: Starting process: {EscapeArgumentForLogging(processSpecification.FilePath)} {string.Join(" ", argumentsEvaluated.Select(EscapeArgumentForLogging))}");
                }
                var process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("Unable to start process!");
                }
                if (startInfo.RedirectStandardInput)
                {
                    if (processSpecification.StdinData != null)
                    {
                        process.StandardInput.Write(processSpecification.StdinData);
                    }
                    if (captureSpecification.InterceptStandardInput)
                    {
                        var data = captureSpecification.OnRequestStandardInputAtStartup();
                        if (data != null)
                        {
                            process.StandardInput.Write(data);
                        }
                    }
                    if (enableTracing)
                    {
                        _logger.LogTrace($"{executionId}: Closing standard input stream...");
                    }
                    process.StandardInput.Close();
                    if (enableTracing)
                    {
                        _logger.LogTrace($"{executionId}: Closed standard input stream.");
                    }
                }
                if (startInfo.RedirectStandardOutput)
                {
                    outputReadingTask = Task.Run(async () =>
                    {
                        while (!process.StandardOutput.EndOfStream)
                        {
                            var line = (await process.StandardOutput.ReadLineAsync(processExitWindDownCancellationTokenSource.Token).ConfigureAwait(false))?.TrimEnd();
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                captureSpecification.OnReceiveStandardOutput(line);
                            }
                        }
                    }, cancellationToken);
                }
                if (startInfo.RedirectStandardError)
                {
                    errorReadingTask = Task.Run(async () =>
                    {
                        while (!process.StandardError.EndOfStream)
                        {
                            var line = (await process.StandardError.ReadLineAsync(processExitWindDownCancellationTokenSource.Token).ConfigureAwait(false))?.TrimEnd();
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                captureSpecification.OnReceiveStandardError(line);
                            }
                        }
                    }, cancellationToken);
                }
                try
                {
                    // Use our own semaphore and the Exited event
                    // instead of Process.WaitForExitAsync, since that
                    // function seems to be buggy and can stall.
                    var exitSemaphore = new SemaphoreSlim(0);
                    process.Exited += (sender, args) =>
                    {
                        if (enableTracing)
                        {
                            _logger.LogTrace($"{executionId}: Received 'process.Exited' event; releasing exit semaphore...");
                        }
                        exitSemaphore.Release();
                        if (enableTracing)
                        {
                            _logger.LogTrace($"{executionId}: Received 'process.Exited' event; released exit semaphore.");
                        }
                    };
                    process.EnableRaisingEvents = true;
                    if (process.HasExited)
                    {
                        if (enableTracing)
                        {
                            _logger.LogTrace($"{executionId}: Checked HasExited after EnableRaisingEvents and the process has already exited; releasing exit semaphore...");
                        }
                        exitSemaphore.Release();
                        if (enableTracing)
                        {
                            _logger.LogTrace($"{executionId}: Checked HasExited after EnableRaisingEvents and the process has already exited; released exit semaphore.");
                        }
                    }

                    // Wait for the process to exit or until cancellation.
                    if (enableTracing)
                    {
                        _logger.LogTrace($"{executionId}: Waiting for process exit...");
                    }
                    await exitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    if (enableTracing)
                    {
                        _logger.LogTrace($"{executionId}: Received process exit notification via exit semaphore.");
                    }
                }
                finally
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        if (!process.HasExited)
                        {
                            // @note: If you're still seeing stalls here when Ctrl-C is pressed, it might
                            // be specific to running under the debugger! Make sure you can reproduce
                            // the stall when running from "dotnet run" or a packaged build before
                            // spending time on trying to fix stalls in this code.

                            // @note: There's a weird bug where if we try to terminate the whole
                            // process tree of cl.exe, then the Process.Kill call will stall for
                            // 30 seconds. Workaround this issue by only killing cl.exe itself
                            // if that's what we're running (it won't spawn child processes anyway).
                            if (Path.GetFileNameWithoutExtension(processSpecification.FilePath) == "cl")
                            {
                                if (enableTracing)
                                {
                                    _logger.LogTrace($"{executionId}: Performing non-tree process kill as process has not exited, but cancellation token is cancelled and this process is 'cl.exe'...");
                                }
                                process.Kill();
                                if (enableTracing)
                                {
                                    _logger.LogTrace($"{executionId}: Performed non-tree process kill as process has not exited, but cancellation token is cancelled and this process is 'cl.exe'.");
                                }
                            }
                            else
                            {
                                if (enableTracing)
                                {
                                    _logger.LogTrace($"{executionId}: Performing tree process kill as process has not exited, but cancellation token is cancelled...");
                                }
                                process.Kill(true);
                                if (enableTracing)
                                {
                                    _logger.LogTrace($"{executionId}: Performed tree process kill as process has not exited, but cancellation token is cancelled.");
                                }
                            }
                        }
                    }
                }
                if (!process.HasExited)
                {
                    // Give the process one last chance to exit normally
                    // so we can try to get the exit code.
                    if (enableTracing)
                    {
                        _logger.LogTrace($"{executionId}: Process has still not exited, waiting another second to see if it will move to an exited state so we can retrieve the exit code...");
                    }
                    process.WaitForExit(1000);
                    if (!process.HasExited)
                    {
                        // We can't get the return code for this process.
                        if (enableTracing)
                        {
                            _logger.LogTrace($"{executionId}: Process still hadn't exited after waiting more time. We can't get the exit code, so int.MaxValue will be returned.");
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        return int.MaxValue;
                    }
                }
                processExitWindDownCancellationTokenSource.CancelAfter(1000);
                if (outputReadingTask != null)
                {
                    try
                    {
                        if (enableTracing)
                        {
                            _logger.LogTrace($"{executionId}: Process has exited, awaiting the standard output reading task to completion...");
                        }
                        await outputReadingTask.ConfigureAwait(false);
                        if (enableTracing)
                        {
                            _logger.LogTrace($"{executionId}: Process has exited; the standard output reading task is complete.");
                        }
                    }
                    catch { }
                }
                if (errorReadingTask != null)
                {
                    try
                    {
                        if (enableTracing)
                        {
                            _logger.LogTrace($"{executionId}: Process has exited, awaiting the standard error reading task to completion...");
                        }
                        await errorReadingTask.ConfigureAwait(false);
                        if (enableTracing)
                        {
                            _logger.LogTrace($"{executionId}: Process has exited; the standard error reading task is complete.");
                        }
                    }
                    catch { }
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (enableTracing)
                {
                    _logger.LogTrace($"{executionId}: Process has exited with exit code {process.ExitCode}.");
                }
                return process.ExitCode;
            }
            finally
            {
                // Ensure standard output/error tasks always get cancelled, even if we didn't go down
                // the cleanup path yet.
                processExitWindDownCancellationTokenSource.Cancel();

                foreach (var disposable in disposables)
                {
                    if (enableTracing)
                    {
                        _logger.LogTrace($"{executionId}: Calling DisposeAsync on {disposable.GetType().FullName}...");
                    }
                    await disposable.DisposeAsync().ConfigureAwait(false);
                    if (enableTracing)
                    {
                        _logger.LogTrace($"{executionId}: Called DisposeAsync on {disposable.GetType().FullName}.");
                    }
                }
            }
        }

        public IAsyncEnumerable<ProcessResponse> ExecuteAsync(
            ProcessSpecification processSpecification,
            CancellationToken cancellationToken)
        {
            return new ProcessExecutionEnumerable(
                this,
                processSpecification,
                cancellationToken);
        }
    }
}
