namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step
{
    using Redpoint.RuntimeJson;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public interface IProvisioningStep
    {
        string Type { get; }

        IRuntimeJson Settings { get; }

        Task ExecuteOnServerUncastedBeforeAsync(
            object configUncasted,
            CancellationToken cancellationToken);

        Task ExecuteOnClientUncastedAsync(
            object configUncasted,
            CancellationToken cancellationToken);

        Task ExecuteOnServerUncastedAfterAsync(
            object configUncasted,
            CancellationToken cancellationToken);
    }

    public interface IProvisioningStep<TConfig> : IProvisioningStep
    {
        Task ExecuteOnServerBeforeAsync(
            TConfig config,
            CancellationToken cancellationToken);

        Task ExecuteOnClientAsync(
            TConfig config,
            CancellationToken cancellationToken);

        Task ExecuteOnServerAfterAsync(
            TConfig config,
            CancellationToken cancellationToken);

        [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This can't be sealed.")]
        Task IProvisioningStep.ExecuteOnServerUncastedBeforeAsync(object configUncasted, CancellationToken cancellationToken)
        {
            return ExecuteOnServerBeforeAsync((TConfig)configUncasted, cancellationToken);
        }

        [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This can't be sealed.")]
        Task IProvisioningStep.ExecuteOnClientUncastedAsync(object configUncasted, CancellationToken cancellationToken)
        {
            return ExecuteOnClientAsync((TConfig)configUncasted, cancellationToken);
        }

        [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This can't be sealed.")]
        Task IProvisioningStep.ExecuteOnServerUncastedAfterAsync(object configUncasted, CancellationToken cancellationToken)
        {
            return ExecuteOnServerAfterAsync((TConfig)configUncasted, cancellationToken);
        }
    }
}
