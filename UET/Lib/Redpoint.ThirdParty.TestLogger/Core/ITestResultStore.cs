// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger.Core
{
    using System.Collections.Generic;

    /// <summary>
    /// Store for test run results and messages.
    /// Thread safe implementation to allow concurrent operations.
    /// </summary>
    public interface ITestResultStore
    {
        void Add(TestResultInfo result);

        void Add(TestMessageInfo message);

        void Pop(out List<TestResultInfo> results, out List<TestMessageInfo> messages);
    }
}