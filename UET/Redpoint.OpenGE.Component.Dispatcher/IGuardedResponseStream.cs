namespace Redpoint.OpenGE.Component.Dispatcher
{
    using System.Threading.Tasks;

    internal interface IGuardedResponseStream<T>
    {
        Task WriteAsync(
            T response,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
