namespace Redpoint.CloudFramework.Locking
{
    using System;

    public class LockAcquisitionException : Exception
    {
        public LockAcquisitionException(string lockId) : base("Unable to acquire lock: " + lockId + ", already in use.")
        {
        }
    }
}
