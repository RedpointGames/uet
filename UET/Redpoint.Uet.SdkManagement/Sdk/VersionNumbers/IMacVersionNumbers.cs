namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    internal interface IMacVersionNumbers : IVersionNumbers
    {
        Task<string> GetXcodeVersion(string unrealEnginePath);
    }
}
