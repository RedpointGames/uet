using Microsoft.Extensions.Hosting;
using Redpoint.Concurrency;

namespace Redpoint.KubernetesManager.HostedService
{
    internal sealed class RkmHostApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        public readonly CancellationTokenSource CtsStarted;
        public readonly CancellationTokenSource CtsStopping;
        public readonly CancellationTokenSource CtsStopped;
        public readonly Gate StopRequestedGate;

        public RkmHostApplicationLifetime()
        {
            CtsStarted = new CancellationTokenSource();
            CtsStopping = new CancellationTokenSource();
            CtsStopped = new CancellationTokenSource();
            StopRequestedGate = new Gate();
        }

        public CancellationToken ApplicationStarted => CtsStarted.Token;

        public CancellationToken ApplicationStopping => CtsStopping.Token;

        public CancellationToken ApplicationStopped => CtsStopped.Token;

        public void StopApplication()
        {
            StopRequestedGate.Open();
            CtsStopping.Cancel();
        }

        public void Dispose()
        {
            ((IDisposable)CtsStarted).Dispose();
            ((IDisposable)CtsStopping).Dispose();
            ((IDisposable)CtsStopped).Dispose();
        }
    }
}
