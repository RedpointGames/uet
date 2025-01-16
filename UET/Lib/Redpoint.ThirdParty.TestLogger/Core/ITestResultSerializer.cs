// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger.Core
{
    using System.Collections.Generic;

    public interface ITestResultSerializer
    {
        string Serialize(
            LoggerConfiguration loggerConfiguration,
            TestRunConfiguration runConfiguration,
            List<TestResultInfo> results,
            List<TestMessageInfo> messages);
    }
}
