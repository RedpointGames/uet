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

        public async IAsyncEnumerable<ExecuteTaskResponse> ExecuteAsync(
            RemoteTaskDescriptor descriptor,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Get a workspace to work in.
            await using (var reservation = await _reservationManagerForOpenGE.ReservationManager.ReserveAsync(
                "OpenGEBuild", 
                descriptor.ToolExecutionInfo.ToolExecutableName))
            {
                // If this tool requires a clean workspace, delete any files that might be in
                // this reservation already.
                if (descriptor.RequireCleanWorkspace)
                {
                    RecreateDirectory(reservation.ReservedPath);
                }

                // Ask the tool manager where our tool is located.
                var st = Stopwatch.StartNew();
                var toolPath = await _toolManager.GetToolPathAsync(
                    descriptor.ToolExecutionInfo.ToolXxHash64,
                    descriptor.ToolExecutionInfo.ToolExecutableName,
                    cancellationToken);
                _logger.LogInformation($"Tool path obtained in: {st.Elapsed}");
                st.Restart();

                // On Windows, we map the I: drive letter on a per-process level
                // to the reservation root, which makes paths identical on every
                // machine that the process is running on. This is required for
                // PCH files to be portable across machines.
                var virtualDriveLetter = 'I';
                var shortenedReservationPath = OperatingSystem.IsWindows() ? $"{virtualDriveLetter}:" : reservation.ReservedPath;

                // Ask the blob manager to lay out all of the files in the reservation
                // based on the input files.
                await _blobManager.LayoutBuildDirectoryAsync(
                    reservation.ReservedPath,
                    shortenedReservationPath,
                    descriptor.InputsByBlobXxHash64,
                    cancellationToken);
                _logger.LogInformation($"Laid out build directory in: {st.Elapsed}");
                st.Restart();

                // Replace {__OPENGE_VIRTUAL_ROOT__} in the arguments as well.
                var arguments = descriptor
                    .Arguments
                    .Select(x => x.Replace(
                        "{__OPENGE_VIRTUAL_ROOT__}",
                        shortenedReservationPath))
                    .ToArray();

                // Replace {__OPENGE_VIRTUAL_ROOT__} in the environment variables.
                Dictionary<string, string>? environmentVariables = null;
                if (descriptor.EnvironmentVariables.Count > 0)
                {
                    environmentVariables = new Dictionary<string, string>();
                    foreach (var kv in descriptor.EnvironmentVariables)
                    {
                        environmentVariables[kv.Key] = kv.Value.Replace(
                            "{__OPENGE_VIRTUAL_ROOT__}",
                            shortenedReservationPath);
                    }
                }

                // Convert the working directory.
                var workingDirectory = _blobManager.ConvertAbsolutePathToBuildDirectoryPath(
                    shortenedReservationPath,
                    descriptor.WorkingDirectoryAbsolutePath);

                // Set up the process specification.
                var processSpecification = new ProcessSpecification
                {
                    FilePath = toolPath,
                    Arguments = arguments,
                    EnvironmentVariables = environmentVariables,
                    WorkingDirectory = workingDirectory,
                };
                if (OperatingSystem.IsWindows())
                {
                    processSpecification.PerProcessDriveMappings = new Dictionary<char, string>
                    {
                        { virtualDriveLetter, reservation.ReservedPath },
                    };
                }

                // Execute the process in the virtual root.
                _logger.LogInformation($"File path: {toolPath}");
                _logger.LogInformation($"Arguments: {string.Join(" ", arguments)}");
                _logger.LogInformation($"Working directory: {workingDirectory}");
                await foreach (var response in _processExecutor.ExecuteAsync(
                    processSpecification,
                    cancellationToken))
                {
                    switch (response)
                    {
                        case ExitCodeResponse exitCode:
                            var outputBlobs = await _blobManager.CaptureOutputBlobsFromBuildDirectoryAsync(
                                reservation.ReservedPath,
                                shortenedReservationPath,
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
