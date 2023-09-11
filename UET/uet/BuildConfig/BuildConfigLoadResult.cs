namespace UET.BuildConfig
{
    using System.Collections.Generic;

    internal sealed class BuildConfigLoadResult
    {
        public required bool Success { get; set; }

        public required List<string> ErrorList { get; set; }

        public required Redpoint.Uet.Configuration.BuildConfig? BuildConfig { get; set; }
    }
}
