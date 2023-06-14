namespace Docker.Registry.DotNet.Models
{
    using System.Runtime.Serialization;

    [DataContract]
    public class Platform
    {
        /// <summary>
        ///     The architecture field specifies the CPU architecture, for example amd64 or ppc64le
        /// </summary>
        [DataMember(Name = "architecture")]
        public string Architecture { get; set; }

        /// <summary>
        ///     The os field specifies the operating system, for example linux or windows.
        /// </summary>
        [DataMember(Name = "os")]
        public string Os { get; set; }

        /// <summary>
        ///     The optional os.version field specifies the operating system version, for example 10.0.10586.
        /// </summary>
        [DataMember(Name = "os.version")]
        public string OsVersion { get; set; }

        /// <summary>
        ///     The optional os.features field specifies an array of strings, each listing a required OS feature (for example on
        ///     Windows win32k)
        /// </summary>
        [DataMember(Name = "os.features")]
        public string OsFeatures { get; set; }

        /// <summary>
        ///     The optional variant field specifies a variant of the CPU, for example armv6l to specify a particular CPU variant
        ///     of the ARM CPU.
        /// </summary>
        [DataMember(Name = "variant")]
        public string Variant { get; set; }

        /// <summary>
        ///     The optional features field specifies an array of strings, each listing a required CPU feature (for example sse4 or
        ///     aes).
        /// </summary>
        [DataMember(Name = "features")]
        public string[] Features { get; set; }
    }
}