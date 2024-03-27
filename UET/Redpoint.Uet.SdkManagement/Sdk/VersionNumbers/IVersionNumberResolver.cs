namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    public interface IVersionNumberResolver
    {
        T For<T>(string unrealEnginePath) where T : IVersionNumbers;
    }
}
