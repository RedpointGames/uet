/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Text.Json;

namespace React
{
    /// <summary>
    /// A required interface that provides the <see cref="JsonSerializerOptions"/> that
    /// will be used for serialization. This is required so that the SSR and MVC serialize
    /// JSON in exactly the same way.
    /// </summary>
    public interface IJsonSerializerOptionsProvider
    {
        /// <summary>
        /// Provides the JSON options.
        /// </summary>
        public JsonSerializerOptions Options { get; }
    }
}
