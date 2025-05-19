namespace Redpoint.KubernetesManager.Models
{
    internal class CertificateRequirement
    {
        public string? Category { get; set; }

        public string? FilenameWithoutExtension { get; set; }

        public string? CommonName { get; set; }

        public string? Role { get; set; }

        public string[]? AdditionalSubjectNames { get; set; }
    }
}
