namespace Redpoint.ProcessExecution.Tests
{
    using Redpoint.ProcessExecution.Windows;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class InternalTests
    {
        /*
        [SkippableFact]
        [SupportedOSPlatform("windows5.1.2600")]
        public async Task CanApplyChrootToExistingProcess()
        {
            Skip.IfNot(OperatingSystem.IsWindows());

            var state = WindowsChroot.SetupChrootState(new Dictionary<char, string>
            {
                { 'I', Environment.CurrentDirectory },
            });
            WindowsChroot.ApplyChrootStateToExistingProcess(state, 36168);

            await Task.Delay(120000);
        }

        [SkippableFact]
        [SupportedOSPlatform("windows5.1.2600")]
        public void CanChangeDirectoryOfExistingProcess()
        {
            Skip.IfNot(OperatingSystem.IsWindows());

            var process = Process.GetProcessById(9844);
            var handle = process.Handle;

            WindowsWorkingDirectory.SetWorkingDirectoryOfAnotherProcess(
                new global::Windows.Win32.Foundation.HANDLE(handle),
                @"C:\Work\internal");
        }
        */
    }
}
