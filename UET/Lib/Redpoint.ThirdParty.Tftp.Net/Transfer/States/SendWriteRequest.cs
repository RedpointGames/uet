using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Tftp.Net.Transfer;
using Tftp.Net.Trace;

namespace Tftp.Net.Transfer.States
{
    class SendWriteRequest : StateWithNetworkTimeout
    {
        public override async Task OnStateEnter()
        {
            await base.OnStateEnter();
            SendRequest();
        }

        private void SendRequest()
        {
            WriteRequest request = new WriteRequest(Context.Filename, Context.TransferMode, Context.ProposedOptions.ToOptionList());
            SendAndRepeat(request);
        }

        public override async Task OnCommand(ITftpCommand command, System.Net.EndPoint endpoint)
        {
            if (command is OptionAcknowledgement)
            {
                TransferOptionSet acknowledged = new TransferOptionSet((command as OptionAcknowledgement).Options);
                Context.FinishOptionNegotiation(acknowledged);
                await BeginSendingTo(endpoint);
            }
            else
            if (command is Acknowledgement && (command as Acknowledgement).BlockNumber == 0)
            {
                Context.FinishOptionNegotiation(TransferOptionSet.NewEmptySet());
                await BeginSendingTo(endpoint);
            }
            else
            if (command is Error)
            {
                //The server denied our request
                Error error = (Error)command;
                await Context.SetState(new ReceivedError(error));
            }
            else
                await base.OnCommand(command, endpoint);
        }

        private Task BeginSendingTo(System.Net.EndPoint endpoint)
        {
            //Switch to the endpoint that we received from the server
            Context.GetConnection().RemoteEndpoint = endpoint;

            //Start sending packets
            return Context.SetState(new Sending());
        }

        public override Task OnCancel(TftpErrorPacket reason)
        {
            return Context.SetState(new CancelledByUser(reason));
        }
    }
}
