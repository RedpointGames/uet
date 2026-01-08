using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tftp.Net.Transfer.States
{
    class CancelledByUser : BaseState
    {
        private readonly TftpErrorPacket reason;

        public CancelledByUser(TftpErrorPacket reason)
        {
            this.reason = reason;
        }

        public override async Task OnStateEnter()
        {
            Error command = new Error(reason.ErrorCode, reason.ErrorMessage);
            Context.GetConnection().Send(command);
            await Context.SetState(new Closed());
        }
    }
}
