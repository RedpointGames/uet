// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger.Core
{
    using System;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
    using Spekt.TestLogger.Extensions;
    using Spekt.TestLogger.Platform;

    public class TestRunBuilder : ITestRunBuilder
    {
        private readonly TestRun testRun;

        public TestRunBuilder()
        {
            this.testRun = new TestRun
            {
                RunConfiguration = new TestRunConfiguration(),
                AdapterFactory = new TestAdapterFactory()
            };
        }

        public ITestRunBuilder WithLoggerConfiguration(LoggerConfiguration configuration)
        {
            this.testRun.LoggerConfiguration = configuration;
            return this;
        }

        public ITestRunBuilder WithStore(ITestResultStore store)
        {
            this.testRun.Store = store ?? throw new ArgumentNullException(nameof(store));
            return this;
        }

        public ITestRunBuilder WithSerializer(ITestResultSerializer serializer)
        {
            this.testRun.Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            return this;
        }

        public ITestRunBuilder Subscribe(TestLoggerEvents loggerEvents)
        {
            ArgumentNullException.ThrowIfNull(loggerEvents);

            loggerEvents.TestRunStart += (_, eventArgs) =>
            {
                this.testRun.RunConfiguration = this.testRun.Start(eventArgs);
            };
            loggerEvents.TestRunMessage += (_, eventArgs) => this.testRun.Message(eventArgs);
            loggerEvents.TestResult += (_, eventArgs) => this.testRun.Result(eventArgs);
            loggerEvents.TestRunComplete += (_, eventArgs) => this.testRun.Complete(eventArgs);

            return this;
        }

        public ITestRunBuilder WithFileSystem(IFileSystem fileSystem)
        {
            this.testRun.FileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            return this;
        }

        public ITestRunBuilder WithConsoleOutput(IConsoleOutput consoleOutput)
        {
            this.testRun.ConsoleOutput = consoleOutput ?? throw new ArgumentNullException(nameof(consoleOutput));
            return this;
        }

        public ITestRun Build()
        {
            return this.testRun;
        }
    }
}