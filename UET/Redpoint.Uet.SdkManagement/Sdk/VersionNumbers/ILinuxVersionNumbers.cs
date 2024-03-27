namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    using System.Threading.Tasks;

    internal interface ILinuxVersionNumbers : IVersionNumbers
    {
        Task<string> GetClangToolchainVersion(string unrealEnginePath);
    }
}
