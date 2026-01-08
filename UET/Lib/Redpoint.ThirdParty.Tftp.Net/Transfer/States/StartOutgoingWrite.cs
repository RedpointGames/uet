using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Tftp.Net.Transfer.States
{
    class StartOutgoingWrite : BaseState
    {
        public override Task OnStart()
        {
            Context.FillOrDisableTransferSizeOption();
            return Context.SetState(new SendWriteRequest());
        }

        public override Task OnCancel(TftpErrorPacket reason)
        {
            return Context.SetState(new Closed());
        }
    }
}
