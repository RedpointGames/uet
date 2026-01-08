using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tftp.Net.Channel;
using System.Net;

namespace Tftp.Net.Transfer.States
{
    class StateThatExpectsMessagesFromDefaultEndPoint : StateWithNetworkTimeout, ITftpCommandVisitor
    {
        public override async Task OnCommand(ITftpCommand command, EndPoint endpoint)
        {
            if (!endpoint.Equals(Context.GetConnection().RemoteEndpoint))
                throw new Exception("Received message from illegal endpoint. Actual: " + endpoint + ". Expected: " + Context.GetConnection().RemoteEndpoint);

            await command.Visit(this);
        }

        public virtual Task OnReadRequest(ReadRequest command)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnWriteRequest(WriteRequest command)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnData(Data command)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnAcknowledgement(Acknowledgement command)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnError(Error command)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnOptionAcknowledgement(OptionAcknowledgement command)
        {
            return Task.CompletedTask;
        }
    }
}
