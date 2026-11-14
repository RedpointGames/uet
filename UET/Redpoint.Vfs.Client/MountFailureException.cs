namespace Redpoint.Vfs.Client
{
    public class MountFailureException : Exception
    { 
        public MountFailureException()
            : base("Failed to mount the virtual filesystem.")
        {
        }
    }
}
