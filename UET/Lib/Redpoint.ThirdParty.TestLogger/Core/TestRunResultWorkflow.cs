// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger.Core
{
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

    public static class TestRunResultWorkflow
    {
        public static void Result(this ITestRun testRun, TestResultEventArgs resultEvent)
        {
            var parsedName = TestCaseNameParser.Parse(resultEvent.Result.TestCase.FullyQualifiedName);
            testRun.Store.Add(new TestResultInfo(
                resultEvent.Result,
                parsedName.NamespaceName,
                parsedName.TypeName,
                parsedName.MethodName));
        }
    }
}