using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tftp.Net.Trace;

namespace Tftp.Net.Transfer.States
{
    class ReceivedError : BaseState
    {
        private readonly TftpTransferError error;

        public ReceivedError(Error error)
            : this(new TftpErrorPacket(error.ErrorCode, GetNonEmptyErrorMessage(error))) { }

        private static string GetNonEmptyErrorMessage(Error error)
        {
            return string.IsNullOrEmpty(error.Message) ? "(No error message provided)" : error.Message;
        }

        public ReceivedError(TftpTransferError error)
        {
            this.error = error;
        }

        public override async Task OnStateEnter()
        {
            TftpTrace.Trace("Received error: " + error, Context);
            await Context.RaiseOnError(error, CancellationToken.None);
            await Context.SetState(new Closed());
        }
    }
}
