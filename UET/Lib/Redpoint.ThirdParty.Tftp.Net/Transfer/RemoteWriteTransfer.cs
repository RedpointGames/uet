using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tftp.Net.Channel;
using Tftp.Net.Transfer.States;
using Tftp.Net.Transfer;

namespace Tftp.Net.Transfer
{
    class RemoteWriteTransfer : TftpTransfer
    {
        public static async Task<RemoteWriteTransfer> CreateRemoteWriteTransferAsync(ITransferChannel connection, string filename)
        {
            var transfer = new RemoteWriteTransfer(connection, filename);
            await transfer.InitAsync(new StartOutgoingWrite());
            return transfer;
        }

        private RemoteWriteTransfer(ITransferChannel connection, string filename)
            : base(connection, filename)
        {
        }
    }
}
