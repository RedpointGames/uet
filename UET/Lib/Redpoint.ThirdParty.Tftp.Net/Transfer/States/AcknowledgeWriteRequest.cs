using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tftp.Net.Trace;

namespace Tftp.Net.Transfer.States
{
    class AcknowledgeWriteRequest : StateThatExpectsMessagesFromDefaultEndPoint
    {
        public override async Task OnStateEnter()
        {
            await base.OnStateEnter();
            SendAndRepeat(new Acknowledgement(0));
        }

        public override async Task OnData(Data command)
        {
            var nextState = new Receiving();
            await Context.SetState(nextState);
            await nextState.OnCommand(command, Context.GetConnection().RemoteEndpoint);
        }

        public override async Task OnCancel(TftpErrorPacket reason)
        {
            await Context.SetState(new CancelledByUser(reason));
        }

        public override async Task OnError(Error command)
        {
            await Context.SetState(new ReceivedError(command));
        }
    }
}
