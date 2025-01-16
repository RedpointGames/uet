// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger.Core
{
    using System;
    using System.Linq;
    using System.Xml;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;

    public static class TestRunStartWorkflow
    {
        public static TestRunConfiguration Start(this ITestRun testRun, TestRunStartEventArgs startedEvent)
        {
            // Extract assembly path and adapter from test run criteria
            // TODO validate if the testcase filter or running specific tests is going to break this!
            var assemblyPath = startedEvent.TestRunCriteria.Sources.First();

            // Extract target framework from run settings
            var runSettings = new XmlDocument();
            runSettings.LoadXml(startedEvent.TestRunCriteria.TestRunSettings);
            var framework = runSettings
                .GetElementsByTagName("TargetFrameworkVersion")[0]
                .InnerText;

            return new TestRunConfiguration
            {
                AssemblyPath = assemblyPath,
                TargetFramework = framework,
                StartTime = DateTime.UtcNow
            };
        }
    }
}