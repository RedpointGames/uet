namespace Redpoint.UET.UAT.Internal
{
    using System.Threading.Tasks;

    internal interface IBuildConfigurationManager
    {
        Task<bool> PushBuildConfiguration();

        Task PopBuildConfiguration();
    }
}
