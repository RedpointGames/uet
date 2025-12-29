using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tftp.Net.Transfer.States;
using Tftp.Net.Channel;
using Tftp.Net.Transfer;

namespace Tftp.Net.Transfer
{
    class RemoteReadTransfer : TftpTransfer
    {
        public static async Task<RemoteReadTransfer> CreateRemoteReadTransferAsync(ITransferChannel connection, string filename)
        {
            var transfer = new RemoteReadTransfer(connection, filename);
            await transfer.InitAsync(new StartOutgoingRead());
            return transfer;
        }

        private RemoteReadTransfer(ITransferChannel connection, string filename)
            : base(connection, filename)
        {
        }

        public override long ExpectedSize
        {
            get { return base.ExpectedSize; }
            set { throw new NotSupportedException("You cannot set the expected size of a file that is remotely transferred to this system."); }
        }
    }
}
