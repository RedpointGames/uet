namespace UET.Commands.TestPackaged
{
    using Grpc.Core.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.MSBuildResolution;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.UET.BuildPipeline.Executors.Local;
    using Redpoint.UET.BuildPipeline;
    using Redpoint.UET.Core;
    using Redpoint.UET.UAT;
    using Redpoint.UET.Workspace;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Redpoint.UET.BuildPipeline.Executors;
    using Redpoint.UET.BuildPipeline.BuildGraph;
    using UET.Commands;
    using System.Net;
    using System.Net.Sockets;
    using System.Diagnostics;

    internal class TestPackagedCommand
    {
        internal class Options
        {
            public Option<DirectoryInfo> EngineRoot = new Option<DirectoryInfo>(
                "--engine",
                "The directory path to the engine.");

            public Option<DirectoryInfo> ProjectRoot = new Option<DirectoryInfo>(
                "--project",
                "The directory path that contains the .uproject file.");

            public Option<string> TestPrefix = new Option<string>(
                "--test-prefix",
                "The prefix for tests to run.");

            public Option<bool> SkipBuild = new Option<bool>(
                "--skip-build",
                "Skip the build and packaging step.");
        }

        public static Command CreateTestPackagedCommand()
        {
            var options = new Options();
            var command = new Command("test-packaged", "Builds and packages a Win64 project, then runs automation tests against it.");
            command.AddAllOptions(options);
            command.AddCommonHandler<TestPackagedCommandInstance>(options);
            return command;
        }

        private class TestPackagedCommandInstance : ICommandInstance
        {
            private readonly ILogger<TestPackagedCommandInstance> _logger;
            private readonly LocalBuildExecutorFactory _factory;
            private readonly Options _options;
            private readonly IProcessExecutor _processExecutor;
            private readonly IAutomationRunner _automationRunner;

            public TestPackagedCommandInstance(
                ILogger<TestPackagedCommandInstance> logger,
                LocalBuildExecutorFactory factory,
                Options options,
                IProcessExecutor processExecutor,
                IAutomationRunner automationRunner)
            {
                _logger = logger;
                _factory = factory;
                _options = options;
                _processExecutor = processExecutor;
                _automationRunner = automationRunner;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var executor = _factory.CreateExecutor();

                var engineSpec = BuildEngineSpecification.ForAbsolutePath(context.ParseResult.GetValueForOption(_options.EngineRoot)!.FullName);
                var projectPath = context.ParseResult.GetValueForOption(_options.ProjectRoot)!;

                var editorTarget = new DirectoryInfo(Path.Combine(projectPath.FullName, "Source")).GetFiles("*Editor.Target.cs").Select(x => x.Name.Substring(0, x.Name.LastIndexOf(".Target.cs"))).First();
                var gameTarget = editorTarget.Substring(0, editorTarget.LastIndexOf("Editor"));
                var projectName = projectPath!.GetFiles("*.uproject").First().Name;

                Directory.CreateDirectory(Path.Combine(projectPath!.FullName, "Saved", "SharedStorage"));

                if (!context.ParseResult.GetValueForOption(_options.SkipBuild))
                {
                    var buildSpec = new BuildSpecification
                    {
                        Engine = engineSpec,
                        BuildGraphScript = BuildGraphScriptSpecification.ForProject(),
                        BuildGraphTarget = "End",
                        BuildGraphSettings = new Redpoint.UET.BuildPipeline.Environment.BuildGraphSettings
                        {
                            { $"BuildScriptsPath", $"__REPOSITORY_ROOT__/BuildScripts" },
                            { $"BuildScriptsLibPath", $"__REPOSITORY_ROOT__/BuildScripts/Lib" },
                            { $"TempPath", $"__REPOSITORY_ROOT__/BuildScripts/Temp" },
                            { $"ProjectRoot", $"__REPOSITORY_ROOT__" },
                            { $"RepositoryRoot", $"__REPOSITORY_ROOT__" },
                            { $"UProjectPath", $"__REPOSITORY_ROOT__/__PROJECT_FILENAME__" },
                            { $"Distribution", $"Default" },
                            { $"PrepareCustomCompileScripts", $"" },
                            { "IsUnrealEngine5", "true" },
                            { $"ExecuteBuild", $"true" },
                            { $"EditorTarget", editorTarget },
                            { $"GameTargets", gameTarget },
                            { $"ClientTargets", $"" },
                            { $"ServerTargets", $"" },
                            { $"GameTargetPlatforms", $"Win64" },
                            { $"ClientTargetPlatforms", $"" },
                            { $"ServerTargetPlatforms", $"" },
                            { $"GameConfigurations", $"DebugGame" },
                            { $"ClientConfigurations", $"" },
                            { $"ServerConfigurations", $"" },
                            { $"MacPlatforms", $"IOS;Mac" },
                            { $"StrictIncludes", $"false" },
                            { $"StageDirectory", $"__REPOSITORY_ROOT__/Saved/StagedBuilds" },
                            { $"ExecuteTests", $"false" },
                            { $"GauntletTests", $"" },
                            { $"CustomTests", $"" },
                            { $"DeploymentSteam", $"" },
                            { $"DeploymentCustom", $"" },
                        },
                        BuildGraphEnvironment = new Redpoint.UET.BuildPipeline.Environment.BuildGraphEnvironment
                        {
                            PipelineId = string.Empty,
                            Windows = new Redpoint.UET.BuildPipeline.Environment.BuildGraphWindowsEnvironment
                            {
                                SharedStorageAbsolutePath = $"{Path.Combine(projectPath.FullName, "Saved", "SharedStorage").TrimEnd('\\')}\\",
                            },
                            UseStorageVirtualisation = false,
                        },
                        BuildGraphRepositoryRoot = projectPath.FullName,
                        BuildGraphSettingReplacements = new Dictionary<string, string>
                        {
                            { "__PROJECT_FILENAME__", projectName },
                        },
                    };

                    var buildResult = await executor.ExecuteBuildAsync(
                        buildSpec,
                        new LoggerBasedBuildExecutionEvents(_logger),
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
                    if (buildResult != 0)
                    {
                        return buildResult;
                    }
                }

                var packagedWorkingPath = Path.Combine(projectPath.FullName, "Saved", "StagedBuilds", "Windows");
                var packagedExecutablePath = Path.Combine(projectPath.FullName, "Saved", "StagedBuilds", "Windows", $"{Path.GetFileNameWithoutExtension(projectName)}.exe");
                var logPath = Path.Combine(projectPath.FullName, "Saved", "StagedBuilds", "Windows", "Log.txt");

                if (!File.Exists(packagedExecutablePath))
                {
                    _logger.LogError($"Expected packaged executable is missing: {packagedExecutablePath}");
                    return 1;
                }

                _logger.LogInformation($"Starting packaged executable to run automation tests: {packagedExecutablePath}");

                var cancellableAutomationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    context.GetCancellationToken());
                var automationTask = Task.Run(async () => await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = packagedExecutablePath,
                        Arguments = new[]
                        {
                            "-stdout",
                            $"-abslog={logPath}",
                            "-AllowStdOutLogVerbosity",
                            "-Windowed",
                            "-Messaging",
                            "-CrashForUAT",
                            "-SessionName=PackagedAutomation",
                            $"-SessionId={Guid.NewGuid().ToString().Replace("-", "").Replace("{", "").Replace("}", "").ToUpperInvariant()}",
                            $"-SessionOwner={Environment.UserName}",
                            "-EnablePlugins=TcpMessaging",
                            "-DisablePlugins=UdpMessaging",
                            "-ini:Engine:[/Script/TcpMessaging.TcpMessagingSettings]:EnableTransport=True",
                            "-ini:Engine:[/Script/TcpMessaging.TcpMessagingSettings]:ListenEndpoint=127.0.0.1:6666",
                            "-ini:Engine:[/Script/UdpMessaging.UdpMessagingSettings]:EnabledByDefault=False",
                            "-ini:Engine:[/Script/UdpMessaging.UdpMessagingSettings]:nEnableTransport=False",
                        },
                        WorkingDirectory = packagedWorkingPath,
                    },
                    CaptureSpecification.Passthrough,
                    cancellableAutomationTokenSource.Token));
                try
                {
                    var needsReconnect = false;
                    do
                    {
                        if (automationTask.IsCanceled ||
                            automationTask.IsFaulted ||
                            automationTask.IsCompleted)
                        {
                            _logger.LogError($"Packaged executable exited unexpectedly with exit code {automationTask.Result}");
                            return 1;
                        }

                        needsReconnect = false;
                        try
                        {
                            var result = await _automationRunner.RunTestsAsync(
                                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6666),
                                context.ParseResult.GetValueForOption(_options.TestPrefix)!,
                                projectName,
                                context.GetCancellationToken());
                            if (result == 0)
                            {
                                _logger.LogInformation("All tests passed successfully.");
                            }
                            else
                            {
                                _logger.LogError("One or more automation tests failed.");
                            }
                        }
                        catch (SocketException ex) when (ex.ErrorCode == 10061)
                        {
                            _logger.LogInformation("Still waiting to be able to connect...");
                            needsReconnect = true;
                        }
                        catch (IOException ex) when (ex.Message.Contains("An existing connection was forcibly closed by the remote host."))
                        {
                            _logger.LogError("Connection was unexpectedly disconnected.");
                            break;
                        }
                    }
                    while (needsReconnect);
                }
                finally
                {
                    cancellableAutomationTokenSource.Cancel();

                    // Some packaged games use wrappers, and even when we kill the process tree in response
                    // to the cancellation token via the process executor, the game binary doesn't exist.
                    // So we now search for any processes that are running binaries that exist inside the
                    // staged area and kill them as well.
                    foreach (var process in Process.GetProcesses())
                    {
                        try
                        {
                            if (process.MainModule != null &&
                                process.MainModule.FileName.StartsWith(packagedWorkingPath, StringComparison.InvariantCultureIgnoreCase))
                            {
                                try
                                {
                                    process.Kill();
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }

                    var foundCrash = false;
                    foreach (var line in await File.ReadAllLinesAsync(logPath))
                    {
                        if (line.Contains("=== Critical error: ==="))
                        {
                            foundCrash = true;
                        }
                        if (foundCrash)
                        {
                            _logger.LogError(line.Substring(line.IndexOf("Error:") + "Error:".Length).Trim());
                        }
                        if (line.Contains("end: stack for UAT"))
                        {
                            foundCrash = false;
                        }
                    }
                }

                return 1;
            }
        }
    }
}
