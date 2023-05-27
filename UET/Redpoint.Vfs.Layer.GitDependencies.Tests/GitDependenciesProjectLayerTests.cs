namespace Redpoint.Vfs.Layer.GitDependencies.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Vfs.Layer.Git;
    using Redpoint.Vfs.LocalIo;

    public class GitDependenciesProjectLayerTests
    {
        [SkippableFact]
        public async Task GitDependenciesLayerEnumeratesRootDirectoryEntriesCorrectly()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddGitDependenciesLayerFactory();
            services.AddLocalIoFileFactory();

            var serviceProvider = services.BuildServiceProvider();

            var gitFactory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();
            var gitDependenciesFactory = serviceProvider.GetRequiredService<IGitDependenciesVfsLayerFactory>();

            var loggerFactory = LoggerFactory.Create(o => { });

            var gitLayer = gitFactory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var gitDependenciesLayer = gitDependenciesFactory.CreateLayer(
                cachePath: @"C:\Work\UE5\.git\git-deps",
                nextLayer: gitLayer);
            await gitDependenciesLayer.InitAsync(CancellationToken.None);

            var entries = gitDependenciesLayer.List(string.Empty);
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
            // This test ensures that Git files and Git dependency files get merged into the directory tree correctly.
            Assert.Contains(entriesList, x => x.Name == "cpp.hint" && !x.IsDirectory);
        }
    }
}