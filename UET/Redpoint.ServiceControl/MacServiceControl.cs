namespace Redpoint.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;

    [SupportedOSPlatform("macos")]
    internal class MacServiceControl : IServiceControl
    {
        [DllImport("libc")]
        internal static extern uint geteuid();

        public bool HasPermissionToInstall => geteuid() == 0;

        public bool HasPermissionToStart => geteuid() == 0;

        public Task<bool> IsServiceInstalled(string name)
        {
            return Task.FromResult(File.Exists($"/Library/LaunchDaemons/{name}.plist"));
        }

        public Task<string> GetServiceExecutableAndArguments(string name)
        {
            var document = new XmlDocument();
            document.Load($"/Library/LaunchDaemons/{name}.plist");

            var kv = new Dictionary<string, object>();
            var dict = document.SelectSingleNode("//dict") as XmlElement;
            if (dict != null)
            {
                string? currentKey = null;
                foreach (var element in dict.ChildNodes.OfType<XmlElement>())
                {
                    if (element.Name == "key")
                    {
                        currentKey = element.InnerText.Trim();
                    }
                    else if (element.Name == "string" && currentKey != null)
                    {
                        kv.Add(currentKey, element.InnerText.Trim());
                    }
                    else if (element.Name == "true" && currentKey != null)
                    {
                        kv.Add(currentKey, true);
                    }
                    else if (element.Name == "false" && currentKey != null)
                    {
                        kv.Add(currentKey, false);
                    }
                    else if (element.Name == "array" && currentKey != null)
                    {
                        var entries = new List<string>();
                        foreach (var subelem in element.ChildNodes.OfType<XmlElement>())
                        {
                            if (subelem.Name == "string")
                            {
                                entries.Add(subelem.InnerText.Trim());
                            }
                        }
                        kv.Add(
                            currentKey,
                            entries.ToArray());
                    }
                }
            }

            if (kv.ContainsKey("IsManagedByRedpointServiceControl"))
            {
                // The executable and arguments are
                // always the second value in ProgramArguments.
                var programArguments = (string[])kv["ProgramArguments"];
                return Task.FromResult(programArguments[1]);
            }
            else
            {
                // Otherwise synthesize the command line.
                var program = kv["Program"] as string ?? string.Empty;
                var programArguments = kv["ProgramArguments"] as string[] ?? new string[0];
                var programArgumentsString = string.Join(" ", programArguments.Select(x => $"'{x.Replace("'", "'\"'\"'")}'"));
                return Task.FromResult($"'{program.Replace("'", "'\"'\"'")}' {programArguments}".Trim());
            }
        }

        public async Task InstallService(string name, string description, string executableAndArguments, string? stdoutLogPath, string? stderrLogPath)
        {
            using (var writer = XmlWriter.Create(
                $"/Library/LaunchDaemons/{name}.plist",
                new XmlWriterSettings { Async = true, Indent = true, IndentChars = "  " }))
            {
                await writer.WriteStartDocumentAsync();

                await writer.WriteDocTypeAsync("plist", "-//Apple Computer//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null);

                await writer.WriteStartElementAsync(null, "plist", null);
                await writer.WriteAttributeStringAsync(null, "version", null, "1.0");

                await writer.WriteStartElementAsync(null, "dict", null);

                await writer.WriteElementStringAsync(null, "key", null, "Label");
                await writer.WriteElementStringAsync(null, "string", null, name);
                await writer.WriteElementStringAsync(null, "key", null, "ServiceDescription");
                await writer.WriteElementStringAsync(null, "string", null, description);

                await writer.WriteElementStringAsync(null, "key", null, "IsManagedByRedpointServiceControl");
                await writer.WriteStartElementAsync(null, "true", null);
                await writer.WriteEndElementAsync();

                await writer.WriteElementStringAsync(null, "key", null, "RunAtLoad");
                await writer.WriteStartElementAsync(null, "true", null);
                await writer.WriteEndElementAsync();

                await writer.WriteElementStringAsync(null, "key", null, "KeepAlive");
                await writer.WriteStartElementAsync(null, "dict", null);
                await writer.WriteElementStringAsync(null, "key", null, "SuccessfulExit");
                await writer.WriteStartElementAsync(null, "false", null);
                await writer.WriteEndElementAsync();
                await writer.WriteEndElementAsync();

                await writer.WriteElementStringAsync(null, "key", null, "Program");
                await writer.WriteElementStringAsync(null, "string", null, "/bin/bash");
                await writer.WriteElementStringAsync(null, "key", null, "ProgramArguments");
                await writer.WriteStartElementAsync(null, "array", null);
                await writer.WriteElementStringAsync(null, "string", null, "/bin/bash");
                await writer.WriteElementStringAsync(null, "string", null, "-c");
                await writer.WriteElementStringAsync(null, "string", null, executableAndArguments);
                await writer.WriteEndElementAsync();

                if (stdoutLogPath != null)
                {
                    await writer.WriteElementStringAsync(null, "key", null, "StandardOutPath");
                    await writer.WriteElementStringAsync(null, "string", null, stdoutLogPath);
                }

                if (stderrLogPath != null)
                {
                    await writer.WriteElementStringAsync(null, "key", null, "StandardErrorPath");
                    await writer.WriteElementStringAsync(null, "string", null, stderrLogPath);
                }

                await writer.WriteEndElementAsync();

                await writer.WriteEndElementAsync();

                await writer.WriteEndDocumentAsync();
            }

            File.SetUnixFileMode(
                $"/Library/LaunchDaemons/{name}.plist",
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.GroupRead |
                UnixFileMode.OtherRead);

            await Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/launchctl",
                ArgumentList =
                {
                    "load",
                    "-w",
                    $"/Library/LaunchDaemons/{name}.plist"
                },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!.WaitForExitAsync();

            await Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/launchctl",
                ArgumentList =
                {
                    "enable",
                    $"system/{name}"
                },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!.WaitForExitAsync();
        }

        public async Task<bool> IsServiceRunning(string name)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/launchctl",
                ArgumentList =
                {
                    "print",
                    $"system/{name}"
                },
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            })!;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (output.Contains("pid = "))
            {
                return true;
            }
            return false;
        }

        public async Task StartService(string name)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/launchctl",
                ArgumentList =
                {
                    "start",
                    $"system/{name}"
                },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!;
            await process.WaitForExitAsync();
        }

        public async Task StopService(string name)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/launchctl",
                ArgumentList =
                {
                    "stop",
                    $"system/{name}"
                },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!;
            await process.WaitForExitAsync();
        }

        public async Task UninstallService(string name)
        {
            await Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/launchctl",
                ArgumentList =
                {
                    "disable",
                    $"system/{name}"
                },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!.WaitForExitAsync();

            await Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/launchctl",
                ArgumentList =
                {
                    "unload",
                    "-w",
                    $"/Library/LaunchDaemons/{name}.plist"
                },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!.WaitForExitAsync();

            File.Delete($"/Library/LaunchDaemons/{name}.plist");
        }
    }
}
