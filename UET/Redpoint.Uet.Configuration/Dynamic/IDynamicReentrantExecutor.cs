namespace Redpoint.Uet.Configuration.Dynamic
{
    using System.Text.Json;
    using System.Threading.Tasks;

    public interface IDynamicReentrantExecutor<TDistribution> : IDynamicProviderRegistration, IDynamicProvider
    {
        Task<int> ExecuteBuildGraphNodeAsync(
            object configUnknown,
            Dictionary<string, string> runtimeSettings,
            CancellationToken cancellationToken);
    }

    public interface IDynamicReentrantExecutor<TDistribution, TConfigClass> : IDynamicReentrantExecutor<TDistribution>
    {
    }
}
