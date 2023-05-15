namespace Redpoint.UET.SdkManagement
{
    public interface ISdkSetup
    {
        string PlatformName { get; }

        Task<string> ComputeSdkPackageId(string unrealEnginePath, CancellationToken cancellationToken);

        Task GenerateSdkPackage(string unrealEnginePath, string sdkPackagePath, CancellationToken cancellationToken);

        Task<EnvironmentForSdkUsage> EnsureSdkPackage(string sdkPackagePath, CancellationToken cancellationToken);
    }
}