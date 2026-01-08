using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Tftp.Net.Transfer.States
{
    class Receiving : StateThatExpectsMessagesFromDefaultEndPoint
    {
        private ushort lastBlockNumber = 0;
        private ushort nextBlockNumber = 1;
        private long bytesReceived = 0;

        public override async Task OnData(Data command)
        {
            if (command.BlockNumber == nextBlockNumber)
            {
                //We received a new block of data
                Context.InputOutputStream.Write(command.Bytes, 0, command.Bytes.Length);
                SendAcknowledgement(command.BlockNumber);

                //Was that the last block of data?
                if (command.Bytes.Length < Context.BlockSize)
                {
                    await Context.RaiseOnFinished(CancellationToken.None);
                    await Context.SetState(new Closed());
                }
                else
                {
                    lastBlockNumber = command.BlockNumber;
                    nextBlockNumber = Context.BlockCounterWrapping.CalculateNextBlockNumber(command.BlockNumber);
                    bytesReceived += command.Bytes.Length;
                    await Context.RaiseOnProgress(bytesReceived, CancellationToken.None);
                }
            }
            else
            if (command.BlockNumber == lastBlockNumber)
            {
                //We received the previous block again. Re-sent the acknowledgement
                SendAcknowledgement(command.BlockNumber);
            }
        }

        public override Task OnCancel(TftpErrorPacket reason)
        {
            return Context.SetState(new CancelledByUser(reason));
        }

        public override Task OnError(Error command)
        {
            return Context.SetState(new ReceivedError(command));
        }

        private void SendAcknowledgement(ushort blockNumber)
        {
            Acknowledgement ack = new Acknowledgement(blockNumber);
            Context.GetConnection().Send(ack);
            ResetTimeout();
        }
    }
}
