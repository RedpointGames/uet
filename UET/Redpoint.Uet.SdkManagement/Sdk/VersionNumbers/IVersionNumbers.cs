namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    public interface IVersionNumbers
    {
        bool CanUse(string unrealEnginePath);

        int Priority { get; }
    }
}
