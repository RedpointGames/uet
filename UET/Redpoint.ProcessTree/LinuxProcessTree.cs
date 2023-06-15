namespace Redpoint.ProcessTree
{
    using System.Diagnostics;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("linux")]
    internal partial class LinuxProcessTree : IProcessTree
    {
        public Process? GetParentProcess(int processId)
        {
            try
            {
                foreach (var line in File.ReadAllLines($"/proc/{processId}/status"))
                {
                    if (line.StartsWith("PPid:"))
                    {
                        var ppidString = line.Substring("PPid:".Length).Trim();
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
            return GetParentProcess(Process.GetCurrentProcess().Id);
        }

        public Process? GetParentProcess(Process process)
        {
            return GetParentProcess(process.Id);
        }
    }
}