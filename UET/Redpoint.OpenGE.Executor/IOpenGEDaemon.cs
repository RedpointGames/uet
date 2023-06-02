namespace Redpoint.OpenGE.Executor
{
    using OpenGE;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public interface IOpenGEDaemon
    {
        Task StartAsync(CancellationToken shutdownCancellationToken);

        string GetConnectionString();

        Task StopAsync();
    }
}
