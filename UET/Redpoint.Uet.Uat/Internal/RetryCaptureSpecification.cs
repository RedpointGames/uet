namespace Redpoint.Uet.Uat.Internal
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using System;
    using System.Text.RegularExpressions;

    internal class RetryCaptureSpecification : ICaptureSpecification
    {
        private readonly ILogger _logger;
        private readonly ICaptureSpecification _baseCaptureSpecification;
        private readonly string[] _forceRetryMessages;
        private bool _isCurrentlySilenced;

        public RetryCaptureSpecification(
            ILogger logger,
            ICaptureSpecification baseCaptureSpecification,
            string[] forceRetryMessages)
        {
            _logger = logger;
            _baseCaptureSpecification = baseCaptureSpecification;
            _forceRetryMessages = forceRetryMessages;
        }

        public bool NeedsRetry { get; private set; } = false;

        public bool ForceRetry { get; private set; } = false;

        public bool NeedsEngineRemount { get; private set; } = false;

        public bool InterceptStandardInput => true;

        public bool InterceptStandardOutput => true;

        public bool InterceptStandardError => true;

        private void CheckDataForRetry(string data)
        {
            if (data.Contains("error C3859", StringComparison.Ordinal))
            {
                // Temporary "PCH out of memory" error that we get from MSVC.
                _logger.LogWarning("This build will be retried due to a 'PCH out of memory' error from MVSC.");
                NeedsRetry = true;
            }
            if (data.Contains("error C1085", StringComparison.Ordinal) &&
                data.Contains("Cannot write precompiled header file", StringComparison.Ordinal))
            {
                // Temporary "Cannot write precompiled header file" error that we get from MSVC.
                _logger.LogWarning("This build will be retried because MSVC failed to write a PCH file.");
                NeedsRetry = true;
            }
            if (data.Contains("error LNK1107", StringComparison.Ordinal))
            {
                // Seems to happen sometimes when using clang-tidy?
                _logger.LogWarning("This build will be retried because a temporary linker error occurred.");
                NeedsRetry = true;
            }
            if (data.Contains("error C1060", StringComparison.Ordinal))
            {
                // Temporary "compiler is out of heap space" error.
                _logger.LogWarning("This build will be retried due to a 'compiler is out of heap space' error from MVSC.");
                NeedsRetry = true;
            }
            if (data.Contains("error LNK1327", StringComparison.Ordinal) ||
                data.Contains("error LNK1171", StringComparison.Ordinal) ||
                data.Contains("error LNK1123", StringComparison.Ordinal))
            {
                // Temporary failures that can happen due to UBA.
                _logger.LogWarning("This build will be retried because a temporary linker error occurred.");
                NeedsRetry = true;
            }
            if (data.Contains("ERROR: MapViewOfFile failed", StringComparison.Ordinal))
            {
                // Temporary failures that can happen due to UBA.
                _logger.LogWarning("This build will be retried because a temporary UBA error occurred.");
                NeedsRetry = true;
            }
            if (data.Contains("LLVM ERROR: out of memory", StringComparison.Ordinal))
            {
                _logger.LogWarning("This build will be retried because a temporary out-of-memory scenario occurred.");
                NeedsRetry = true;
            }
            if (data.Contains("it is being used by another process", StringComparison.Ordinal) &&
                data.Contains("DynamicBuildGraph", StringComparison.Ordinal) &&
                data.Contains(".xml", StringComparison.Ordinal))
            {
                // For some reason BuildGraph wasn't able to read the DynamicBuildGraph
                // file on the network share. This wouldn't occur if BuildGraph were built
                // with .NET 6 or later (which seems to set up the FileShare mode more
                // generously), but .NET 5 uses a FileStream underneath to implement
                // ReadAllBytesAsync and can thus be blocked by another build job reading
                // the DynamicBuildGraph at the exact same moment.
                //
                // Our options for working around this would be either:
                //
                // - Copying the DynamicBuildGraph file locally when ci-build starts up
                //   ourselves to gracefully handle the lock. This would mean manually 
                //   parsing the BuildGraphSettings though, because the dynamic build graph
                //   isn't actually a special field, so it's not ideal.
                //
                // - Just retrying the job as part of the UAT executor. This error 
                //   happens immediately as soon as BuildGraph starts up, so we don't lose
                //   any build time by working around this issue with a retry.
                //
                _logger.LogWarning("This build will be retried because the build temporarily could not access the DynamicBuildGraph files on the network.");
                NeedsRetry = true;
            }
            if (data.Contains("fatal error CVT1107", StringComparison.Ordinal) && data.Contains("is corrupt", StringComparison.Ordinal))
            {
                // fatal error CVT1107: '(file path)' is corrupt
                // Delete the corrupt file and retry.
                var fileRegex = Regex.Match(data.Trim(), "^fatal error CVT1107: '([^']+)' is corrupt$");
                if (fileRegex.Success)
                {
                    var filePath = fileRegex.Groups[1].Value;
                    _logger.LogWarning($"Detected that '{filePath}' is a corrupt PCH or object file. It will be deleted and the build will be retried.");
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        NeedsRetry = true;
                    }
                }
            }
            if (_forceRetryMessages.Length > 0)
            {
                foreach (var retry in _forceRetryMessages)
                {
                    if (data.Contains(retry, StringComparison.Ordinal))
                    {
                        ForceRetry = true;
                    }
                }
            }
            if (data.Trim() == "Stack overflow.")
            {
                // Rare scenario that was encountered when BuildGraph was generating
                // graphs. I'm pretty sure this output was from cmd.exe inside
                // AutomationTool and not BuildGraph, and probably caused by some
                // transient memory issue. In any case, if we see this message on
                // a line by itself, we'll need to restart the command.
                _logger.LogWarning("This build will be retried because BuildGraph encountered a stack overflow.");
                NeedsRetry = true;
            }
            if (data.Contains("fatal error C1356: unable to find mspdbcore.dll", StringComparison.Ordinal))
            {
                // Caused by UBA detours intermittently.
                _logger.LogWarning("This build will be retried due to a temporary error caused by the Unreal Build Accelerator.");
                NeedsRetry = true;
            }
            if (data.Contains("error: ", StringComparison.Ordinal) &&
                data.Contains("/UHT/", StringComparison.Ordinal))
            {
                // Scenario on macOS where UHT files are corrupt and need to be regenerated.
                var fileRegex = Regex.Match(data.Trim(), @"^\s*(?<filename>[^:]+):([0-9]+):([0-9]+):\serror:");
                if (fileRegex.Success)
                {
                    var filePath = fileRegex.Groups["filename"].Value;
                    _logger.LogWarning($"Detected that '{filePath}' is a corrupt UHT generated file. It will be deleted and the build will be retried.");
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        NeedsRetry = true;
                    }
                }
            }
            if (data.Contains("The device is not ready.", StringComparison.Ordinal))
            {
                _logger.LogWarning($"Detected that the UEFS mount for the engine is not ready to serve requests. The engine will be remounted without the existing write scratch data and the build will be retried.");
                NeedsEngineRemount = true;
            }
            if (data.Contains("The request could not be performed because of an I/O device error.", StringComparison.Ordinal))
            {
                _logger.LogWarning($"Detected that the UEFS mount for the engine is not ready to serve requests. The engine will be remounted without the existing write scratch data and the build will be retried.");
                NeedsEngineRemount = true;
            }
            if (data.Contains("error C4821", StringComparison.Ordinal))
            {
                _logger.LogWarning("Detected that one or more files were corrupted. The engine will be remounted without the existing write scratch data and the build will be retried.");
                NeedsEngineRemount = true;
            }
            if (data.Contains("Missing object file", StringComparison.Ordinal) && data.Contains(@"\Engine\", StringComparison.Ordinal))
            {
                _logger.LogWarning("Detected that one or more files were corrupted. The engine will be remounted without the existing write scratch data and the build will be retried.");
                NeedsEngineRemount = true;
            }
            if (data.Contains("The file or directory is corrupted and unreadable.", StringComparison.Ordinal))
            {
                _logger.LogWarning("Detected that one or more files were corrupted. The engine will be remounted without the existing write scratch data and the build will be retried.");
                NeedsEngineRemount = true;
            }
            if (data.Contains("BUILD MUST RESTART DUE TO INVALID DLL FILE", StringComparison.Ordinal))
            {
                _logger.LogWarning("Detected one or more invalid DLL files were deleted due to post-Compile checks. The corrupt files have been deleted and the build will be retried.");
                NeedsRetry = true;
            }
            if (data.Contains("UnrealBuildTool.Env.BuildConfiguration.xml", StringComparison.Ordinal) &&
                data.Contains("used by another process", StringComparison.Ordinal))
            {
                _logger.LogWarning("Detected access conflict on 'UnrealBuildTool.Env.BuildConfiguration.xml'. The build will be retried.");
                NeedsRetry = true;
            }
        }

        public void OnReceiveStandardError(string data)
        {
            if (data.Contains("UET-SILENCE-OUTPUT-ON", StringComparison.Ordinal))
            {
                _isCurrentlySilenced = true;
                return;
            }
            else if (data.Contains("UET-SILENCE-OUTPUT-OFF", StringComparison.Ordinal))
            {
                _isCurrentlySilenced = false;
                return;
            }

            if (_isCurrentlySilenced)
            {
                return;
            }

            CheckDataForRetry(data);

            if (_baseCaptureSpecification.InterceptStandardError)
            {
                _baseCaptureSpecification.OnReceiveStandardError(data);
            }
            else
            {
                Console.WriteLine(data);
            }
        }

        public void OnReceiveStandardOutput(string data)
        {
            if (data.Contains("UET-SILENCE-OUTPUT-ON", StringComparison.Ordinal))
            {
                _isCurrentlySilenced = true;
                return;
            }
            else if (data.Contains("UET-SILENCE-OUTPUT-OFF", StringComparison.Ordinal))
            {
                _isCurrentlySilenced = false;
                return;
            }

            if (_isCurrentlySilenced)
            {
                return;
            }

            CheckDataForRetry(data);

            if (_baseCaptureSpecification.InterceptStandardOutput)
            {
                _baseCaptureSpecification.OnReceiveStandardOutput(data);
            }
            else
            {
                Console.WriteLine(data);
            }
        }

        public string? OnRequestStandardInputAtStartup()
        {
            if (_baseCaptureSpecification.InterceptStandardInput)
            {
                return _baseCaptureSpecification.OnRequestStandardInputAtStartup();
            }

            return null;
        }
    }
}
