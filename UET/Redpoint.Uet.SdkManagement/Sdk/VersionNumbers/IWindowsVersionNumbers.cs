namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    using Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk;
    using System.Threading.Tasks;

    internal interface IWindowsVersionNumbers : IVersionNumbers
    {
        Task<WindowsSdkInstallerTarget> GetWindowsVersionNumbersAsync(string unrealEnginePath);
    }
}
