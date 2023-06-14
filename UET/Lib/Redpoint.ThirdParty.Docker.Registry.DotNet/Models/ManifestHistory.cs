namespace Docker.Registry.DotNet.Models
{
    using System.Runtime.Serialization;

    [DataContract]
    public class ManifestHistory
    {
        [DataMember(Name = "v1Compatibility")]
        public string V1Compatibility { get; set; }
    }
}