namespace Docker.Registry.DotNet.Models
{
    using System.Runtime.Serialization;

    [DataContract]
    public class ListImageTagsResponse
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "tags")]
        public string[] Tags { get; set; }
    }
}