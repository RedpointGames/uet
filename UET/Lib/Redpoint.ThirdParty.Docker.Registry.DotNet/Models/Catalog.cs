namespace Docker.Registry.DotNet.Models
{
    using System.Runtime.Serialization;

    [DataContract]
    public class Catalog
    {
        [DataMember(Name = "repositories", EmitDefaultValue = false)]
        public string[] Repositories { get; set; }
    }
}