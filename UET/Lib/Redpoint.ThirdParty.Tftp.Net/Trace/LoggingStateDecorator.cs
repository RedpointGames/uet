using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Tftp.Net.Transfer.States;
using System.IO;
using Tftp.Net.Channel;
using System.Net;
using Tftp.Net.Trace;
using Tftp.Net.Transfer;

namespace Tftp.Net.Trace
{
    class LoggingStateDecorator : ITransferState
    {
        public TftpTransfer Context
        {
            get { return decoratee.Context; }
            set { decoratee.Context = value; }
        }

        private readonly ITransferState decoratee;
        private readonly TftpTransfer transfer;

        public LoggingStateDecorator(ITransferState decoratee, TftpTransfer transfer)
        {
            this.decoratee = decoratee;
            this.transfer = transfer;
        }

        public String GetStateName()
        {
            return "[" + decoratee.GetType().Name + "]";
        }

        public Task OnStateEnter()
        {
            TftpTrace.Trace(GetStateName() + " OnStateEnter", transfer);
            return decoratee.OnStateEnter();
        }

        public Task OnStart()
        {
            TftpTrace.Trace(GetStateName() + " OnStart", transfer);
            return decoratee.OnStart();
        }

        public Task OnCancel(TftpErrorPacket reason)
        {
            TftpTrace.Trace(GetStateName() + " OnCancel: " + reason, transfer);
            return decoratee.OnCancel(reason);
        }

        public Task OnCommand(ITftpCommand command, EndPoint endpoint)
        {
            TftpTrace.Trace(GetStateName() + " OnCommand: " + command + " from " + endpoint, transfer);
            return decoratee.OnCommand(command, endpoint);
        }

        public Task OnTimer()
        {
            return decoratee.OnTimer();
        }
    }
}
