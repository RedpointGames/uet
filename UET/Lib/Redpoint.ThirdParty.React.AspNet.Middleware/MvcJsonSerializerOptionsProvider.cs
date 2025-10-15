/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace React.AspNet
{
    internal class MvcJsonSerializerOptionsProvider : IJsonSerializerOptionsProvider
    {
        private readonly IOptions<JsonOptions> _jsonOptions;

        public MvcJsonSerializerOptionsProvider(IOptions<JsonOptions> jsonOptions)
        {
            _jsonOptions = jsonOptions;
        }

        public JsonSerializerOptions Options => _jsonOptions.Value.JsonSerializerOptions;
    }
}
