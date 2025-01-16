// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger.Core
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    /// <summary>
    /// Logger configuration provided by the test platform and command line
    /// parameters.
    /// </summary>
    public class LoggerConfiguration
    {
        public const string LogFilePathKey = "LogFilePath";
        public const string LogFileNameKey = "LogFileName";
        private const string AssemblyToken = "{assembly}";
        private const string FrameworkToken = "{framework}";

        public LoggerConfiguration(Dictionary<string, string> config)
        {
            if (!config.ContainsKey(LogFilePathKey))
            {
                // If LogFilePath is not provided, expect LogFileName and TestRunDirectory to be available.
                // We'll construct a LogFilePath from those.
                if (!config.TryGetValue(LogFileNameKey, out var resultFileName) ||
                    !config.TryGetValue(DefaultLoggerParameterNames.TestRunDirectory, out var resultDir))
                {
                    throw new ArgumentException($"Expected {LogFileNameKey} or {DefaultLoggerParameterNames.TestRunDirectory} parameter.", nameof(config));
                }

                if (string.IsNullOrEmpty(resultFileName) ||
                    string.IsNullOrEmpty(resultDir))
                {
                    throw new ArgumentNullException($"Expected {LogFileNameKey} or {DefaultLoggerParameterNames.TestRunDirectory} to be non empty.", nameof(config));
                }

                config[LogFilePathKey] = Path.Combine(resultDir, resultFileName);
            }

            if (string.IsNullOrEmpty(config[LogFilePathKey]))
            {
                throw new ArgumentNullException($"Expected {LogFilePathKey} to be non empty.");
            }

            this.Values = config;
        }

        public string LogFilePath => this.Values[LogFilePathKey];

        public Dictionary<string, string> Values { get; }

        public string GetFormattedLogFilePath(TestRunConfiguration runConfiguration)
        {
            var logFilePath = this.LogFilePath;
            if (logFilePath.Contains(AssemblyToken))
            {
                var assemblyName = Path.GetFileNameWithoutExtension(runConfiguration.AssemblyPath);
                logFilePath = logFilePath.Replace(AssemblyToken, assemblyName);
            }

            if (logFilePath.Contains(FrameworkToken))
            {
                var framework = runConfiguration.TargetFramework.Replace(",Version=v", string.Empty).Replace(".", string.Empty);
                logFilePath = logFilePath.Replace(FrameworkToken, framework);
            }

            return logFilePath;
        }
    }
}