using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;

namespace Tftp.Net.Transfer.States
{
    class BaseState : ITransferState
    {
        public TftpTransfer Context { get; set; }

        public virtual Task OnStateEnter()
        {
            //no-op
            return Task.CompletedTask;
        }

        public virtual Task OnStart()
        {
            return Task.CompletedTask;
        }

        public virtual Task OnCancel(TftpErrorPacket reason)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnCommand(ITftpCommand command, EndPoint endpoint)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnTimer()
        {
            //Ignore timer events
            return Task.CompletedTask;
        }
    }
}
