namespace Redpoint.Vfs.Driver
{
    /// <summary>
    /// Additional options for a virtual filesystem driver.
    /// </summary>
    public class VfsDriverOptions
    {
        /// <summary>
        /// If set, the virtual filesystem driver will emit logs to this path.
        /// </summary>
        public string? DriverLogPath { get; set; }
    }
}
