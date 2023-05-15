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

        public async Task<int> ExecuteAsync(
            ProcessSpecification processSpecification,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
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
