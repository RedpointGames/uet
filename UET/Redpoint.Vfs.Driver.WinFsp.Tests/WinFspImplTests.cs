namespace Redpoint.Vfs.Driver.WinFsp.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System.Runtime.Versioning;
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.Layer.Git;
    using Redpoint.Vfs.LocalIo;
    using Redpoint.Vfs.Layer.Scratch;

    public class WinFspImplTests
    {
        [Fact]
        [SupportedOSPlatform("windows6.2")]
        public async Task ReadDirectoryEntriesWorkCorrectlyInBatches()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");
            Skip.IfNot(OperatingSystem.IsWindows(), "This test must be run on Windows");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();
            services.AddWinFspVfsDriver();

            var serviceProvider = services.BuildServiceProvider();

            var gitFactory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();

            var gitLayer = gitFactory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var vfs = new WinFspVfsDriverImpl(
                serviceProvider.GetRequiredService<ILogger<WinFspVfsDriverImpl>>(),
                gitLayer,
                new WinFspVfsDriverOptions
                {
                    EnableCorrectnessChecks = true,
                });

            var result = vfs.Open(
                fileName: @"\Engine\Source\Programs\UnrealBuildTool",
                createOptions: 0,
                grantedAccess: 0,
                fileNodeOut: out var fileNode,
                fileDesc: out var fileDesc,
                fileInfo: out var fileInfo,
                normalizedName: out var normalizedName);
            Assert.Equal(0, result);
            Assert.NotNull(fileNode);
            Assert.Null(fileDesc); // We don't use fileDesc.

            string? marker = null;
            var context = new object();
            var entries = new Dictionary<string, Fsp.Interop.FileInfo>(new FileSystemNameComparer());
            for (int i = 0; i < 30; i++)
            {
                var didAdd = false;
                vfs.ReadDirectoryEntries(
                    fileNode,
                    fileDesc,
                    "*",
                    marker,
                    ref context,
                    (name, fileInfo) =>
                    {
                        Assert.False(entries.ContainsKey(name), $"ReadDirectoryEntries resulted in duplicated entry: {name}");
                        entries.Add(name, fileInfo);
                        marker = name;
                        didAdd = true;
                        return false;
                    });
                if (!didAdd)
                {
                    break;
                }
            }

            var expectedEntries = new (string name, bool isDir)[]
            {
                (".", true),
                ("..", true),
                ("Configuration", true),
                ("Executors", true),
                ("Matchers", true),
                ("Modes", true),
                ("Platform", true),
                ("Preprocessor", true),
                ("ProjectFiles", true),
                ("Properties", true),
                ("Storage", true),
                ("System", true),
                ("ToolChain", true),
                ("app.manifest", false),
                ("runtimeconfig.template.json", false),
                ("UnrealBuildTool.cs", false),
                ("UnrealBuildTool.csproj", false),
                ("UnrealBuildTool.sln", false),
            };
            foreach (var expected in expectedEntries)
            {
                Assert.Contains(expected.name, entries.Keys);
                if (expected.isDir)
                {
                    Assert.True((entries[expected.name].FileAttributes & (uint)FileAttributes.Directory) != 0, $"{expected.name} should be a directory");
                }
                else
                {
                    Assert.True((entries[expected.name].FileAttributes & (uint)FileAttributes.Directory) == 0, $"{expected.name} should be a file");
                }
            }
        }

        [Fact]
        [SupportedOSPlatform("windows6.2")]
        public async Task FileInfosHaveIndexNumber()
        {
            // @note: The IndexNumber attribute on FileInfo is *required* for ISPC to work
            // properly. If it's left as 0, ISPC seems to assume that all include folders
            // are the same folder and doesn't scan them properly.

            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");
            Skip.IfNot(OperatingSystem.IsWindows(), "This test must be run on Windows");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();
            services.AddWinFspVfsDriver();

            var serviceProvider = services.BuildServiceProvider();

            var gitFactory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();

            var gitLayer = gitFactory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var vfs = new WinFspVfsDriverImpl(
                serviceProvider.GetRequiredService<ILogger<WinFspVfsDriverImpl>>(),
                gitLayer,
                new WinFspVfsDriverOptions
                {
                    EnableCorrectnessChecks = true,
                });

            var result = vfs.Open(
                fileName: @"\Engine\Source\Programs\UnrealBuildTool",
                createOptions: 0,
                grantedAccess: 0,
                fileNodeOut: out var fileNode,
                fileDesc: out var fileDesc,
                fileInfo: out var fileInfo,
                normalizedName: out var normalizedName);
            Assert.NotEqual<ulong>(0, fileInfo.IndexNumber);
        }

        [Fact]
        [SupportedOSPlatform("windows6.2")]
        public async Task NormalizedNameIsSet()
        {
            // @note: normalizedName is required for applications to
            // see the correct capitalization of files and directories,
            // but we need to make our implementation performant first
            // (see the notes in WinFspProjectionLayerFileSystem.GetNormalizedName)

            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");
            Skip.IfNot(OperatingSystem.IsWindows(), "This test must be run on Windows");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();
            services.AddWinFspVfsDriver();

            var serviceProvider = services.BuildServiceProvider();

            var gitFactory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();

            var gitLayer = gitFactory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var vfs = new WinFspVfsDriverImpl(
                serviceProvider.GetRequiredService<ILogger<WinFspVfsDriverImpl>>(),
                gitLayer,
                new WinFspVfsDriverOptions
                {
                    EnableCorrectnessChecks = true,
                    EnableNameNormalization = true,
                });

            var result = vfs.Open(
                fileName: @"\Engine\Source\Programs\UnrealBuildTool",
                createOptions: 0,
                grantedAccess: 0,
                fileNodeOut: out var fileNode,
                fileDesc: out var fileDesc,
                fileInfo: out var fileInfo,
                normalizedName: out var normalizedName);
            Assert.NotNull(normalizedName);
        }

        [Fact]
        [SupportedOSPlatform("windows6.2")]
        public async Task PatternMatchingIsNotCaseSensitive()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");
            Skip.IfNot(OperatingSystem.IsWindows(), "This test must be run on Windows");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();
            services.AddWinFspVfsDriver();

            var serviceProvider = services.BuildServiceProvider();

            var gitFactory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();

            var gitLayer = gitFactory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var vfs = new WinFspVfsDriverImpl(
                serviceProvider.GetRequiredService<ILogger<WinFspVfsDriverImpl>>(),
                gitLayer,
                new WinFspVfsDriverOptions
                {
                    EnableCorrectnessChecks = true,
                });

            var result = vfs.Open(
                fileName: @"\Engine\Source\Programs\UnrealBuildTool",
                createOptions: 0,
                grantedAccess: 0,
                fileNodeOut: out var fileNode,
                fileDesc: out var fileDesc,
                fileInfo: out var fileInfo,
                normalizedName: out var normalizedName);
            Assert.Equal(0, result);
            Assert.NotNull(fileNode);
            Assert.Null(fileDesc); // We don't use fileDesc.

            var context = new object();
            string? storedName = null;
            vfs.ReadDirectoryEntries(
                fileNode,
                fileDesc,
                "RUNTIMECONFIG.TEMPLATE.JSON",
                null,
                ref context,
                (name, fileInfo) =>
                {
                    storedName = name;
                    return false;
                });

            Assert.Equal("runtimeconfig.template.json", storedName);
        }

        [Fact]
        [SupportedOSPlatform("windows6.2")]
        public async Task PatternMatchingIsNotCaseSensitiveWithMarker()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");
            Skip.IfNot(OperatingSystem.IsWindows(), "This test must be run on Windows");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();
            services.AddWinFspVfsDriver();

            var serviceProvider = services.BuildServiceProvider();

            var gitFactory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();

            var gitLayer = gitFactory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var vfs = new WinFspVfsDriverImpl(
                serviceProvider.GetRequiredService<ILogger<WinFspVfsDriverImpl>>(),
                gitLayer,
                new WinFspVfsDriverOptions
                {
                    EnableCorrectnessChecks = true,
                });

            var result = vfs.Open(
                fileName: @"\Engine\Source\Programs\UnrealBuildTool",
                createOptions: 0,
                grantedAccess: 0,
                fileNodeOut: out var fileNode,
                fileDesc: out var fileDesc,
                fileInfo: out var fileInfo,
                normalizedName: out var normalizedName);
            Assert.Equal(0, result);
            Assert.NotNull(fileNode);
            Assert.Null(fileDesc); // We don't use fileDesc.

            var context = new object();
            string? storedName = null;
            vfs.ReadDirectoryEntries(
                fileNode,
                fileDesc,
                "RUNTIMECONFIG.TEMPLATE.JSON",
                "Preprocessor",
                ref context,
                (name, fileInfo) =>
                {
                    storedName = name;
                    return false;
                });

            Assert.Equal("runtimeconfig.template.json", storedName);
        }
    }
}