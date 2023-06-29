namespace Redpoint.Uet.Automation.Worker.Local
{
    using System;
    using System.Net;
    using System.Threading.Tasks;

    internal abstract class LocalWorker : IWorker, IAsyncDisposable
    {
        public abstract string Id { get; }

        public abstract string DisplayName { get; }

        public abstract DesiredWorkerDescriptor Descriptor { get; }

        public abstract IPEndPoint EndPoint { get; }

        public abstract TimeSpan StartupDuration { get; }

        public abstract void StartInBackground();

        public abstract ValueTask DisposeAsync();
    }
}
