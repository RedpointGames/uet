/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace React
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// JSON serializer context for <see cref="SourceMap"/>.
    /// </summary>
    [JsonSerializable(typeof(SourceMap))]
    public partial class SourceMapJsonSerializerContext : JsonSerializerContext
    {
    }
}
