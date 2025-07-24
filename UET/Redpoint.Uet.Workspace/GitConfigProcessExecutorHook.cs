namespace Redpoint.Uet.Workspace
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal sealed class GitConfigProcessExecutorHook : IProcessExecutorHook
    {
        private readonly ILogger<GitConfigProcessExecutorHook> _logger;

        public GitConfigProcessExecutorHook(
            ILogger<GitConfigProcessExecutorHook> logger)
        {
            _logger = logger;
        }

        public Task ModifyProcessSpecificationAsync(
            ProcessSpecification processSpecification,
            CancellationToken cancellationToken)
        {
            var filename = Path.GetFileNameWithoutExtension(processSpecification.FilePath);
            if (!string.Equals(filename, "git", StringComparison.Ordinal))
            {
                return Task.CompletedTask;
            }

            // Validate that the config file doesn't contain null characters and is not corrupt.
            var gitConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".gitconfig");
            if (!File.Exists(gitConfigPath))
            {
                return Task.CompletedTask;
            }

            var gitConfig = File.ReadAllBytes(gitConfigPath);
            if (gitConfig != null &&
                gitConfig.Length >= 4 &&
                gitConfig[0] == 0 &&
                gitConfig[1] == 0 &&
                gitConfig[2] == 0 &&
                gitConfig[3] == 0)
            {
                // This .gitconfig file is almost certainly corrupt. Delete it.
                _logger.LogInformation("Detected corrupt .gitignore file; deleting it before running Git...");
                File.Delete(gitConfigPath);
            }

            return Task.CompletedTask;
        }
    }
}
