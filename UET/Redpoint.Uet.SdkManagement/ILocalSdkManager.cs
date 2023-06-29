namespace Redpoint.Uet.SdkManagement
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface ILocalSdkManager
    {
        string[] GetRecognisedPlatforms();

        Task<Dictionary<string, string>> EnsureSdkForPlatformAsync(
            string enginePath,
            string sdksPath,
            string platform,
            CancellationToken cancellationToken);
    }
}
