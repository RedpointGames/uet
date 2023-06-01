namespace Redpoint.OpenGE.Executor
{
    using System.Threading.Tasks;

    internal interface ICoreReservation
    {
        Task<int> AllocateCoreAsync(CancellationToken cancellationToken);

        Task ReleaseCoreAsync(int core, CancellationToken cancellationToken);
    }
}
