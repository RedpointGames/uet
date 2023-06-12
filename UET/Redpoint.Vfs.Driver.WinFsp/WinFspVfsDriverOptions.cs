namespace Redpoint.Vfs.Driver.WinFsp
{
    using System.Runtime.Versioning;

    /// <summary>
    /// Additional options specific to the WinFsp filesystem driver implementation.
    /// </summary>
    [SupportedOSPlatform("windows6.2")]
    public class WinFspVfsDriverOptions : VfsDriverOptions
    {
        /// <summary>
        /// If true, additional correctness checks will be performed. Virtual filesystem operations will be slower with this enabled.
        /// </summary>
        public bool EnableCorrectnessChecks { get; set; } = false;

        /// <summary>
        /// If true, asynchronous I/O is enabled. Asynchronous I/O is experimental and is known not to work for some applications.
        /// </summary>
        public bool EnableAsyncIo { get; set; } = false;

        /// <summary>
        /// If true, the names of files on the virtual filesystem will be normalized. Currently disabled by default as it imposes a performance penalty, but required for some applications.
        /// </summary>
        public bool EnableNameNormalization { get; set; } = false;
    }
}
