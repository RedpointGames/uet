namespace UET.BuildConfig
{
    using System.Collections.Generic;

    internal class BuildConfigLoadResult
    {
        public required bool Success { get; set; }

        public required List<string> ErrorList { get; set; }

        public required Redpoint.UET.Configuration.BuildConfig? BuildConfig { get; set; }
    }
}
