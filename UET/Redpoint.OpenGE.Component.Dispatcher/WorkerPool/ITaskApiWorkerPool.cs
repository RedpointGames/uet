namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public interface ITaskApiWorkerPool : IAsyncDisposable
    {
        Task<IWorkerCoreRequest<ITaskApiWorkerCore>> ReserveCoreAsync(bool requireLocal, CancellationToken cancellationToken);
    }
}
