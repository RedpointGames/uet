namespace UET.Commands.Internal.VerifyDllFileIntegrity
{
    using k8s.KubeConfigModels;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.Database;
    using Redpoint.Uet.Database.Models;
    using System;
    using System.Collections.Concurrent;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal sealed class VerifyDllFileIntegrityCommand
    {
        internal sealed class Options
        {
            public Option<FileInfo> FileList;
            public Option<DirectoryInfo> Folder;

            public Options()
            {
                FileList = new Option<FileInfo>("--file-list");
                Folder = new Option<DirectoryInfo>("--folder");
            }
        }

        public static Command CreateVerifyDllFileIntegrityCommand()
        {
            var options = new Options();
            var command = new Command("verify-dll-file-integrity");
            command.AddAllOptions(options);
            command.AddCommonHandler<VerifyDllFileIntegrityCommandInstance>(options);
            return command;
        }

        private sealed class VerifyDllFileIntegrityCommandInstance : ICommandInstance
        {
            private readonly IProcessExecutor _processExecutor;
            private readonly IUetDbConnectionFactory _uetDbConnectionFactory;
            private readonly ILogger<VerifyDllFileIntegrityCommandInstance> _logger;
            private readonly Options _options;

            public VerifyDllFileIntegrityCommandInstance(
                IProcessExecutor processExecutor,
                IUetDbConnectionFactory uetDbConnectionFactory,
                ILogger<VerifyDllFileIntegrityCommandInstance> logger,
                Options options)
            {
                _processExecutor = processExecutor;
                _uetDbConnectionFactory = uetDbConnectionFactory;
                _logger = logger;
                _options = options;
            }

            private static bool IsInvalid(string stdout)
            {
                return
                    stdout.Contains("File Type: COFF OBJECT", StringComparison.OrdinalIgnoreCase) ||
                    stdout.Contains("LNK1106: invalid file", StringComparison.OrdinalIgnoreCase) ||
                    stdout.Contains("LNK1107: invalid", StringComparison.OrdinalIgnoreCase);
            }

            private async Task<int> VerifyFileListAsync(
                IUetDbConnection dbConnection,
                string dumpbinPath,
                List<FileInfo> fileList,
                bool shouldDeleteInvalidDllFiles,
                CancellationToken cancellationToken)
            {
                _logger.LogInformation($"{fileList.Count} files requested for verification.");

                // Figure out what files we need to verify, using the cache to skip already verified files.
                var fileListToVerify = new List<FileInfo>();
                foreach (var file in fileList)
                {
                    var verifiedDllFile = await dbConnection.FindAsync<VerifiedDllFileModel>(
                        file.FullName.ToLowerInvariant(),
                        cancellationToken);
                    if (verifiedDllFile != null &&
                        verifiedDllFile.LastWriteTime != null &&
                        verifiedDllFile.LastWriteTime == new DateTimeOffset(file.LastWriteTimeUtc).ToUnixTimeSeconds())
                    {
                        // This file is already verified.
                        continue;
                    }

                    fileListToVerify.Add(file);
                }

                // Verify files that need verifying, and track which files verify successfully.
                _logger.LogInformation($"{fileListToVerify.Count} are not already verified in cache.");
                var filesSuccessfullyVerified = new ConcurrentBag<(FileInfo file, DateTimeOffset dateTimeOffsetBeforeVerify)>();
                var invalidFiles = new ConcurrentBag<FileInfo>();
                await Parallel.ForEachAsync(
                    fileListToVerify.ToAsyncEnumerable(),
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 8,
                        CancellationToken = cancellationToken,
                    },
                    async (file, cancellationToken) =>
                    {
                        // Track date time offset before verify so that if something writes after verifying before caching, we don't
                        // cache something as valid that might be invalid.
                        var dateTimeOffsetBeforeVerify = new DateTimeOffset(file.LastWriteTimeUtc);

                        var stdoutBuilder = new StringBuilder();
                        await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = dumpbinPath,
                                Arguments = [file.FullName, "/SYMBOLS"],
                            },
                            CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(stdoutBuilder),
                            cancellationToken);
                        if (IsInvalid(stdoutBuilder.ToString()))
                        {
                            _logger.LogError($"Detected invalid .dll file: {file.FullName}");
                            if (shouldDeleteInvalidDllFiles)
                            {
                                File.Delete(file.FullName);
                            }
                            invalidFiles.Add(file);
                        }
                        else
                        {
                            filesSuccessfullyVerified.Add((file, dateTimeOffsetBeforeVerify));
                        }
                    });

                // For all the successfully verified files, add them to the cache.
                _logger.LogInformation($"{filesSuccessfullyVerified.Count} successfully verified.");
                _logger.LogInformation($"{invalidFiles.Count} files were invalid.");
                foreach (var file in filesSuccessfullyVerified)
                {
                    await dbConnection.UpsertAsync(
                        new VerifiedDllFileModel
                        {
                            Key = file.file.FullName.ToLowerInvariant(),
                            LastWriteTime = file.dateTimeOffsetBeforeVerify.ToUnixTimeSeconds()
                        },
                        cancellationToken);
                }

                // Return how many invalid files there were.
                return invalidFiles.Count;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                if (!OperatingSystem.IsWindows())
                {
                    _logger.LogWarning("This command is only supported on Windows.");
                    return 0;
                }

                await using var dbConnection = await _uetDbConnectionFactory.ConnectToDefaultDatabaseAsync(context.GetCancellationToken());

                var folder = context.ParseResult.GetValueForOption(_options.Folder);
                var fileList = context.ParseResult.GetValueForOption(_options.FileList);

                var ueSdksRoot = Environment.GetEnvironmentVariable("UE_SDKS_ROOT");
                if (string.IsNullOrWhiteSpace(ueSdksRoot))
                {
                    _logger.LogError("UE_SDKS_ROOT environment variable is not set.");
                    return 1;
                }

                var envsJson = Path.Combine(ueSdksRoot, "HostWin64", "Win64", "envs.json");
                if (!File.Exists(envsJson))
                {
                    _logger.LogError($"Expected file to exist at '{envsJson}'.");
                    return 1;
                }

                var envs = JsonSerializer.Deserialize(File.ReadAllText(envsJson), EnvsJsonSerializerContext.Default.DictionaryStringString);
                if (!(envs?.TryGetValue("VCToolsInstallDir", out var vcToolsInstallDir) ?? false))
                {
                    _logger.LogError($"Expected envs.json to contain 'VCToolsInstallDir' key.");
                    return 1;
                }
                vcToolsInstallDir = vcToolsInstallDir.Replace(
                    "<root>",
                    Path.Combine(ueSdksRoot, "HostWin64", "Win64"),
                    StringComparison.OrdinalIgnoreCase);
                if (!Directory.Exists(vcToolsInstallDir))
                {
                    _logger.LogError($"Expected VCToolsInstallDir to exist at '{vcToolsInstallDir}'.");
                    return 1;
                }

                var dumpbinPath = Path.Combine(vcToolsInstallDir, "bin", "Hostx64", "x64", "dumpbin.exe");
                if (!File.Exists(dumpbinPath))
                {
                    _logger.LogError($"Expected dumpbin.exe to exist at '{dumpbinPath}'.");
                    return 1;
                }

                var shouldDeleteInvalidDllFiles = Environment.GetEnvironmentVariable("UET_DELETE_INVALID_DLL_FILES") == "1";

                var exitCode = 0;

                if (folder != null)
                {
                    _logger.LogInformation($"Verifying that .dll files in '{folder.FullName}' are valid...");

                    await VerifyFileListAsync(
                        dbConnection,
                        dumpbinPath,
                        folder.GetFiles("*.dll").ToList(),
                        shouldDeleteInvalidDllFiles,
                        context.GetCancellationToken());

                    // This does not set the exit code to 1, because it's running before the Compile operation and thus the Compile
                    // operation should cause files to be relinked as necessary.
                }

                if (fileList != null)
                {
                    _logger.LogInformation($"Verifying that .dll files listed in '{fileList.FullName}' are valid...");

                    var files = await File.ReadAllLinesAsync(fileList.FullName, context.GetCancellationToken());
                    var dllFiles = files
                        .Where(x => x.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        .Select(x => new FileInfo(x))
                        .ToList();

                    var invalidFileCount = await VerifyFileListAsync(
                        dbConnection,
                        dumpbinPath,
                        dllFiles,
                        shouldDeleteInvalidDllFiles,
                        context.GetCancellationToken());

                    if (invalidFileCount > 0)
                    {
                        exitCode = 1;
                    }
                }

                if (exitCode != 0 && shouldDeleteInvalidDllFiles)
                {
                    _logger.LogError("BUILD MUST RESTART DUE TO INVALID DLL FILE.");
                }

                return exitCode;
            }
        }
    }
}
