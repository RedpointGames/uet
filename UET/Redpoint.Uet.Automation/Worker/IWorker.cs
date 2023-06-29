namespace Redpoint.Uet.Automation.Worker
{
    using System;
    using System.Net;

    public interface IWorker
    {
        string Id { get; }

        string DisplayName { get; }

        DesiredWorkerDescriptor Descriptor { get; }

        IPEndPoint EndPoint { get; }

        TimeSpan StartupDuration { get; }
    }
}
