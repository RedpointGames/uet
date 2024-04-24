#if GIT_NATIVE_CODE_ENABLED

namespace Redpoint.Vfs.Layer.Scratch.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.Layer.Git;
    using Redpoint.Vfs.LocalIo;
    using System.Text;

    public class ScratchProjectionLayerTests
    {
        [SkippableFact]
        public async Task DirectoryEntriesAreNotDuplicatedWhenWritingNestedFile()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();

            var serviceProvider = services.BuildServiceProvider();

            var gitFactory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();
            var scratchFactory = serviceProvider.GetRequiredService<IScratchVfsLayerFactory>();

            var gitLayer = gitFactory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var scratchPath = Path.Combine(Environment.CurrentDirectory, $"scratch-{Guid.NewGuid()}");
            try
            {
                Directory.CreateDirectory(scratchPath);
                using (var scratchLayer = scratchFactory.CreateLayer(
                    path: scratchPath,
                    nextLayer: gitLayer,
                    enableCorrectnessChecks: true))
                {
                    // Create nested directory.
                    Assert.True(scratchLayer.CreateDirectory(@"Engine\Source\Programs\UnrealBuildTool\obj"), "Must be able to create nested directory");

                    // Create a file.
                    VfsEntry? metadata = null;
                    var handle = scratchLayer.OpenFile(
                        @"Engine\Source\Programs\UnrealBuildTool\obj\test.txt",
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        ref metadata);
                    Assert.NotNull(handle);
                    using (handle)
                    {
                        var buffer = Encoding.ASCII.GetBytes("hello world");
                        var result = handle.VfsFile.WriteFile(buffer, (uint)buffer.Length, out _, 0);
                        Assert.Equal(0, result);
                    }

                    // Check to make sure we don't have two UnrealBuildTool entries
                    // when listing the "Programs" folder.
                    var entries = scratchLayer.List(@"Engine\Source\Programs");
                    Assert.NotNull(entries);
                    Assert.Single(entries, x => x.Name.ToLower() == "unrealbuildtool");
                }
            }
            finally
            {
                Directory.Delete(scratchPath, true);
            }
        }

        [SkippableFact]
        public async Task DirectoryEntriesAreNotDuplicatedWhenWritingNestedFileWithMismatchedCasingAtDirectoryLevel()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();

            var serviceProvider = services.BuildServiceProvider();

            var gitFactory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();
            var scratchFactory = serviceProvider.GetRequiredService<IScratchVfsLayerFactory>();

            var gitLayer = gitFactory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var scratchPath = Path.Combine(Environment.CurrentDirectory, $"scratch-{Guid.NewGuid()}");
            try
            {
                Directory.CreateDirectory(scratchPath);
                using (var scratchLayer = scratchFactory.CreateLayer(
                    path: scratchPath,
                    nextLayer: gitLayer,
                    enableCorrectnessChecks: true))
                {
                    // Create nested directory.
                    Assert.True(scratchLayer.CreateDirectory(@"ENGINE\SOURCE\PROGRAMS\UNREALBUILDTOOL\obj"), "Must be able to create nested directory");

                    // Create a file.
                    VfsEntry? metadata = null;
                    var handle = scratchLayer.OpenFile(
                        @"ENGINE\SOURCE\PROGRAMS\UNREALBUILDTOOL\obj\test.txt",
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        ref metadata);
                    Assert.NotNull(handle);
                    using (handle)
                    {
                        var buffer = Encoding.ASCII.GetBytes("hello world");
                        var result = handle.VfsFile.WriteFile(buffer, (uint)buffer.Length, out _, 0);
                        Assert.Equal(0, result);
                    }

                    // Check to make sure we don't have two UnrealBuildTool entries
                    // when listing the "Programs" folder.
                    var entries = scratchLayer.List(@"Engine\Source\Programs");
                    Assert.NotNull(entries);
                    Assert.Single(entries, x => x.Name.ToLower() == "unrealbuildtool");
                }
            }
            finally
            {
                Directory.Delete(scratchPath, true);
            }
        }

        [SkippableFact]
        public async Task DirectoryEntriesAreNotDuplicatedWhenWritingNestedFileWithMismatchedCasingAtFileLevel()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();

            var serviceProvider = services.BuildServiceProvider();

            var gitFactory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();
            var scratchFactory = serviceProvider.GetRequiredService<IScratchVfsLayerFactory>();

            var gitLayer = gitFactory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var scratchPath = Path.Combine(Environment.CurrentDirectory, $"scratch-{Guid.NewGuid()}");
            try
            {
                Directory.CreateDirectory(scratchPath);
                using (var scratchLayer = scratchFactory.CreateLayer(
                    path: scratchPath,
                    nextLayer: gitLayer,
                    enableCorrectnessChecks: true))
                {
                    // Create nested directory.
                    Assert.True(scratchLayer.CreateDirectory(@"Engine\Source\Programs\UnrealBuildTool\obj"), "Must be able to create nested directory");

                    // Create a file.
                    VfsEntry? metadata = null;
                    var handle = scratchLayer.OpenFile(
                        @"ENGINE\SOURCE\PROGRAMS\UNREALBUILDTOOL\obj\test.txt",
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        ref metadata);
                    Assert.NotNull(handle);
                    using (handle)
                    {
                        var buffer = Encoding.ASCII.GetBytes("hello world");
                        var result = handle.VfsFile.WriteFile(buffer, (uint)buffer.Length, out _, 0);
                        Assert.Equal(0, result);
                    }

                    // Check to make sure we don't have two UnrealBuildTool entries
                    // when listing the "Programs" folder.
                    var entries = scratchLayer.List(@"Engine\Source\Programs");
                    Assert.NotNull(entries);
                    Assert.Single(entries, x => x.Name.ToLower() == "unrealbuildtool");
                }
            }
            finally
            {
                Directory.Delete(scratchPath, true);
            }
        }

        [SkippableFact]
        public async Task DirectoryEntriesAreNotDuplicatedAfterMovingNestedFile()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();

            var serviceProvider = services.BuildServiceProvider();

            var gitFactory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();
            var scratchFactory = serviceProvider.GetRequiredService<IScratchVfsLayerFactory>();

            var gitLayer = gitFactory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var scratchPath = Path.Combine(Environment.CurrentDirectory, $"scratch-{Guid.NewGuid()}");
            try
            {
                Directory.CreateDirectory(scratchPath);
                using (var scratchLayer = scratchFactory.CreateLayer(
                    path: scratchPath,
                    nextLayer: gitLayer,
                    enableCorrectnessChecks: true))
                {
                    // Create nested directory.
                    Assert.True(scratchLayer.CreateDirectory(@"Engine\Source\Programs\UnrealBuildTool\obj"), "Must be able to create nested directory");

                    // Create a file.
                    VfsEntry? metadata = null;
                    var handle = scratchLayer.OpenFile(
                        @"Engine\Source\Programs\UnrealBuildTool\obj\test.txt",
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        ref metadata);
                    Assert.NotNull(handle);
                    using (handle)
                    {
                        var buffer = Encoding.ASCII.GetBytes("hello world");
                        var result = handle.VfsFile.WriteFile(buffer, (uint)buffer.Length, out _, 0);
                        Assert.Equal(0, result);
                    }

                    // Move the file.
                    Assert.True(scratchLayer.MoveFile(
                        @"Engine\Source\Programs\UnrealBuildTool\obj\test.txt",
                        @"Engine\Source\Programs\UnrealBuildTool\obj\test.txt2",
                        true));

                    // Check to make sure we don't have two UnrealBuildTool entries
                    // when listing the "Programs" folder.
                    var entries = scratchLayer.List(@"Engine\Source\Programs");
                    Assert.NotNull(entries);
                    Assert.Single(entries, x => x.Name.ToLower() == "unrealbuildtool");
                }
            }
            finally
            {
                Directory.Delete(scratchPath, true);
            }
        }

        [SkippableFact]
        public async Task DirectoryEntriesAreNotDuplicatedAfterMovingExtraNestedFile()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();

            var serviceProvider = services.BuildServiceProvider();

            var gitFactory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();
            var scratchFactory = serviceProvider.GetRequiredService<IScratchVfsLayerFactory>();

            var gitLayer = gitFactory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var scratchPath = Path.Combine(Environment.CurrentDirectory, $"scratch-{Guid.NewGuid()}");
            try
            {
                Directory.CreateDirectory(scratchPath);
                using (var scratchLayer = scratchFactory.CreateLayer(
                    path: scratchPath,
                    nextLayer: gitLayer,
                    enableCorrectnessChecks: true))
                {
                    // Create nested directory.
                    Assert.True(scratchLayer.CreateDirectory(@"ENGINE\SOURCE\PROGRAMS\UNREALBUILDTOOL\OBJ\DEVELOPMENT"), "Must be able to create nested directory");

                    // Create a file.
                    VfsEntry? metadata = null;
                    var handle = scratchLayer.OpenFile(
                        @"ENGINE\SOURCE\PROGRAMS\UNREALBUILDTOOL\OBJ\DEVELOPMENT\RCXBEB3.TMP",
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        ref metadata);
                    Assert.NotNull(handle);
                    using (handle)
                    {
                        var buffer = Encoding.ASCII.GetBytes("hello world");
                        var result = handle.VfsFile.WriteFile(buffer, (uint)buffer.Length, out _, 0);
                        Assert.Equal(0, result);
                    }

                    // Move the file.
                    Assert.True(scratchLayer.MoveFile(
                        @"ENGINE\SOURCE\PROGRAMS\UNREALBUILDTOOL\OBJ\DEVELOPMENT\RCXBEB3.TMP",
                        @"ENGINE\SOURCE\PROGRAMS\UNREALBUILDTOOL\OBJ\DEVELOPMENT\apphost.exe",
                        true));

                    // Check to make sure we don't have two UnrealBuildTool entries
                    // when listing the "Programs" folder.
                    var entries = scratchLayer.List(@"Engine\Source\Programs");
                    Assert.NotNull(entries);
                    Assert.Single(entries, x => x.Name.ToLower() == "unrealbuildtool");
                }
            }
            finally
            {
                Directory.Delete(scratchPath, true);
            }
        }

        [SkippableFact]
        public async Task DirectoryHasCorrectCasingAfterMovingExtraNestedFile()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();

            var serviceProvider = services.BuildServiceProvider();

            var gitFactory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();
            var scratchFactory = serviceProvider.GetRequiredService<IScratchVfsLayerFactory>();

            var gitLayer = gitFactory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var scratchPath = Path.Combine(Environment.CurrentDirectory, $"scratch-{Guid.NewGuid()}");
            try
            {
                Directory.CreateDirectory(scratchPath);
                using (var scratchLayer = scratchFactory.CreateLayer(
                    path: scratchPath,
                    nextLayer: gitLayer,
                    enableCorrectnessChecks: true))
                {
                    // Create nested directory.
                    Assert.True(scratchLayer.CreateDirectory(@"ENGINE\SOURCE\PROGRAMS\UNREALBUILDTOOL\OBJ\DEVELOPMENT"), "Must be able to create nested directory");

                    // Create a file.
                    VfsEntry? metadata = null;
                    var handle = scratchLayer.OpenFile(
                        @"ENGINE\SOURCE\PROGRAMS\UNREALBUILDTOOL\OBJ\DEVELOPMENT\RCXBEB3.TMP",
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        ref metadata);
                    Assert.NotNull(handle);
                    using (handle)
                    {
                        var buffer = Encoding.ASCII.GetBytes("hello world");
                        var result = handle.VfsFile.WriteFile(buffer, (uint)buffer.Length, out _, 0);
                        Assert.Equal(0, result);
                    }

                    // Move the file.
                    Assert.True(scratchLayer.MoveFile(
                        @"ENGINE\SOURCE\PROGRAMS\UNREALBUILDTOOL\OBJ\DEVELOPMENT\RCXBEB3.TMP",
                        @"ENGINE\SOURCE\PROGRAMS\UNREALBUILDTOOL\OBJ\DEVELOPMENT\apphost.exe",
                        true));

                    // Make sure the original casing of "UnrealBuildTool" has been preserved.
                    var entries = scratchLayer.List(@"Engine\Source\Programs");
                    Assert.NotNull(entries);
                    Assert.Single(entries, x => x.Name == "UnrealBuildTool");
                }
            }
            finally
            {
                Directory.Delete(scratchPath, true);
            }
        }

        [SkippableFact]
        public async Task ConcurrentDeleteAndCreateDoesNotResultInCorruptCache()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();

            var serviceProvider = services.BuildServiceProvider();

            var gitFactory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();
            var scratchFactory = serviceProvider.GetRequiredService<IScratchVfsLayerFactory>();

            var gitLayer = gitFactory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var scratchPath = Path.Combine(Environment.CurrentDirectory, $"scratch-{Guid.NewGuid()}");
            try
            {
                Directory.CreateDirectory(scratchPath);
                using (var scratchLayer = scratchFactory.CreateLayer(
                    path: scratchPath,
                    nextLayer: gitLayer,
                    enableCorrectnessChecks: true))
                {
                    var createTask = Task.Run(() =>
                    {
                        for (int i = 0; i < 1000; i++)
                        {
                            VfsEntry? metadata = null;
                            var handle = scratchLayer.OpenFile(
                                @"target.dat",
                                FileMode.Create,
                                FileAccess.ReadWrite,
                                FileShare.ReadWrite | FileShare.Delete,
                                ref metadata);
                            Assert.NotNull(handle);
                            using (handle)
                            {
                                var buffer = Encoding.ASCII.GetBytes("hello world");
                                var result = handle.VfsFile.WriteFile(buffer, (uint)buffer.Length, out _, 0);
                                Assert.Equal(0, result);
                            }
                        }
                    });
                    var deleteTask = Task.Run(() =>
                    {
                        for (int i = 0; i < 1000; i++)
                        {
                            // @note: We don't check the return result of this, because it will
                            // fail if the file doesn't exist.
                            scratchLayer.DeleteFile("target.dat");
                        }
                    });
                    await Task.WhenAll(createTask, deleteTask);
                }
            }
            finally
            {
                Directory.Delete(scratchPath, true);
            }
        }

        [SkippableFact]
        public async Task ChangingFileTimesWorks()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();

            var serviceProvider = services.BuildServiceProvider();

            var gitFactory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();
            var scratchFactory = serviceProvider.GetRequiredService<IScratchVfsLayerFactory>();

            var gitLayer = gitFactory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var scratchPath = Path.Combine(Environment.CurrentDirectory, $"scratch-{Guid.NewGuid()}");
            try
            {
                Directory.CreateDirectory(scratchPath);
                using (var scratchLayer = scratchFactory.CreateLayer(
                    path: scratchPath,
                    nextLayer: gitLayer,
                    enableCorrectnessChecks: true))
                {
                    VfsEntry? metadata = null;
                    var handle = scratchLayer.OpenFile(
                        @"test.dat",
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        ref metadata);
                    Assert.NotNull(handle);
                    using (handle)
                    {
                        var buffer = Encoding.ASCII.GetBytes("hello world");
                        var result = handle.VfsFile.WriteFile(buffer, (uint)buffer.Length, out _, 0);
                        Assert.Equal(0, result);
                    }

                    var targetDate = DateTimeOffset.FromFileTime(12345678);

                    Assert.True(scratchLayer.SetBasicInfo(
                        @"test.dat",
                        null,
                        targetDate,
                        targetDate,
                        targetDate,
                        targetDate));

                    var fileInfo = scratchLayer.GetInfo(@"test.dat");
                    Assert.NotNull(fileInfo);
                    Assert.Equal(targetDate, fileInfo.CreationTime);
                    Assert.Equal(targetDate, fileInfo.LastAccessTime);
                    Assert.Equal(targetDate, fileInfo.LastWriteTime);
                    Assert.Equal(targetDate, fileInfo.ChangeTime);
                }
            }
            finally
            {
                Directory.Delete(scratchPath, true);
            }
        }

        [SkippableFact]
        public async Task PathsHaveCorrectMateralizationStatus()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();

            var serviceProvider = services.BuildServiceProvider();

            var gitFactory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();
            var scratchFactory = serviceProvider.GetRequiredService<IScratchVfsLayerFactory>();

            var gitLayer = gitFactory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var scratchPath = Path.Combine(Environment.CurrentDirectory, $"scratch-{Guid.NewGuid()}");
            try
            {
                Directory.CreateDirectory(scratchPath);
                using (var scratchLayer = scratchFactory.CreateLayer(
                    path: scratchPath,
                    nextLayer: gitLayer,
                    enableCorrectnessChecks: true))
                {
                    // Check that "Engine" does not count as materialized, and that "test.dat" 
                    // does not exist.
                    var engineStatus = scratchLayer.GetPathStatus("Engine");
                    var fileStatus = scratchLayer.GetPathStatus(@"Engine\test.dat");
                    Assert.Equal(ScratchVfsPathStatus.Passthrough, engineStatus.status);
                    Assert.Equal(VfsEntryExistence.DirectoryExists, engineStatus.existence);
                    Assert.Equal(ScratchVfsPathStatus.Nonexistent, fileStatus.status);
                    Assert.Equal(VfsEntryExistence.DoesNotExist, fileStatus.existence);

                    // List entries under Engine.
                    var entries = scratchLayer.List("Engine");
                    Assert.NotNull(entries);
                    Assert.DoesNotContain(entries, x => x.Name == "test.dat");

                    // Create a new file.
                    VfsEntry? metadata = null;
                    var handle = scratchLayer.OpenFile(
                        @"Engine\test.dat",
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        ref metadata);
                    Assert.NotNull(handle);
                    using (handle)
                    {
                        var buffer = Encoding.ASCII.GetBytes("hello world");
                        var result = handle.VfsFile.WriteFile(buffer, (uint)buffer.Length, out _, 0);
                        Assert.Equal(0, result);
                    }

                    // Check that both "Engine" and "Engine/test.dat" count as materialized.
                    engineStatus = scratchLayer.GetPathStatus("Engine");
                    fileStatus = scratchLayer.GetPathStatus(@"Engine\test.dat");
                    Assert.Equal(ScratchVfsPathStatus.Materialized, engineStatus.status);
                    Assert.Equal(VfsEntryExistence.DirectoryExists, engineStatus.existence);
                    Assert.Equal(ScratchVfsPathStatus.Materialized, fileStatus.status);
                    Assert.Equal(VfsEntryExistence.FileExists, fileStatus.existence);
                }
            }
            finally
            {
                Directory.Delete(scratchPath, true);
            }
        }

        [SkippableFact]
        public async Task FilesystemScratchCacheIsCleared()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGitLayerFactory();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();

            var serviceProvider = services.BuildServiceProvider();

            var gitFactory = serviceProvider.GetRequiredService<IGitVfsLayerFactory>();
            var scratchFactory = serviceProvider.GetRequiredService<IScratchVfsLayerFactory>();

            var gitLayer = gitFactory.CreateNativeLayer(
                barePath: @"C:\Work\UE5\.git",
                blobPath: null,
                indexCachePath: null,
                commitHash: "cdaec5b33ea5d332e51eee4e4866495c90442122");
            await gitLayer.InitAsync(CancellationToken.None);

            var scratchPath = Path.Combine(Environment.CurrentDirectory, $"scratch-{Guid.NewGuid()}");
            try
            {
                Directory.CreateDirectory(scratchPath);
                using (var scratchLayer = scratchFactory.CreateLayer(
                    path: scratchPath,
                    nextLayer: gitLayer,
                    enableCorrectnessChecks: true))
                {
                    // List entries under Engine.
                    var entries = scratchLayer.List("Engine");
                    Assert.NotNull(entries);
                    Assert.DoesNotContain(entries, x => x.Name == "test.dat");

                    // Create a new file.
                    VfsEntry? metadata = null;
                    var handle = scratchLayer.OpenFile(
                        @"Engine\test.dat",
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        ref metadata);
                    Assert.NotNull(handle);
                    using (handle)
                    {
                        var buffer = Encoding.ASCII.GetBytes("hello world");
                        var result = handle.VfsFile.WriteFile(buffer, (uint)buffer.Length, out _, 0);
                        Assert.Equal(0, result);
                    }

                    // Verify that the entries now contain test.dat.
                    entries = scratchLayer.List("Engine");
                    Assert.NotNull(entries);
                    Assert.Contains(entries, x => x.Name == "test.dat");
                }
            }
            finally
            {
                Directory.Delete(scratchPath, true);
            }
        }
    }

}

#endif