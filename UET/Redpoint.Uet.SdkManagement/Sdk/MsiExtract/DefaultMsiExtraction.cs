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
        retryExtract:
            _logger.LogInformation($"Extracting MSI: {msiFilename}");
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
            if (!File.Exists(Path.Combine(targetPath, msiFilename)))
            {
                throw new SdkSetupPackageGenerationFailedException($"MSI extraction failed for: {msiFilename}");
            }
            File.Delete(Path.Combine(targetPath, msiFilename));
        }
    }

}
