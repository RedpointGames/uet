namespace Redpoint.Git.Native
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System;

    /// <summary>
    /// The default implementation of <see cref="IGitRepoManagerFactory"/>. This should be registered as a singleton in dependency injection.
    /// </summary>
    public class GitRepoManagerFactory : IGitRepoManagerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Constructs a new <see cref="GitRepoManagerFactory"/>.
        /// </summary>
        /// <param name="serviceProvider">The dependency injection service provider.</param>
        public GitRepoManagerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Constructs a new <see cref="IGitRepoManager"/> for the specified Git repository.
        /// </summary>
        /// <param name="gitRepoPath">The path to the Git repository.</param>
        /// <returns>The new <see cref="IGitRepoManager"/> instance.</returns>
        public IGitRepoManager CreateGitRepoManager(string gitRepoPath)
        {
            return new GitRepoManager(
                _serviceProvider.GetRequiredService<ILogger<GitRepoManager>>(),
                gitRepoPath);
        }
    }
}
