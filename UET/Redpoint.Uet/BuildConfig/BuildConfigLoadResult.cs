namespace Redpoint.Uet.BuildConfig
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    public sealed class BuildConfigLoadResult
    {
        public required bool Success { get; set; }

        public required IReadOnlyList<string> ErrorList { get; set; }

        public required Configuration.BuildConfig? BuildConfig { get; set; }
    }
}
