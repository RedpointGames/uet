namespace Redpoint.Vfs.Driver.WinFsp
{
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows6.2")]
    public class WinFspVfsDriverOptions : VfsDriverOptions
    {
        public bool EnableCorrectnessChecks { get; set; } = false;

        public bool EnableAsyncIo { get; set; } = false;

        public bool EnableNameNormalization { get; set; } = false;
    }
}
