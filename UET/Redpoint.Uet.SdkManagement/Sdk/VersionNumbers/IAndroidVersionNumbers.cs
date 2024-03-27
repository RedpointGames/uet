namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    internal interface IAndroidVersionNumbers : IVersionNumbers
    {
        Task<(string platforms, string buildTools, string cmake, string ndk)> GetVersions(string unrealEnginePath);
    }
}
