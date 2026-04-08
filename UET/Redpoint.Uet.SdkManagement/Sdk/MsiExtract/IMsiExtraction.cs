namespace Redpoint.Uet.SdkManagement.Sdk.MsiExtract
{
    using System.Collections.Generic;

    public interface IMsiExtraction
    {
        Task ExtractMsiAsync(
            string msiSourceDirectory,
            string msiFilename,
            string targetPath,
            CancellationToken cancellationToken);
    }

}
