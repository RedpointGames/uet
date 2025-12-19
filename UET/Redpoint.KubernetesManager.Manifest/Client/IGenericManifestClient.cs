namespace Redpoint.KubernetesManager.Manifest.Client
{
    using Redpoint.KubernetesManager.Manifest;
    using System;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;

    public interface IGenericManifestClient
    {
        Task RegisterAndRunWithManifestAsync<T>(
            Uri uri,
            string? manifestCachePath,
            JsonTypeInfo<T> jsonTypeInfo,
            Func<T, CancellationToken, Task> runWithManifest,
            CancellationToken cancellationToken) where T : class, IVersionedManifest;
    }
}
