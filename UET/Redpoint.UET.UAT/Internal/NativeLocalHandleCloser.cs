namespace Redpoint.UET.UAT.Internal
{
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Windows.Win32.System.WindowsProgramming;

    internal class NativeLocalHandleCloser : ILocalHandleCloser
    {
        private const SYSTEM_INFORMATION_CLASS _systemHandleInformation = (SYSTEM_INFORMATION_CLASS)16;

        public Task CloseLocalHandles(string localPath)
        {
            /*
            foreach (var process in Process.GetProcesses())
            {
                nint handle = process.Handle;

                Windows.Win32.PInvoke.NtQuerySystemInformation(_systemHandleInformation, )
            }

            throw new NotImplementedException();
            */
            return Task.CompletedTask;
        }
    }
}
