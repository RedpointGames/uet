namespace Redpoint.OpenGE.Component.Worker.TaskDescriptorExecutors
{
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Protocol;
    using Redpoint.ProcessExecution.Enumerable;
    using Redpoint.ProcessExecution;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Runtime.CompilerServices;
    using Microsoft.Extensions.Logging;
    using System.Diagnostics;
    using System.Runtime.Versioning;
    using Redpoint.IO;

    internal class RemoteTaskDescriptorExecutor : ITaskDescriptorExecutor<RemoteTaskDescriptor>
    {
        private readonly ILogger<RemoteTaskDescriptorExecutor> _logger;
        private readonly IReservationManagerForOpenGE _reservationManagerForOpenGE;
        private readonly IToolManager _toolManager;
        private readonly IBlobManager _blobManager;
        private readonly IProcessExecutor _processExecutor;

        public RemoteTaskDescriptorExecutor(
            ILogger<RemoteTaskDescriptorExecutor> logger,
            IReservationManagerForOpenGE reservationManagerForOpenGE,
            IToolManager toolManager,
            IBlobManager blobManager,
            IProcessExecutor processExecutor)
        {
            _logger = logger;
            _reservationManagerForOpenGE = reservationManagerForOpenGE;
            _toolManager = toolManager;
            _blobManager = blobManager;
            _processExecutor = processExecutor;
        }

        private void RecreateDirectory(string path)
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                // Try and remove "Read Only" flags on files and directories.
                foreach (var entry in Directory.GetFileSystemEntries(
                    path,
                    "*",
                    new EnumerationOptions
                    {
                        AttributesToSkip = FileAttributes.System,
                        RecurseSubdirectories = true
                    }))
                {
                    var attrs = File.GetAttributes(entry);
                    if ((attrs & FileAttributes.ReadOnly) != 0)
                    {
                        attrs ^= FileAttributes.ReadOnly;
                        File.SetAttributes(entry, attrs);
                    }
                }

                // Now try to delete again.
                Directory.Delete(path, true);
            }
            Directory.CreateDirectory(path);
        }

        public IAsyncEnumerable<ExecuteTaskResponse> ExecuteAsync(
            RemoteTaskDescriptor descriptor,
            CancellationToken cancellationToken)
        {
            if (descriptor.UseFastLocalExecution && OperatingSystem.IsWindows())
            {
                return ExecuteLocalAsync(descriptor, cancellationToken);
            }
            else
            {
                return ExecuteRemoteAsync(descriptor, cancellationToken);
            }
        }

        [SupportedOSPlatform("windows")]
        private async IAsyncEnumerable<ExecuteTaskResponse> ExecuteLocalAsync(
            RemoteTaskDescriptor descriptor,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Set up the environment variable dictionary.
            Dictionary<string, string>? environmentVariables = null;
            if (descriptor.EnvironmentVariables.Count > 0)
            {
                environmentVariables = new Dictionary<string, string>();
                foreach (var kv in descriptor.EnvironmentVariables)
                {
                    environmentVariables[kv.Key] = kv.Value;
                }
            }

            // Set up the process specification.
            var processSpecification = new ProcessSpecification
            {
                FilePath = descriptor.ToolLocalAbsolutePath,
                Arguments = descriptor.Arguments,
                EnvironmentVariables = environmentVariables,
                WorkingDirectory = descriptor.WorkingDirectoryAbsolutePath,
            };

            // Execute the process in the virtual root.
            _logger.LogInformation($"File path: {descriptor.ToolLocalAbsolutePath}");
            _logger.LogInformation($"Arguments: {string.Join(" ", descriptor.Arguments)}");
            _logger.LogInformation($"Working directory: {descriptor.WorkingDirectoryAbsolutePath}");
            await foreach (var response in _processExecutor.ExecuteAsync(
                processSpecification,
                cancellationToken))
            {
                switch (response)
                {
                    case ExitCodeResponse exitCode:
                        yield return new ExecuteTaskResponse
                        {
                            Response = new Protocol.ProcessResponse
                            {
                                ExitCode = exitCode.ExitCode,
                            },
                        };
                        break;
                    case StandardOutputResponse standardOutput:
                        _logger.LogInformation(standardOutput.Data);
                        yield return new ExecuteTaskResponse
                        {
                            Response = new Protocol.ProcessResponse
                            {
                                StandardOutputLine = standardOutput.Data,
                            },
                        };
                        break;
                    case StandardErrorResponse standardError:
                        _logger.LogInformation(standardError.Data);
                        yield return new ExecuteTaskResponse
                        {
                            Response = new Protocol.ProcessResponse
                            {
                                StandardOutputLine = standardError.Data,
                            },
                        };
                        break;
                    default:
                        throw new InvalidOperationException("Received unexpected ProcessResponse type from IProcessExecutor!");
                };
            }
        }

        private async IAsyncEnumerable<ExecuteTaskResponse> ExecuteRemoteAsync(
            RemoteTaskDescriptor descriptor,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
            {
                throw new InvalidOperationException("Remoting tasks can't run on non-Windows platforms yet.");
            }

            // Get a workspace to work in.
            await using (var reservation = await _reservationManagerForOpenGE.ReservationManager.ReserveAsync(
                "OpenGEBuild", 
                descriptor.ToolExecutionInfo.ToolExecutableName))
            {
                // Ask the tool manager where our tool is located.
                var st = Stopwatch.StartNew();
                var toolPath = await _toolManager.GetToolPathAsync(
                    descriptor.ToolExecutionInfo.ToolXxHash64,
                    descriptor.ToolExecutionInfo.ToolExecutableName,
                    cancellationToken);
                _logger.LogInformation($"Tool path obtained in: {st.Elapsed}");
                st.Restart();

                // Ask the blob manager to lay out all of the files in the reservation
                // based on the input files.
                await _blobManager.LayoutBuildDirectoryAsync(
                    reservation.ReservedPath,
                    descriptor.InputsByBlobXxHash64,
                    cancellationToken);
                _logger.LogInformation($"Laid out build directory in: {st.Elapsed}");
                st.Restart();

                // Inside our reservation directory, we need to create a junction
                // for the folder that our tool is in.
                var toolFolder = Path.GetDirectoryName(toolPath)!;
                Junction.CreateJunction(
                    _blobManager.ConvertAbsolutePathToBuildDirectoryPath(reservation.ReservedPath, toolFolder),
                    toolFolder,
                    true);

                // We also need to map SYSTEMROOT, since that is where our Windows
                // folder is.
                var systemRoot = Environment.GetEnvironmentVariable("SYSTEMROOT")!;
                if (Directory.Exists(systemRoot))
                {
                    Junction.CreateJunction(
                        _blobManager.ConvertAbsolutePathToBuildDirectoryPath(reservation.ReservedPath, systemRoot),
                        systemRoot,
                        true);
                }

                // Set up the environment variable dictionary.
                Dictionary<string, string>? environmentVariables = null;
                if (descriptor.EnvironmentVariables.Count > 0)
                {
                    environmentVariables = new Dictionary<string, string>();
                    foreach (var kv in descriptor.EnvironmentVariables)
                    {
                        environmentVariables[kv.Key] = kv.Value;
                    }
                }

                // Set up the process specification.
                var processSpecification = new ProcessSpecification
                {
                    FilePath = toolPath,
                    Arguments = descriptor.Arguments,
                    EnvironmentVariables = environmentVariables,
                    WorkingDirectory = descriptor.WorkingDirectoryAbsolutePath,
                };
                if (OperatingSystem.IsWindows())
                {
                    processSpecification.PerProcessDriveMappings = DriveInfo
                        .GetDrives()
                        .Where(x => Path.Exists(Path.Combine(reservation.ReservedPath, x.Name[0].ToString())))
                        .ToDictionary(
                            k => k.Name[0],
                            v => Path.Combine(reservation.ReservedPath, v.Name[0].ToString()));
                }

                // Execute the process in the virtual root.
                _logger.LogInformation($"File path: {toolPath}");
                _logger.LogInformation($"Arguments: {string.Join(" ", descriptor.Arguments)}");
                _logger.LogInformation($"Working directory: {descriptor.WorkingDirectoryAbsolutePath}");
                await foreach (var response in _processExecutor.ExecuteAsync(
                    processSpecification,
                    cancellationToken))
                {
                    switch (response)
                    {
                        case ExitCodeResponse exitCode:
                            var outputBlobs = await _blobManager.CaptureOutputBlobsFromBuildDirectoryAsync(
                                reservation.ReservedPath,
                                descriptor.OutputAbsolutePaths,
                                cancellationToken);
                            yield return new ExecuteTaskResponse
                            {
                                Response = new Protocol.ProcessResponse
                                {
                                    ExitCode = exitCode.ExitCode,
                                },
                                OutputAbsolutePathsToBlobXxHash64 = outputBlobs,
                            };
                            break;
                        case StandardOutputResponse standardOutput:
                            _logger.LogInformation(standardOutput.Data);
                            yield return new ExecuteTaskResponse
                            {
                                Response = new Protocol.ProcessResponse
                                {
                                    StandardOutputLine = standardOutput.Data,
                                },
                            };
                            break;
                        case StandardErrorResponse standardError:
                            _logger.LogInformation(standardError.Data);
                            yield return new ExecuteTaskResponse
                            {
                                Response = new Protocol.ProcessResponse
                                {
                                    StandardOutputLine = standardError.Data,
                                },
                            };
                            break;
                        default:
                            throw new InvalidOperationException("Received unexpected ProcessResponse type from IProcessExecutor!");
                    };
                }
            }
        }
    }
}
