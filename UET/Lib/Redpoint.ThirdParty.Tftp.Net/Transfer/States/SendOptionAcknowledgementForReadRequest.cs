using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tftp.Net.Transfer.States;

namespace Tftp.Net.Transfer.States
{
    class SendOptionAcknowledgementForReadRequest : SendOptionAcknowledgementBase
    {
        public override Task OnAcknowledgement(Acknowledgement command)
        {
            if (command.BlockNumber == 0)
            {
                //We received an OACK, so let's get going ;)
                return Context.SetState(new Sending());
            }
            else
            {
                return Task.CompletedTask;
            }
        }
    }
}
