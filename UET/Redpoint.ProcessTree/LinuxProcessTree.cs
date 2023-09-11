namespace Redpoint.ProcessTree
{
    using System.Diagnostics;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("linux")]
    internal sealed partial class LinuxProcessTree : IProcessTree
    {
        public Process? GetParentProcess(int processId)
        {
            try
            {
                foreach (var line in File.ReadAllLines($"/proc/{processId}/status"))
                {
                    if (line.StartsWith("PPid:", StringComparison.Ordinal))
                    {
                        var ppidString = line["PPid:".Length..].Trim();
                        if (int.TryParse(ppidString, out var ppid))
                        {
                            return Process.GetProcessById(ppid);
                        }
                        return null;
                    }
                }
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        public Process? GetParentProcess()
        {
            return GetParentProcess(Environment.ProcessId);
        }

        public Process? GetParentProcess(Process process)
        {
            return GetParentProcess(process.Id);
        }
    }
}