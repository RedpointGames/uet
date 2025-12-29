using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Tftp.Net.Channel;
using System.Net;

namespace Tftp.Net.Transfer.States
{
    interface ITransferState
    {
        TftpTransfer Context { get; set; }

        //Called by TftpTransfer
        Task OnStateEnter();

        //Called if the user calls TftpTransfer.Start() or TftpTransfer.Cancel()
        Task OnStart();
        Task OnCancel(TftpErrorPacket reason);

        //Called regularely by the context
        Task OnTimer();

        //Called when a command is received
        Task OnCommand(ITftpCommand command, EndPoint endpoint);
    }
}
