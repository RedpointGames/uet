namespace Redpoint.Git.Native
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System;

    public class GitRepoManagerFactory : IGitRepoManagerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public GitRepoManagerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IGitRepoManager CreateGitRepoManager(string gitRepoPath)
        {
            return new GitRepoManager(
                _serviceProvider.GetRequiredService<ILogger<GitRepoManager>>(),
                gitRepoPath);
        }
    }
}
