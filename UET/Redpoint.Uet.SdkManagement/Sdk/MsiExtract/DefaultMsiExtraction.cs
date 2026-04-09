namespace Redpoint.Uet.SdkManagement.Sdk.MsiExtract
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using System;
    using System.Text;

    internal class DefaultMsiExtraction : IMsiExtraction
    {
        private readonly ILogger<DefaultMsiExtraction> _logger;
        private readonly IProcessExecutor _processExecutor;

        public DefaultMsiExtraction(
            ILogger<DefaultMsiExtraction> logger,
            IProcessExecutor processExecutor)
        {
            _logger = logger;
            _processExecutor = processExecutor;
        }

        public async Task ExtractMsiAsync(
            string msiSourceDirectory,
            string msiFilename,
            string targetPath,
            CancellationToken cancellationToken)
        {
            if (Path.GetFileName(msiFilename) != msiFilename)
            {
                throw new InvalidOperationException($"{msiFilename} should be a filename only.");
            }

            var msiFullPath = Path.Combine(msiSourceDirectory, msiFilename);
            var msiTargetRedundantPath = Path.Combine(targetPath, msiFilename);

        retryExtract:
            _logger.LogInformation($"Extracting MSI: {msiFullPath}");
            var msiexecOutput = new StringBuilder();
            await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = @"C:\WINDOWS\system32\msiexec.exe",
                    Arguments = new LogicalProcessArgument[]
                    {
                        "/a",
                        msiFilename,
                        "/quiet",
                        "/qn",
                        $"TARGETDIR={targetPath}",
                    },
                    WorkingDirectory = msiSourceDirectory
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(msiexecOutput),
                cancellationToken).ConfigureAwait(false);
            if (msiexecOutput.ToString().Contains("Another program is being installed.", StringComparison.Ordinal))
            {
                _logger.LogWarning("Another instance of msiexec is currently running; retrying extraction in 2 seconds...");
                await Task.Delay(2000, cancellationToken);
                goto retryExtract;
            }
            if (!File.Exists(msiFullPath))
            {
                throw new SdkSetupPackageGenerationFailedException($"MSI extraction unexpectedly deleted source MSI file: {msiFullPath}");
            }
            if (!File.Exists(msiTargetRedundantPath))
            {
                throw new SdkSetupPackageGenerationFailedException($"MSI extraction failed for: {msiFilename}");
            }
            _logger.LogInformation($"Deleting unneeded MSI file that was copied during install: {msiTargetRedundantPath}");
            File.Delete(msiTargetRedundantPath);
        }
    }

}
