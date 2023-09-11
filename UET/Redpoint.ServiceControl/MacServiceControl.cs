namespace Redpoint.ServiceControl
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Threading.Tasks;
    using System.Xml;

    [SupportedOSPlatform("macos")]
    internal class MacServiceControl : IServiceControl
    {
        [DllImport("libc")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
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
                var programArguments = kv["ProgramArguments"] as string[] ?? Array.Empty<string>();
                var programArgumentsString = string.Join(" ", programArguments.Select(x => $"'{x.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'"));
                return Task.FromResult($"'{program.Replace("'", "'\"'\"'", StringComparison.Ordinal)}' {programArguments}".Trim());
            }
        }

        public async Task InstallService(string name, string description, string executableAndArguments, string? stdoutLogPath, string? stderrLogPath)
        {
            using (var writer = XmlWriter.Create(
                $"/Library/LaunchDaemons/{name}.plist",
                new XmlWriterSettings { Async = true, Indent = true, IndentChars = "  " }))
            {
                await writer.WriteStartDocumentAsync().ConfigureAwait(false);

                await writer.WriteDocTypeAsync("plist", "-//Apple Computer//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null).ConfigureAwait(false);

                await writer.WriteStartElementAsync(null, "plist", null).ConfigureAwait(false);
                await writer.WriteAttributeStringAsync(null, "version", null, "1.0").ConfigureAwait(false);

                await writer.WriteStartElementAsync(null, "dict", null).ConfigureAwait(false);

                await writer.WriteElementStringAsync(null, "key", null, "Label").ConfigureAwait(false);
                await writer.WriteElementStringAsync(null, "string", null, name).ConfigureAwait(false);
                await writer.WriteElementStringAsync(null, "key", null, "ServiceDescription").ConfigureAwait(false);
                await writer.WriteElementStringAsync(null, "string", null, description).ConfigureAwait(false);

                await writer.WriteElementStringAsync(null, "key", null, "IsManagedByRedpointServiceControl").ConfigureAwait(false);
                await writer.WriteStartElementAsync(null, "true", null).ConfigureAwait(false);
                await writer.WriteEndElementAsync().ConfigureAwait(false);

                await writer.WriteElementStringAsync(null, "key", null, "RunAtLoad").ConfigureAwait(false);
                await writer.WriteStartElementAsync(null, "true", null).ConfigureAwait(false);
                await writer.WriteEndElementAsync().ConfigureAwait(false);

                await writer.WriteElementStringAsync(null, "key", null, "KeepAlive").ConfigureAwait(false);
                await writer.WriteStartElementAsync(null, "dict", null).ConfigureAwait(false);
                await writer.WriteElementStringAsync(null, "key", null, "SuccessfulExit").ConfigureAwait(false);
                await writer.WriteStartElementAsync(null, "false", null).ConfigureAwait(false);
                await writer.WriteEndElementAsync().ConfigureAwait(false);
                await writer.WriteEndElementAsync().ConfigureAwait(false);

                await writer.WriteElementStringAsync(null, "key", null, "Program").ConfigureAwait(false);
                await writer.WriteElementStringAsync(null, "string", null, "/bin/bash").ConfigureAwait(false);
                await writer.WriteElementStringAsync(null, "key", null, "ProgramArguments").ConfigureAwait(false);
                await writer.WriteStartElementAsync(null, "array", null).ConfigureAwait(false);
                await writer.WriteElementStringAsync(null, "string", null, "/bin/bash").ConfigureAwait(false);
                await writer.WriteElementStringAsync(null, "string", null, "-c").ConfigureAwait(false);
                await writer.WriteElementStringAsync(null, "string", null, executableAndArguments).ConfigureAwait(false);
                await writer.WriteEndElementAsync().ConfigureAwait(false);

                if (stdoutLogPath != null)
                {
                    await writer.WriteElementStringAsync(null, "key", null, "StandardOutPath").ConfigureAwait(false);
                    await writer.WriteElementStringAsync(null, "string", null, stdoutLogPath).ConfigureAwait(false);
                }

                if (stderrLogPath != null)
                {
                    await writer.WriteElementStringAsync(null, "key", null, "StandardErrorPath").ConfigureAwait(false);
                    await writer.WriteElementStringAsync(null, "string", null, stderrLogPath).ConfigureAwait(false);
                }

                await writer.WriteEndElementAsync().ConfigureAwait(false);

                await writer.WriteEndElementAsync().ConfigureAwait(false);

                await writer.WriteEndDocumentAsync().ConfigureAwait(false);
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
            })!.WaitForExitAsync().ConfigureAwait(false);

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
            })!.WaitForExitAsync().ConfigureAwait(false);
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
            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            if (output.Contains("pid = ", StringComparison.Ordinal))
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
            await process.WaitForExitAsync().ConfigureAwait(false);
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
            await process.WaitForExitAsync().ConfigureAwait(false);
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
            })!.WaitForExitAsync().ConfigureAwait(false);

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
            })!.WaitForExitAsync().ConfigureAwait(false);

            File.Delete($"/Library/LaunchDaemons/{name}.plist");
        }
    }
}
