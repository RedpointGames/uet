// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger.Core
{
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

    /// <summary>
    /// A message generated during the test run.
    /// </summary>
    public class TestMessageInfo
    {
        public TestMessageLevel Level { get; set; }

        public string Message { get; set; }
    }
}