using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tftp.Net
{
    public class TftpTransferProgress
    {
        /// <summary>
        /// Number of bytes that have already been transferred.
        /// </summary>
        public long TransferredBytes { get; private set; }

        /// <summary>
        /// Total number of bytes being transferred. May be 0 if unknown.
        /// </summary>
        public long TotalBytes { get; private set; }

        public TftpTransferProgress(long transferred, long total)
        {
            TransferredBytes = transferred;
            TotalBytes = total;
        }

        public override string ToString()
        {
            if (TotalBytes > 0)
                return (TransferredBytes * 100L) / TotalBytes + "% completed";
            else
                return TransferredBytes + " bytes transferred";
        }
    }
}
