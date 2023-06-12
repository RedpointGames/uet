namespace Redpoint.Vfs.Layer.Git.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Git.Native;

    public class GitFactoryTests
    {
        [Fact]
        public void CanConstructFactories()
        {
            var services = new ServiceCollection();
            services.AddGitLayerFactory();

            var sp = services.BuildServiceProvider();

            sp.GetRequiredService<IGitRepoManagerFactory>();
            sp.GetRequiredService<IGitVfsLayerFactory>();
        }
    }
}