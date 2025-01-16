// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger.Extensions
{
    using System.Collections.Generic;
    using Spekt.TestLogger.Core;

    public class MSTestAdapter : ITestAdapter
    {
        public List<TestResultInfo> TransformResults(List<TestResultInfo> results, List<TestMessageInfo> messages)
        {
            // MS Test puts test parameters in the DisplayName and not in the FullyQualifiedName.
            // So we use the DisplayName whenever it is available.
            foreach (var result in results)
            {
                string displayName = result.Result.DisplayName;
                string method = result.Method;

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    // Preserving method because result display name was empty
                }
                else if (method != displayName)
                {
                    result.Method = displayName;
                }
            }

            return results;
        }
    }
}
