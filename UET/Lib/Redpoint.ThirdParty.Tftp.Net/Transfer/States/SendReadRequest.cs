using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Tftp.Net.Channel;
using System.Net;
using Tftp.Net.Transfer;
using Tftp.Net.Trace;

namespace Tftp.Net.Transfer.States
{
    class SendReadRequest : StateWithNetworkTimeout
    {
        public override async Task OnStateEnter()
        {
            await base.OnStateEnter();
            SendRequest(); //Send a read request to the server
        }

        private void SendRequest()
        {
            ReadRequest request = new ReadRequest(Context.Filename, Context.TransferMode, Context.ProposedOptions.ToOptionList());
            SendAndRepeat(request);
        }

        public override async Task OnCommand(ITftpCommand command, EndPoint endpoint)
        {
            if (command is Data || command is OptionAcknowledgement)
            {
                //The server acknowledged our read request.
                //Fix out remote endpoint
                Context.GetConnection().RemoteEndpoint = endpoint;
            }

            if (command is Data)
            {
                if (Context.NegotiatedOptions == null)
                    Context.FinishOptionNegotiation(TransferOptionSet.NewEmptySet());

                //Switch to the receiving state...
                ITransferState nextState = new Receiving();
                await Context.SetState(nextState);

                //...and let it handle the data packet
                await nextState.OnCommand(command, endpoint);
            }
            else if (command is OptionAcknowledgement)
            {
                //Check which options were acknowledged
                Context.FinishOptionNegotiation(new TransferOptionSet((command as OptionAcknowledgement).Options));

                //the server acknowledged our options. Confirm the final options
                SendAndRepeat(new Acknowledgement(0));
            }
            else if (command is Error)
            {
                await Context.SetState(new ReceivedError((Error)command));
            }
            else
                await base.OnCommand(command, endpoint);
        }

        public override Task OnCancel(TftpErrorPacket reason)
        {
            return Context.SetState(new CancelledByUser(reason));
        }
    }
}
