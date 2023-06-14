namespace Docker.Registry.DotNet.Models
{
    // https://docs.docker.com/registry/spec/manifest-v2-1/#signed-manifests

    public static class ManifestMediaTypes
    {
        /// <summary>
        ///     schema1 (existing manifest format). Note that “application/json” will also be accepted for schema 1.
        /// </summary>
        public const string ManifestSchema1 =
            "application/vnd.docker.distribution.manifest.v1+json";

        /// <summary>
        ///     schema1 (existing manifest format) signed.
        /// </summary>
        public const string ManifestSchema1Signed =
            "application/vnd.docker.distribution.manifest.v1+prettyjws";

        /// <summary>
        ///     New image manifest format (schemaVersion = 2)
        /// </summary>
        public const string ManifestSchema2 =
            "application/vnd.docker.distribution.manifest.v2+json";

        /// <summary>
        ///     Manifest list, aka “fat manifest”
        /// </summary>
        public const string ManifestList =
            "application/vnd.docker.distribution.manifest.list.v2+json";

        /// <summary>
        ///     Container config JSON
        /// </summary>
        public const string ContainerConfig = "application/vnd.docker.container.image.v1+json";

        /// <summary>
        ///     “Layer”, as a gzipped tar
        /// </summary>
        public const string GzippedTar = "application/vnd.docker.image.rootfs.diff.tar.gzip";

        /// <summary>
        ///     “Layer”, as a gzipped tar that should never be pushed
        /// </summary>
        public const string GzippedLayer =
            "application/vnd.docker.image.rootfs.foreign.diff.tar.gzip";

        /// <summary>
        ///     Plugin config JSON
        /// </summary>
        public const string PluginConfigJson = "application/vnd.docker.plugin.v1+json";
    }
}