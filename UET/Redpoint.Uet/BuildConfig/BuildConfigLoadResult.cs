namespace UET.BuildConfig
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    public sealed class BuildConfigLoadResult
    {
        public required bool Success { get; set; }

        public required IReadOnlyList<string> ErrorList { get; set; }

        public required Redpoint.Uet.Configuration.BuildConfig? BuildConfig { get; set; }
    }
}
