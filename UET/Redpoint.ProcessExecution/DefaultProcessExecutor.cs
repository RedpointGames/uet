namespace Redpoint.ProcessExecution
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Diagnostics;
    using System.Linq;
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
            if (!argument.Contains(" "))
            {
                return argument;
            }
            return $"\"{argument.Replace("\\", "\\\\")}\"";
        }

        public async Task<int> ExecuteAsync(
            ProcessSpecification processSpecification,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            foreach (var hook in _serviceProvider.GetServices<IProcessExecutorHook>())
            {
                await hook.ModifyProcessSpecificationAsync(processSpecification, cancellationToken);
            }

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
            _logger.LogTrace($"Starting process: {EscapeArgumentForLogging(processSpecification.FilePath)} {string.Join(" ", argumentsEvaluated.Select(EscapeArgumentForLogging))}");
            var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Unable to start process!");
            }
            if (captureSpecification.InterceptRawStreams)
            {
                captureSpecification.OnReceiveStreams(
                    startInfo.RedirectStandardInput ? process.StandardInput : null,
                    startInfo.RedirectStandardOutput ? process.StandardOutput : null,
                    startInfo.RedirectStandardError ? process.StandardError : null,
                    cancellationToken);
            }
            else
            {
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
                    process.StandardInput.Close();
                }
                if (startInfo.RedirectStandardOutput)
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        var line = e?.Data?.TrimEnd();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            captureSpecification.OnReceiveStandardOutput(line);
                        }
                    };
                    process.BeginOutputReadLine();
                }
                if (startInfo.RedirectStandardError)
                {
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        var line = e?.Data?.TrimEnd();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            captureSpecification.OnReceiveStandardError(line);
                        }
                    };
                    process.BeginErrorReadLine();
                }
            }
            try
            {
                // Use our own semaphore and the Exited event
                // instead of Process.WaitForExitAsync, since that
                // function seems to be buggy and can stall.
                var exitSemaphore = new SemaphoreSlim(0);
                process.Exited += (sender, args) =>
                {
                    exitSemaphore.Release();
                };
                process.EnableRaisingEvents = true;
                if (process.HasExited)
                {
                    exitSemaphore.Release();
                }

                // Wait for the process to exit or until cancellation.
                await exitSemaphore.WaitAsync(cancellationToken);
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
                            process.Kill();
                        }
                        else
                        {
                            process.Kill(true);
                        }
                    }
                }
            }
            if (!process.HasExited)
            {
                // Give the process one last chance to exit normally
                // so we can try to get the exit code.
                process.WaitForExit(1000);
                if (!process.HasExited)
                {
                    // We can't get the return code for this process.
                    return int.MaxValue;
                }
            }
            return process.ExitCode;
        }
    }
}
