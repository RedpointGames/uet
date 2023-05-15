namespace AutomationRunner
{
    using System.Linq;
    using System.Text;
    using System.Xml;

    public static class JUnitConverter
    {
        public static void WriteTestResults(
            string projectName,
            string filenamePrefixToCut,
            FileInfo junitExport,
            TestResults results)
        {
            using (var writer = XmlWriter.Create(junitExport.FullName, new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  "
            }))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("testsuites");
                writer.WriteStartElement("testsuite");

                var tests = results.Results.Count;
                var skipped = results.Results.Count(x => x.State == TestState.NotRun);
                var failures = results.Results.Count(x => x.State == TestState.Fail);

                writer.WriteAttributeString("name", results.ClientDescriptor);
                writer.WriteAttributeString("tests", tests.ToString());
                writer.WriteAttributeString("skipped", skipped.ToString());
                writer.WriteAttributeString("failures", failures.ToString());
                writer.WriteAttributeString("errors", "0");
                writer.WriteAttributeString("time", results.TotalDuration.ToString());

                foreach (var test in results.Results)
                {
                    writer.WriteStartElement("testcase");

                    var testClassName = string.Empty;
                    var testName = string.Empty;
                    if (test.FullTestPath.EndsWith($".{test.TestDisplayName}"))
                    {
                        testClassName = test.FullTestPath.Substring(
                            0,
                            test.FullTestPath.Length - test.TestDisplayName.Length - 1);
                        testName = test.TestDisplayName;

                        if (!string.IsNullOrWhiteSpace(projectName) &&
                            testClassName.StartsWith($"{projectName}."))
                        {
                            testClassName = testClassName.Substring(projectName.Length + 1);
                        }
                    }
                    else
                    {
                        testClassName = string.Empty;
                        testName = test.FullTestPath;
                    }

                    writer.WriteAttributeString("classname", testClassName);
                    writer.WriteAttributeString("name", testName);

                    var stdout = new StringBuilder();

                    if (test.State == TestState.NotRun ||
                        test.State == TestState.InProcess)
                    {
                        writer.WriteStartElement("skipped");
                        writer.WriteEndElement();
                    }
                    else if (test.State == TestState.Fail &&
                             test.Entries.Count(x => x.Event.Type == "Error" || x.Event.Type == "Crash") == 0)
                    {
                        // We have to manually write a failure since this test failed, but
                        // we don't have any events that describe why.
                        writer.WriteStartElement("failure");
                        writer.WriteAttributeString("type", "Error");
                        writer.WriteAttributeString("message", "This test failed for an unknown reason (the test was in Fail state, but there were no reported error events). Refer to worker logs for more information.");
                        writer.WriteString("This test failed for an unknown reason (the test was in Fail state, but there were no reported error events). Refer to worker logs for more information.");
                        writer.WriteEndElement();
                    }

                    foreach (var entry in test.Entries)
                    {
                        var filename = entry.Filename;
                        if (!string.IsNullOrWhiteSpace(filenamePrefixToCut))
                        {
                            if (filename.ToLowerInvariant().StartsWith((filenamePrefixToCut + "\\").ToLowerInvariant()))
                            {
                                filename = filename.Substring(filenamePrefixToCut.Length + 1);
                                filename = Path.Combine(projectName, "Source", filename);
                            }
                            else
                            {
                                // Write-Warning "Source file '$($TestEntry.filename)' did not have expected prefix '$FilenamePrefixToCut\'"
                            }
                        }
                        filename = filename.Replace("\\", "/");

                        if (entry.Event.Type == "Error" || entry.Event.Type == "Crash")
                        {
                            writer.WriteStartElement("failure");
                            writer.WriteAttributeString("type", entry.Event.Type);
                            writer.WriteAttributeString("message", entry.Event.Message);
                            if (!string.IsNullOrWhiteSpace(filename) && entry.LineNumber > -1)
                            {
                                writer.WriteString($@"
ERROR: {entry.Event.Message}
File: {filename}
Line: {entry.LineNumber}
");
                            }
                            else
                            {
                                writer.WriteString($@"
ERROR: {entry.Event.Message}
No filename or line number is available.
");
                            }
                            writer.WriteEndElement();
                        }

                        if (entry.Event.Type == "Warning")
                        {
                            if (!string.IsNullOrWhiteSpace(filename) && entry.LineNumber > -1)
                            {
                                stdout.AppendLine($@"
ERROR: {entry.Event.Message}
File: {filename}
Line: {entry.LineNumber}
");
                            }
                            else
                            {
                                stdout.AppendLine($@"
ERROR: {entry.Event.Message}
No filename or line number is available.
");
                            }
                        }
                    }

                    var stdoutString = stdout.ToString().Trim();
                    if (stdoutString.Length > 0)
                    {
                        writer.WriteStartElement("system-out");
                        writer.WriteString(stdoutString);
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                }

                if (results.WorkerCrashes.Count > 0)
                {
                    writer.WriteStartElement("testcase");

                    writer.WriteAttributeString("classname", "");
                    writer.WriteAttributeString("name", "WorkerCrashes");

                    writer.WriteStartElement("failure");
                    writer.WriteAttributeString("type", "Error");
                    writer.WriteAttributeString("message", "One or more workers crashed while running tests. Due to the way Unreal Engine reports crashes on automation workers, these crashes can't be associated with a specific test.");
                    writer.WriteString(string.Join("\n\n", results.WorkerCrashes.Select(x =>
                    {
                        return @$"
Worker #{x.worker.WorkerNum} crashed:
{string.Join("\n", x.crashLogs)}";
                    })));
                    writer.WriteEndElement();

                    writer.WriteStartElement("system-out");
                    writer.WriteString(string.Join("\n\n", results.WorkerCrashes.Select(x =>
                    {
                        return @$"
Worker #{x.worker.WorkerNum} crashed:
{string.Join("\n", x.crashLogs)}";
                    })));
                    writer.WriteEndElement();

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndElement();
            }
        }
    }
}
