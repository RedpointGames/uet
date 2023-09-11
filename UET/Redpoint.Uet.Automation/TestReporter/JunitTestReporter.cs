namespace Redpoint.Uet.Automation.TestReporter
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Automation.Model;
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using System.Xml;

    internal class JunitTestReporter : ITestReporter
    {
        private readonly ILogger<JunitTestReporter> _logger;
        private readonly string _path;

        public JunitTestReporter(
            ILogger<JunitTestReporter> logger,
            string path)
        {
            _logger = logger;
            _path = path;
        }

        public async Task ReportResultsAsync(
            string projectName,
            TestResult[] results,
            TimeSpan duration,
            string filenamePrefixToCut)
        {
            _logger.LogTrace($"Writing test results in Junit format to: {_path}");
            using (var stream = new FileStream(_path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    Async = true,
                }))
                {
                    await writer.WriteStartDocumentAsync().ConfigureAwait(false);
                    await writer.WriteStartElementAsync(null, "testsuites", null).ConfigureAwait(false);
                    await writer.WriteStartElementAsync(null, "testsuite", null).ConfigureAwait(false);
                    await writer.WriteAttributeStringAsync(null, "name", null, "UET").ConfigureAwait(false);
                    await writer.WriteAttributeStringAsync(null, "tests", null, results.Length.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                    await writer.WriteAttributeStringAsync(null, "skipped", null, results.Count(x => x.TestStatus == TestResultStatus.Skipped).ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                    await writer.WriteAttributeStringAsync(null, "failures", null, results.Count(x => x.TestStatus != TestResultStatus.Skipped && x.TestStatus != TestResultStatus.Passed).ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                    await writer.WriteAttributeStringAsync(null, "errors", null, "0").ConfigureAwait(false);
                    await writer.WriteAttributeStringAsync(null, "time", null, duration.TotalSeconds.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);

                    foreach (var result in results)
                    {
                        await writer.WriteStartElementAsync(null, "testcase", null).ConfigureAwait(false);

                        var testClassName = string.Empty;
                        var testName = string.Empty;
                        if (result.FullTestPath.EndsWith("." + result.TestName, StringComparison.Ordinal))
                        {
                            testClassName = result.FullTestPath[..(result.FullTestPath.Length - result.TestName.Length - 1)];
                            testName = result.TestName;

                            if (testClassName.StartsWith(projectName + ".", StringComparison.Ordinal))
                            {
                                testClassName = testClassName[(projectName.Length + 1)..];
                            }
                        }
                        else
                        {
                            testClassName = string.Empty;
                            testName = result.FullTestPath;
                        }

                        await writer.WriteAttributeStringAsync(null, "classname", null, testClassName).ConfigureAwait(false);
                        await writer.WriteAttributeStringAsync(null, "name", null, testName).ConfigureAwait(false);

                        var stdout = new List<string>();

                        foreach (var entry in result.Entries)
                        {
                            var filename = entry.Filename;
                            filename = filename.Replace("\\", "/", StringComparison.Ordinal);
                            if (filename.StartsWith(filenamePrefixToCut.Replace("\\", "/", StringComparison.Ordinal) + "/", StringComparison.OrdinalIgnoreCase))
                            {
                                filename = filename[(filenamePrefixToCut.Length + 1)..];
                                filename = $"{projectName}/Source/{filename}";
                            }

                            if (entry.Category == TestResultEntryCategory.Error)
                            {
                                await writer.WriteStartElementAsync(null, "failure", null).ConfigureAwait(false);
                                await writer.WriteAttributeStringAsync(null, "type", null, entry.Category.ToString()).ConfigureAwait(false);
                                await writer.WriteAttributeStringAsync(null, "message", null, entry.Message.ToString()).ConfigureAwait(false);
                                if (!string.IsNullOrWhiteSpace(filename) || entry.LineNumber > -1)
                                {
                                    await writer.WriteStringAsync($@"
ERROR: {entry.Message}
File: {filename}
Line: {entry.LineNumber}
").ConfigureAwait(false);
                                }
                                else
                                {
                                    await writer.WriteStringAsync($@"
ERROR: {entry.Message}
No filename or line number is available.
").ConfigureAwait(false);
                                }
                                await writer.WriteEndElementAsync().ConfigureAwait(false);
                            }

                            if (entry.Category == TestResultEntryCategory.Warning)
                            {
                                if (!string.IsNullOrWhiteSpace(filename) || entry.LineNumber > -1)
                                {
                                    stdout.Add($@"
WARNING: {entry.Message}
File: {filename}
Line: {entry.LineNumber}
");
                                }
                                else
                                {
                                    stdout.Add($@"
ERROR: {entry.Message}
No filename or line number is available.
");
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(result.EngineCrashInfo))
                        {
                            await writer.WriteStartElementAsync(null, "failure", null).ConfigureAwait(false);
                            await writer.WriteAttributeStringAsync(null, "type", null, "Crash").ConfigureAwait(false);
                            await writer.WriteAttributeStringAsync(null, "message", null, result.EngineCrashInfo).ConfigureAwait(false);
                            await writer.WriteStringAsync(result.EngineCrashInfo).ConfigureAwait(false);
                            await writer.WriteEndElementAsync().ConfigureAwait(false);
                        }

                        if (result.AutomationRunnerCrashInfo != null)
                        {
                            await writer.WriteStartElementAsync(null, "failure", null).ConfigureAwait(false);
                            await writer.WriteAttributeStringAsync(null, "type", null, "Exception").ConfigureAwait(false);
                            await writer.WriteAttributeStringAsync(null, "message", null, result.AutomationRunnerCrashInfo.Message).ConfigureAwait(false);
                            await writer.WriteStringAsync(result.AutomationRunnerCrashInfo.ToString()).ConfigureAwait(false);
                            await writer.WriteEndElementAsync().ConfigureAwait(false);
                        }

                        if (stdout.Count > 0)
                        {
                            await writer.WriteStartElementAsync(null, "system-out", null).ConfigureAwait(false);
                            await writer.WriteStringAsync(string.Join("\n", stdout)).ConfigureAwait(false);
                            await writer.WriteEndElementAsync().ConfigureAwait(false);
                        }

                        await writer.WriteEndElementAsync().ConfigureAwait(false);
                    }

                    await writer.WriteEndElementAsync().ConfigureAwait(false);
                    await writer.WriteEndElementAsync().ConfigureAwait(false);
                    await writer.WriteEndDocumentAsync().ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
