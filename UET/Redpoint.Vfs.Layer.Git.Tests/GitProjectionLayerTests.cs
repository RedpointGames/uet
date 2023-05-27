namespace Redpoint.Vfs.Layer.Git.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Vfs.LocalIo;

    public class GitProjectionLayerTests
    {
        [SkippableFact]
        public async Task GitLayerEnumeratesDirectoryEntriesCorrectly()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddLocalIoFileFactory();

            var serviceProvider = services.BuildServiceProvider();

            var factory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();

            var gitLayer = factory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var entries = gitLayer.List(@"Engine\Source\Programs\UnrealBuildTool");
            Assert.NotNull(entries);

            var entriesList = entries.ToList();
            Assert.Contains(entriesList, x => x.Name == "Configuration" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Executors" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Matchers" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Modes" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Platform" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Preprocessor" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "ProjectFiles" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Properties" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Storage" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "System" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "ToolChain" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "app.manifest" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "runtimeconfig.template.json" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "UnrealBuildTool.cs" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "UnrealBuildTool.csproj" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "UnrealBuildTool.sln" && !x.IsDirectory);
        }

        [SkippableFact]
        public async Task GitLayerEnumeratesDirectoryEntriesCorrectlyWithCaseInsensitivePath()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddLocalIoFileFactory();

            var serviceProvider = services.BuildServiceProvider();

            var factory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();

            var gitLayer = factory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var entries = gitLayer.List(@"eNgInE\sOuRcE\pRoGrAmS\uNREALbUILDtOOL");
            Assert.NotNull(entries);

            var entriesList = entries.ToList();
            Assert.Contains(entriesList, x => x.Name == "Configuration" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Executors" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Matchers" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Modes" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Platform" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Preprocessor" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "ProjectFiles" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Properties" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Storage" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "System" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "ToolChain" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "app.manifest" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "runtimeconfig.template.json" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "UnrealBuildTool.cs" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "UnrealBuildTool.csproj" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "UnrealBuildTool.sln" && !x.IsDirectory);
        }

        [SkippableFact]
        public async Task GitLayerEnumeratesRootDirectoryEntriesCorrectly()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddLocalIoFileFactory();

            var serviceProvider = services.BuildServiceProvider();

            var factory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();

            var gitLayer = factory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var entries = gitLayer.List(string.Empty);
            Assert.NotNull(entries);

            var entriesList = entries.ToList();
            Assert.Contains(entriesList, x => x.Name == "Engine" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Samples" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Templates" && x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == ".editorconfig" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == ".gitattributes" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == ".gitignore" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Default.uprojectdirs" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "GenerateProjectFiles.bat" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "GenerateProjectFiles.command" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "GenerateProjectFiles.sh" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "LICENSE.md" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "README.md" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Setup.bat" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Setup.command" && !x.IsDirectory);
            Assert.Contains(entriesList, x => x.Name == "Setup.sh" && !x.IsDirectory);
        }
    }
}