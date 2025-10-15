/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace React
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal class ReactAppAssetManifest
	{
        [JsonPropertyName("files")]
		public Dictionary<string, string> Files { get; set; }

        [JsonPropertyName("entrypoints")]
		public List<string> Entrypoints { get; set; }

		public static ReactAppAssetManifest LoadManifest(IReactSiteConfiguration config, IFileSystem fileSystem, ICache cache, bool useCacheRead)
		{
			string cacheKey = "REACT_APP_MANIFEST";

			if (useCacheRead)
			{
				var cachedManifest = cache.Get<ReactAppAssetManifest>(cacheKey);
				if (cachedManifest != null)
					return cachedManifest;
			}

			var manifestString = fileSystem.ReadAsString($"{config.ReactAppBuildPath}/asset-manifest.json");
			var manifest = JsonSerializer.Deserialize<ReactAppAssetManifest>(manifestString, ReactAppAssetManifestJsonSerializerContext.Default.ReactAppAssetManifest);

			cache.Set(cacheKey, manifest, TimeSpan.FromHours(1));
			return manifest;
		}
	}
}
