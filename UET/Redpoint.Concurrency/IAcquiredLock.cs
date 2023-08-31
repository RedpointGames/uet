namespace Redpoint.Concurrency
{
    using System;

    /// <summary>
    /// Represents an acquired lock. Dispose it when you are finished with the lock.
    /// </summary>
    public interface IAcquiredLock : IDisposable
    {
    }
}
