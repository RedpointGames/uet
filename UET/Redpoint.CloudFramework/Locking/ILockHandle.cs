namespace Redpoint.CloudFramework.Locking
{
    using System.Threading.Tasks;

    public interface ILockHandle : IAsyncDisposable
    {
        [Obsolete("Use DisposeAsync instead.")]
        Task Release();
    }
}
