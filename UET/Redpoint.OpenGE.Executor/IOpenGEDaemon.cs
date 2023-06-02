namespace Redpoint.OpenGE.Executor
{
    using OpenGE;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public interface IOpenGEDaemon
    {
        Task<string> StartIfNeededAndGetConnectionEnvironmentVariableAsync(CancellationToken cancellationToken);

        Task StopAsync(CancellationToken cancellationToken);
    }
}
