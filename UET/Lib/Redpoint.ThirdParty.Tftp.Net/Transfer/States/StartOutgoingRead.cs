using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Tftp.Net.Transfer.States
{
    class StartOutgoingRead : BaseState
    {
        public override Task OnStart()
        {
            return Context.SetState(new SendReadRequest());
        }

        public override Task OnCancel(TftpErrorPacket reason)
        {
            return Context.SetState(new Closed());
        }
    }
}
