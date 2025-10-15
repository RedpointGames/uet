/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace React
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(ReactAppAssetManifest))]
    internal partial class ReactAppAssetManifestJsonSerializerContext : JsonSerializerContext
    {
    }
}
