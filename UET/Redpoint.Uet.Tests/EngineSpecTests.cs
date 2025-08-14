namespace Redpoint.Uet.Tests
{
    using System.CommandLine;
    using System.CommandLine.Parsing;
    using UET.Commands.EngineSpec;
    using Xunit;

    public class EngineSpecTests
    {
        [Fact]
        public void Path()
        {
            var option = new Option<EngineSpec>("--engine", parseArgument: EngineSpec.ParseEngineSpecContextless());
            var result = option.Parse([
                "--engine",
                Environment.CurrentDirectory
            ]);
            var spec = result.GetValueForOption(option);
            Assert.NotNull(spec);
            Assert.Equal(EngineSpecType.Path, spec.Type);
            Assert.Equal(Environment.CurrentDirectory, spec.Path);
        }

        [Fact]
        public void Git()
        {
            var option = new Option<EngineSpec>("--engine", parseArgument: EngineSpec.ParseEngineSpecContextless());
            var result = option.Parse([
                "--engine",
                "git:main@git@example.com:group/repository.git"
            ]);
            var spec = result.GetValueForOption(option);
            Assert.NotNull(spec);
            Assert.Equal(EngineSpecType.GitCommit, spec.Type);
            Assert.Equal("main", spec.GitCommit);
            Assert.Equal("git@example.com:group/repository.git", spec.GitUrl);
        }

        [Fact]
        public void GitLegacy()
        {
            var option = new Option<EngineSpec>("--engine", parseArgument: EngineSpec.ParseEngineSpecContextless());
            var result = option.Parse([
                "--engine",
                "git:main@git@example.com:group/repository.git,f:a,z:b,wc:c,mc:d"
            ]);
            var spec = result.GetValueForOption(option);
            Assert.NotNull(spec);
            Assert.Equal(EngineSpecType.GitCommit, spec.Type);
            Assert.Equal("main", spec.GitCommit);
            Assert.Equal("git@example.com:group/repository.git", spec.GitUrl);
            Assert.NotNull(spec.FolderLayers);
            Assert.Equal("a", Assert.Single(spec.FolderLayers));
            Assert.NotNull(spec.ZipLayers);
            Assert.Equal("b", Assert.Single(spec.ZipLayers));
            Assert.Equal("c", spec.WindowsSharedGitCachePath);
            Assert.Equal("d", spec.MacSharedGitCachePath);
        }

        [Fact]
        public void GitWithOptions()
        {
            var option = new Option<EngineSpec>("--engine", parseArgument: EngineSpec.ParseEngineSpecContextless());
            var result = option.Parse([
                "--engine",
                "git:main@git@example.com:group/repository.git?submodules=false&lfs=true&config=f:a,z:b,wc:c,mc:d"
            ]);
            var spec = result.GetValueForOption(option);
            Assert.NotNull(spec);
            Assert.Equal(EngineSpecType.GitCommit, spec.Type);
            Assert.Equal("main", spec.GitCommit);
            Assert.Equal("git@example.com:group/repository.git", spec.GitUrl);
            Assert.Equal("false", spec.GitQueryString?["submodules"]);
            Assert.Equal("true", spec.GitQueryString?["lfs"]);
            Assert.Equal("f:a,z:b,wc:c,mc:d", spec.GitQueryString?["config"]);
            Assert.NotNull(spec.FolderLayers);
            Assert.Equal("a", Assert.Single(spec.FolderLayers));
            Assert.NotNull(spec.ZipLayers);
            Assert.Equal("b", Assert.Single(spec.ZipLayers));
            Assert.Equal("c", spec.WindowsSharedGitCachePath);
            Assert.Equal("d", spec.MacSharedGitCachePath);
        }

        [Fact]
        public void Uefs()
        {
            var option = new Option<EngineSpec>("--engine", parseArgument: EngineSpec.ParseEngineSpecContextless());
            var result = option.Parse([
                "--engine",
                "uefs:example.com/path:tag"
            ]);
            var spec = result.GetValueForOption(option);
            Assert.NotNull(spec);
            Assert.Equal(EngineSpecType.UEFSPackageTag, spec.Type);
            Assert.Equal("example.com/path:tag", spec.UEFSPackageTag);
        }
    }
}
