namespace Redpoint.Windows.HCS.v1
{
    using Newtonsoft.Json;

    public class ContainerProperties
    {
        [JsonProperty("Id")]
        public string? Id { get; set; }

        [JsonProperty("State")]
        public string? State { get; set; }

        [JsonProperty("Name")]
        public string? Name { get; set; }

        [JsonProperty("SystemType")]
        public string? SystemType { get; set; }

        [JsonProperty("RuntimeOsType")]
        public string? RuntimeOSType { get; set; }

        [JsonProperty("Owner")]
        public string? Owner { get; set; }

        [JsonProperty("SiloGUID")]
        public string? SiloGUID { get; set; }

        [JsonProperty("RuntimeID")]
        public string? RuntimeID { get; set; }

        [JsonProperty("IsRuntimeTemplate")]
        public bool IsRuntimeTemplate { get; set; }

        [JsonProperty("RuntimeImagePath")]
        public string? RuntimeImagePath { get; set; }

        [JsonProperty("Stopped")]
        public bool Stopped { get; set; }

        [JsonProperty("ExitType")]
        public string? ExitType { get; set; }

        [JsonProperty("AreUpdatesPending")]
        public bool AreUpdatesPending { get; set; }

        [JsonProperty("ObRoot")]
        public string? ObRoot { get; set; }
    }
}
