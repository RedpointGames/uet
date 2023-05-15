namespace Redpoint.ProcessExecution
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    internal class DefaultProcessExecutor : IProcessExecutor
    {
        private readonly ILogger<DefaultProcessExecutor> _logger;

        public DefaultProcessExecutor(
            ILogger<DefaultProcessExecutor> logger)
        {
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

        public Task<int> ExecuteAsync(
            ProcessSpecification processSpecification,
            CancellationToken cancellationToken)
        {
            return RunAsync(processSpecification, null, cancellationToken);
        }

        public Task<int> CaptureAsync(
            ProcessSpecification processSpecification,
            CaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            return RunAsync(processSpecification, captureSpecification, cancellationToken);
        }

        private async Task<int> RunAsync(
            ProcessSpecification processSpecification,
            CaptureSpecification? captureSpecification,
            CancellationToken cancellationToken)
        {
            var argumentsEvaluated = processSpecification.Arguments.ToArray();
            var startInfo = new ProcessStartInfo
            {
                FileName = processSpecification.FilePath,
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            if (captureSpecification != null)
            {
                startInfo.RedirectStandardInput = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = captureSpecification.ReceiveStderr != null;
            }
            if (processSpecification.StdinData != null)
            {
                startInfo.RedirectStandardInput = true;
            }
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
            _logger.LogInformation($"Starting process: {EscapeArgumentForLogging(processSpecification.FilePath)} {string.Join(" ", argumentsEvaluated.Select(EscapeArgumentForLogging))}");
            var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Unable to start process!");
            }
            if (processSpecification.StdinData != null)
            {
                process.StandardInput.Write(processSpecification.StdinData);
            }
            if (processSpecification.StdinData != null || captureSpecification != null)
            {
                process.StandardInput.Close();
            }
            if (captureSpecification != null)
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    var line = e?.Data?.TrimEnd();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        if (captureSpecification.ReceiveStdout(line))
                        {
                            System.Console.WriteLine(line);
                        }
                    }
                };
                if (captureSpecification.ReceiveStderr != null)
                {
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        var line = e?.Data?.TrimEnd();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            if (captureSpecification.ReceiveStderr(line))
                            {
                                System.Console.WriteLine(line);
                            }
                        }
                    };
                }
                process.BeginOutputReadLine();
                if (captureSpecification.ReceiveStderr != null)
                {
                    process.BeginErrorReadLine();
                }
            }
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            finally
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch { }
                }
            }
            return process.ExitCode;
        }
    }
}
