namespace Io.Processors
{
    using System.Threading;

    public static class GlobalSemaphore
    {
        public static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1);
    }
}
