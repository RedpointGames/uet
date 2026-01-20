namespace Redpoint.Uet.BuildPipeline.BuildGraph
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    internal class EngineBuildVersion
    {
        [JsonPropertyName("MajorVersion")]
        public int MajorVersion { get; set; }

        [JsonPropertyName("MinorVersion")]
        public int MinorVersion { get; set; }

        [JsonPropertyName("PatchVersion")]
        public int PatchVersion { get; set; }

        [JsonPropertyName("Changelist")]
        public int Changelist { get; set; }

        [JsonPropertyName("CompatibleChangelist")]
        public int CompatibleChangelist { get; set; }

        [JsonPropertyName("IsLicenseeVersion")]
        public int IsLicenseeVersion { get; set; }

        [JsonPropertyName("IsPromotedBuild")]
        public int IsPromotedBuild { get; set; }

        [JsonPropertyName("BranchName")]
        public string? BranchName { get; set; }
    }
}
