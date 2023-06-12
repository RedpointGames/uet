namespace Redpoint.Vfs.Driver
{
    /// <summary>
    /// Represents a virtual filesystem driver, which can be disposed when the driver should be unmounted.
    /// </summary>
    public interface IVfsDriver : IDisposable
    {
    }
}
