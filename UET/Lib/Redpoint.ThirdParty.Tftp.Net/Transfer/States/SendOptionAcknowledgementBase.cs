using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tftp.Net.Transfer.States;
using Tftp.Net.Trace;

namespace Tftp.Net.Transfer
{
    class SendOptionAcknowledgementBase : StateThatExpectsMessagesFromDefaultEndPoint
    {
        public override async Task OnStateEnter()
        {
            await base.OnStateEnter();
            SendAndRepeat(new OptionAcknowledgement(Context.NegotiatedOptions.ToOptionList()));
        }

        public override Task OnError(Error command)
        {
            return Context.SetState(new ReceivedError(command));
        }

        public override Task OnCancel(TftpErrorPacket reason)
        {
            return Context.SetState(new CancelledByUser(reason));
        }
    }
}
