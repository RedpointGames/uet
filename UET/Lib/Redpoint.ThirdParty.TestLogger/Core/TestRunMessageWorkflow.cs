// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger.Core
{
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

    public static class TestRunMessageWorkflow
    {
        public static void Message(this ITestRun testRun, TestRunMessageEventArgs messageEvent)
        {
            testRun.Store.Add(new TestMessageInfo { Level = messageEvent.Level, Message = messageEvent.Message });
        }
    }
}