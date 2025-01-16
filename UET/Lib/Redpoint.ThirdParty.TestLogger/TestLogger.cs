// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
    using Spekt.TestLogger.Core;
    using Spekt.TestLogger.Platform;

    /// <summary>
    /// Base test logger implementation.
    /// </summary>
    public abstract class TestLogger : ITestLoggerWithParameters
    {
        private readonly IFileSystem fileSystem;
        private readonly IConsoleOutput consoleOutput;
        private readonly ITestResultStore resultStore;
        private readonly ITestResultSerializer resultSerializer;
        private ITestRun testRun;

        protected TestLogger(ITestResultSerializer resultSerializer)
            : this(new FileSystem(), new ConsoleOutput(), new TestResultStore(), resultSerializer)
        {
        }

        protected TestLogger(
            IFileSystem fileSystem,
            IConsoleOutput consoleOutput,
            ITestResultStore resultStore,
            ITestResultSerializer resultSerializer)
        {
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.consoleOutput = consoleOutput ?? throw new ArgumentNullException(nameof(consoleOutput));
            this.resultStore = resultStore ?? throw new ArgumentNullException(nameof(resultStore));
            this.resultSerializer = resultSerializer ?? throw new ArgumentNullException(nameof(resultSerializer));
        }

        protected abstract string DefaultTestResultFile { get; }

        /// <inheritdoc/>
        /// <remarks>Overrides <see cref="ITestLogger.Initialize"/> method. Supports older runners.</remarks>
        public void Initialize(TestLoggerEvents events, string testResultsDirPath)
        {
            ArgumentNullException.ThrowIfNull(events);

            ArgumentNullException.ThrowIfNull(testResultsDirPath);

            var config = new Dictionary<string, string>
            {
                { DefaultLoggerParameterNames.TestRunDirectory, testResultsDirPath },
                { LoggerConfiguration.LogFilePathKey, Path.Combine(testResultsDirPath, this.DefaultTestResultFile) }
            };

            this.CreateTestRun(events, new LoggerConfiguration(config));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Overrides <c>ITestLoggerWithParameters.Initialize(TestLoggerEvents, Dictionary)</c> method.
        /// </remarks>
        public void Initialize(TestLoggerEvents events, Dictionary<string, string> parameters)
        {
            ArgumentNullException.ThrowIfNull(events);

            ArgumentNullException.ThrowIfNull(parameters);

            var config = new Dictionary<string, string>(parameters);

            // Set the default log file name if not provided by user
            if (!config.ContainsKey(LoggerConfiguration.LogFilePathKey) &&
                !config.ContainsKey(LoggerConfiguration.LogFileNameKey))
            {
                config[LoggerConfiguration.LogFileNameKey] = this.DefaultTestResultFile;
            }

            this.CreateTestRun(events, new LoggerConfiguration(config));
        }

        private void CreateTestRun(TestLoggerEvents events, LoggerConfiguration config)
        {
            this.testRun = new TestRunBuilder()
                .WithLoggerConfiguration(config)
                .WithFileSystem(this.fileSystem)
                .WithConsoleOutput(this.consoleOutput)
                .WithStore(this.resultStore)
                .WithSerializer(this.resultSerializer)
                .Subscribe(events)
                .Build();
        }
    }
}
