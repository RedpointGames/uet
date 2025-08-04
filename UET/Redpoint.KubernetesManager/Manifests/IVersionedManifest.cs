namespace Redpoint.KubernetesManager.Manifests
{
    public interface IVersionedManifest
    {
        /// <summary>
        /// The version of this manifest when it was built. This is used to restart clients when they are out of date with
        /// the server.
        /// </summary>
        static abstract int ManifestCurrentVersion { get; }

        /// <summary>
        /// The version of this manifest. The manifest client checks if the manifest version matches our current build version
        /// and indicates the application should shutdown if it does not.
        /// </summary>
        int ManifestVersion { get; }
    }
}
