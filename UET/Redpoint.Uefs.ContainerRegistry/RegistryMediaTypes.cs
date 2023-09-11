namespace Redpoint.Uefs.ContainerRegistry
{
    /// <summary>
    /// Various media type and constants used by UEFS when interacting with Docker registries.
    /// </summary>
    public static class RegistryConstants
    {
        /// <summary>
        /// A VHD stored directly in the Docker registry.
        /// </summary>
        public const string MediaTypePackageVHD = "uefs.redpoint.games/package.vhd";

        /// <summary>
        /// A sparse image stored directly in the Docker registry.
        /// </summary>
        public const string MediaTypePackageSparseImage = "uefs.redpoint.games/package.sparseimage";

        /// <summary>
        /// A reference to a VHD stored on a network share, where the reference pointer is stored in the Docker registry.
        /// </summary>
        public const string MediaTypePackageReferenceVHD = "uefs.redpoint.games/package.reference-vhd";

        /// <summary>
        /// A reference to a sparse image stored on a network share, where the reference pointer is stored in the Docker registry.
        /// </summary>
        public const string MediaTypePackageReferenceSparseImage = "uefs.redpoint.games/package.reference-sparseimage";

        /// <summary>
        /// A legacy package reference stored in a Docker registry (where it is assumed to be a VHD).
        /// </summary>
        public const string MediaTypeLegacyPackageReference = "uefs.redpoint.games/package.reference";

        /// <summary>
        /// The media type used for registry manifest lists (version 2).
        /// </summary>
        public const string MediaTypeManifestListV2 = "application/vnd.docker.distribution.manifest.list.v2+json";

        /// <summary>
        /// The media type used for a registry manifest (version 2).
        /// </summary>
        public const string MediaTypeManifestV2 = "application/vnd.docker.distribution.manifest.v2+json";

        /// <summary>
        /// The media type used for an image manifest (version 1).
        /// </summary>
        public const string MediaTypeManifestImageV1 = "application/vnd.docker.container.image.v1+json";

        /// <summary>
        /// The file extension used for VHDs.
        /// </summary>
        public const string FileExtensionVHD = ".vhd";

        /// <summary>
        /// The file extension used for sparse images.
        /// </summary>
        public const string FileExtensionSparseImage = ".sparseimage";

        /// <summary>
        /// The platform constant used by Docker registries used for Windows packages/containers.
        /// </summary>
        public const string PlatformWindows = "windows";

        /// <summary>
        /// The platform constant used by Docker registries used for macOS packages/containers.
        /// </summary>
        public const string PlatformMacOS = "darwin";
    }
}
