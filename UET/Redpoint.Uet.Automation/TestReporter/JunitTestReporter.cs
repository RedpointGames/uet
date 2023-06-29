namespace Redpoint.Uet.Automation.TestReporter
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Automation.Model;
    using System;
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
                    await writer.WriteStartDocumentAsync();
                    await writer.WriteStartElementAsync(null, "testsuites", null);
                    await writer.WriteStartElementAsync(null, "testsuite", null);
                    await writer.WriteAttributeStringAsync(null, "name", null, "UET");
                    await writer.WriteAttributeStringAsync(null, "tests", null, results.Length.ToString());
                    await writer.WriteAttributeStringAsync(null, "skipped", null, results.Count(x => x.TestStatus == TestResultStatus.Skipped).ToString());
                    await writer.WriteAttributeStringAsync(null, "failures", null, results.Count(x => x.TestStatus != TestResultStatus.Skipped && x.TestStatus != TestResultStatus.Passed).ToString());
                    await writer.WriteAttributeStringAsync(null, "errors", null, "0");
                    await writer.WriteAttributeStringAsync(null, "time", null, duration.TotalSeconds.ToString());

                    foreach (var result in results)
                    {
                        await writer.WriteStartElementAsync(null, "testcase", null);

                        var testClassName = string.Empty;
                        var testName = string.Empty;
                        if (result.FullTestPath.EndsWith("." + result.TestName))
                        {
                            testClassName = result.FullTestPath.Substring(0, result.FullTestPath.Length - result.TestName.Length - 1);
                            testName = result.TestName;

                            if (testClassName.StartsWith(projectName + "."))
                            {
                                testClassName = testClassName.Substring(projectName.Length + 1);
                            }
                        }
                        else
                        {
                            testClassName = string.Empty;
                            testName = result.FullTestPath;
                        }

                        await writer.WriteAttributeStringAsync(null, "classname", null, testClassName);
                        await writer.WriteAttributeStringAsync(null, "name", null, testName);

                        var stdout = new List<string>();

                        foreach (var entry in result.Entries)
                        {
                            var filename = entry.Filename;
                            filename = filename.Replace("\\", "/");
                            if (filename.ToLowerInvariant().StartsWith((filenamePrefixToCut.Replace("\\", "/") + "/").ToLowerInvariant()))
                            {
                                filename = filename.Substring(filenamePrefixToCut.Length + 1);
                                filename = $"{projectName}/Source/{filename}";
                            }

                            if (entry.Category == TestResultEntryCategory.Error)
                            {
                                await writer.WriteStartElementAsync(null, "failure", null);
                                await writer.WriteAttributeStringAsync(null, "type", null, entry.Category.ToString());
                                await writer.WriteAttributeStringAsync(null, "message", null, entry.Message.ToString());
                                if (!string.IsNullOrWhiteSpace(filename) || entry.LineNumber > -1)
                                {
                                    await writer.WriteStringAsync($@"
ERROR: {entry.Message}
File: {filename}
Line: {entry.LineNumber}
");
                                }
                                else
                                {
                                    await writer.WriteStringAsync($@"
ERROR: {entry.Message}
No filename or line number is available.
");
                                }
                                await writer.WriteEndElementAsync();
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
                            await writer.WriteStartElementAsync(null, "failure", null);
                            await writer.WriteAttributeStringAsync(null, "type", null, "Crash");
                            await writer.WriteAttributeStringAsync(null, "message", null, result.EngineCrashInfo);
                            await writer.WriteStringAsync(result.EngineCrashInfo);
                            await writer.WriteEndElementAsync();
                        }

                        if (result.AutomationRunnerCrashInfo != null)
                        {
                            await writer.WriteStartElementAsync(null, "failure", null);
                            await writer.WriteAttributeStringAsync(null, "type", null, "Exception");
                            await writer.WriteAttributeStringAsync(null, "message", null, result.AutomationRunnerCrashInfo.Message);
                            await writer.WriteStringAsync(result.AutomationRunnerCrashInfo.ToString());
                            await writer.WriteEndElementAsync();
                        }

                        if (stdout.Count > 0)
                        {
                            await writer.WriteStartElementAsync(null, "system-out", null);
                            await writer.WriteStringAsync(string.Join("\n", stdout));
                            await writer.WriteEndElementAsync();
                        }

                        await writer.WriteEndElementAsync();
                    }

                    await writer.WriteEndElementAsync();
                    await writer.WriteEndElementAsync();
                    await writer.WriteEndDocumentAsync();
                    await writer.FlushAsync();
                }
            }
        }
    }
}
