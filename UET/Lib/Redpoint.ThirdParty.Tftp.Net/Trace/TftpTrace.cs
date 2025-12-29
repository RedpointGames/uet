using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tftp.Net.Transfer;

namespace Tftp.Net.Trace
{
    /// <summary>
    /// Class that controls all tracing in the TFTP module.
    /// </summary>
    public static class TftpTrace
    {
        /// <summary>
        /// Set this property to <code>false</code> to disable tracing.
        /// </summary>
        public static bool Enabled { get; set; }

        static TftpTrace()
        {
            Enabled = false;
        }

        internal static void Trace(String message, ITftpTransfer transfer)
        {
            if (!Enabled)
                return;

            System.Diagnostics.Trace.WriteLine(message, transfer.ToString());
        }
    }
}
