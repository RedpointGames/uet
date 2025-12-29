namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step
{
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.RuntimeJson;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    public interface IProvisioningStep
    {
        string Type { get; }

        IRuntimeJson GetJsonType(JsonSerializerOptions options);

        /// <summary>
        /// Flags that change the behaviour of how the server handles this provisioning step.
        /// </summary>
        ProvisioningStepFlags Flags { get; }

        Task ExecuteOnServerUncastedBeforeAsync(
            object? configUncasted,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken);

        Task ExecuteOnClientUncastedAsync(
            object? configUncasted,
            IProvisioningStepClientContext context,
            CancellationToken cancellationToken);

        Task ExecuteOnServerUncastedAfterAsync(
            object? configUncasted,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken);

        Task<string?> GetIpxeAutoexecScriptOverrideOnServerUncastedAsync(
            object? configUncasted,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken);
    }

    public interface IProvisioningStep<TConfig> : IProvisioningStep where TConfig : new()
    {
        Task ExecuteOnServerBeforeAsync(
            TConfig config,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken);

        Task ExecuteOnClientAsync(
            TConfig config,
            IProvisioningStepClientContext context,
            CancellationToken cancellationToken);

        Task ExecuteOnServerAfterAsync(
            TConfig config,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken);

        Task<string?> GetIpxeAutoexecScriptOverrideOnServerAsync(
            TConfig config,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }

        [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This can't be sealed.")]
        Task IProvisioningStep.ExecuteOnServerUncastedBeforeAsync(
            object? configUncasted,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken)
        {
            return ExecuteOnServerBeforeAsync(
                configUncasted == null ? new() : (TConfig)configUncasted,
                nodeStatus,
                serverContext,
                cancellationToken);
        }

        [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This can't be sealed.")]
        Task IProvisioningStep.ExecuteOnClientUncastedAsync(
            object? configUncasted,
            IProvisioningStepClientContext context,
            CancellationToken cancellationToken)
        {
            return ExecuteOnClientAsync(
                configUncasted == null ? new() : (TConfig)configUncasted,
                context,
                cancellationToken);
        }

        [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This can't be sealed.")]
        Task IProvisioningStep.ExecuteOnServerUncastedAfterAsync(
            object? configUncasted,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken)
        {
            return ExecuteOnServerAfterAsync(
                configUncasted == null ? new() : (TConfig)configUncasted,
                nodeStatus,
                serverContext,
                cancellationToken);
        }

        [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This can't be sealed.")]
        Task<string?> IProvisioningStep.GetIpxeAutoexecScriptOverrideOnServerUncastedAsync(
            object? configUncasted,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken)
        {
            return GetIpxeAutoexecScriptOverrideOnServerAsync(
                configUncasted == null ? new() : (TConfig)configUncasted,
                nodeStatus,
                serverContext,
                cancellationToken);
        }
    }
}
