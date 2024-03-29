namespace Redpoint.Uet.OpenGE
{
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Protocol;
    using Redpoint.ProcessExecution;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Hashing;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class OpenGEProcessExecutorHook : IProcessExecutorHook
    {
        private readonly ILogger<OpenGEProcessExecutorHook> _logger;
        private readonly IOpenGEProvider _provider;
        private readonly IGrpcPipeFactory _pipeFactory;
        private static SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1);

        public OpenGEProcessExecutorHook(
            ILogger<OpenGEProcessExecutorHook> logger,
            IOpenGEProvider provider,
            IGrpcPipeFactory pipeFactory)
        {
            _logger = logger;
            _provider = provider;
            _pipeFactory = pipeFactory;
        }

        public async Task<IAsyncDisposable?> ModifyProcessSpecificationWithCleanupAsync(
            ProcessSpecification processSpecification,
            CancellationToken cancellationToken)
        {
            if (processSpecification is not OpenGEProcessSpecification openGEProcessSpecification)
            {
                _logger.LogTrace($"Not extracting OpenGE shim for execution of {processSpecification.FilePath} because it is not an OpenGEProcessSpecification.");
                return null;
            }

            var environmentInfo = await _provider.GetOpenGEEnvironmentInfo().ConfigureAwait(false);

            if (!environmentInfo.ShouldUseOpenGE || openGEProcessSpecification.DisableOpenGE)
            {
                _logger.LogTrace($"Not extracting OpenGE shim for execution of {processSpecification.FilePath} because environmentInfo.ShouldUseOpenGE={environmentInfo.ShouldUseOpenGE} or openGEProcessSpecification.DisableOpenGE={openGEProcessSpecification.DisableOpenGE}.");
                return null;
            }

            var shimName = true switch
            {
                var v when v == OperatingSystem.IsWindows() => "xgConsole.exe",
                var v when v == OperatingSystem.IsMacOS() => "xgConsole",
                var v when v == OperatingSystem.IsLinux() => "ib_console",
                _ => throw new PlatformNotSupportedException(),
            };
            var embeddedResourceName = true switch
            {
                var v when v == OperatingSystem.IsWindows() => "win_x64.xgConsole.exe",
                var v when v == OperatingSystem.IsMacOS() => "osx_arm64.xgConsole",
                var v when v == OperatingSystem.IsLinux() => "linux_x64.ib_console",
                _ => throw new PlatformNotSupportedException(),
            };

            var xgeShimFolder = Path.Combine(Path.GetTempPath(), $"openge-shim-{Environment.ProcessId}");
            var xgeShimPath = Path.Combine(xgeShimFolder, shimName);

            var manifestName = $"{typeof(OpenGEProcessExecutorHook).Namespace}.Embedded.{embeddedResourceName}";
            long manifestHash;
            {
                var manifestStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(manifestName);
                if (manifestStream == null)
                {
                    throw new InvalidOperationException($"This process requires the OpenGE shim to be extracted, but UET was incorrectly built and doesn't have a copy of the shim as an embedded resource with the name '{manifestName}'.");
                }
                using (manifestStream)
                {
                    var hasher = new XxHash64();
                    await hasher.AppendAsync(manifestStream, cancellationToken).ConfigureAwait(false);
                    manifestHash = BitConverter.ToInt64(hasher.GetCurrentHash());
                }
            }

            await _semaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var shouldWrite = !File.Exists(xgeShimPath);
                if (shouldWrite)
                {
                    _logger.LogTrace($"Will extract XGE shim as the shim does not exist at '{xgeShimPath}'.");
                }
                else
                {
                    var existingFileHash = await XxHash64Helpers.HashFile(xgeShimPath, cancellationToken).ConfigureAwait(false);
                    shouldWrite = existingFileHash.hash != manifestHash;
                    if (shouldWrite)
                    {
                        _logger.LogTrace($"Will extract XGE shim as the shim at '{xgeShimPath}' does not match the embedded version inside UET (possibly an older shim from a previous UET version).");
                    }
                    else
                    {
                        _logger.LogTrace($"Will not extract XGE shim as the shim already exists at '{xgeShimPath}' as is identical.");
                    }
                }

                if (shouldWrite)
                {
                    Directory.CreateDirectory(xgeShimFolder);
                    var manifestStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(manifestName);
                    using (manifestStream!)
                    {
                        using (var target = new FileStream(xgeShimPath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await manifestStream!.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                    {
                        var mode = File.GetUnixFileMode(xgeShimPath + ".tmp");
                        mode |= UnixFileMode.UserExecute;
                        mode |= UnixFileMode.GroupExecute;
                        mode |= UnixFileMode.OtherExecute;
                        File.SetUnixFileMode(xgeShimPath + ".tmp", mode);
                    }
                    File.Move(xgeShimPath + ".tmp", xgeShimPath, true);
                    _logger.LogTrace("Extracted XGE shim to '" + xgeShimPath + "'.");
                }
                else
                {
                    _logger.LogTrace($"Not extracting OpenGE shim to '" + xgeShimPath + "'.");
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }

            JobApi.JobApiClient upstream;
            if (environmentInfo.UsingSystemWideDaemon)
            {
                upstream = _pipeFactory.CreateClient(
                    "OpenGE",
                    GrpcPipeNamespace.Computer,
                    channel => new JobApi.JobApiClient(channel));
            }
            else
            {
                upstream = _pipeFactory.CreateClient(
                    environmentInfo.PerProcessDispatcherPipeName,
                    GrpcPipeNamespace.User,
                    channel => new JobApi.JobApiClient(channel));
            }

            var logInterceptingPipeName = Guid.NewGuid().ToString();
            var logInterceptingServer = _pipeFactory.CreateServer(
                logInterceptingPipeName,
                GrpcPipeNamespace.User,
                new LogInterceptingDispatcher(_logger, upstream));

            var path = processSpecification.EnvironmentVariables != null && processSpecification.EnvironmentVariables.ContainsKey("PATH")
                ? processSpecification.EnvironmentVariables["PATH"]
                : Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            var pathComponents = path.Split(Path.PathSeparator).ToList();
            pathComponents.Add(xgeShimFolder);

            var newPath = string.Join(Path.PathSeparator, pathComponents);

            var newEnvironmentVariables = processSpecification.EnvironmentVariables != null ? new Dictionary<string, string>(processSpecification.EnvironmentVariables) : new Dictionary<string, string>();
            newEnvironmentVariables["PATH"] = newPath;
            newEnvironmentVariables["UET_FORCE_XGE_SHIM"] = "1";
            newEnvironmentVariables["UET_XGE_SHIM_PIPE_NAME"] = logInterceptingPipeName;
            processSpecification.EnvironmentVariables = newEnvironmentVariables;

            // @note: Handle a special case where UET wants to launch the
            // OpenGE shim directly, rather than running a process that wants
            // to find the OpenGE shim on the PATH.
            if (processSpecification.FilePath == "__openge__")
            {
                processSpecification.FilePath = xgeShimPath;
            }

            await logInterceptingServer.StartAsync().ConfigureAwait(false);
            return new LogInterceptingServerStopper(logInterceptingServer);
        }

        private sealed class LogInterceptingServerStopper : IAsyncDisposable
        {
            private IGrpcPipeServer<LogInterceptingDispatcher> _logInterceptingServer;

            public LogInterceptingServerStopper(
                IGrpcPipeServer<LogInterceptingDispatcher> logInterceptingServer)
            {
                _logInterceptingServer = logInterceptingServer;
            }

            public async ValueTask DisposeAsync()
            {
                await _logInterceptingServer.StopAsync().ConfigureAwait(false);
            }
        }
    }
}
