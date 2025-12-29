using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Tftp.Net.Trace;

namespace Tftp.Net.Transfer.States
{
    class Sending : StateThatExpectsMessagesFromDefaultEndPoint
    {
        private byte[] lastData;
        private ushort lastBlockNumber;
        private long bytesSent = 0;
        private bool lastPacketWasSent = false;

        public override async Task OnStateEnter()
        {
            await base.OnStateEnter();
            lastData = new byte[Context.BlockSize];
            SendNextPacket(1);
        }

        public override async Task OnAcknowledgement(Acknowledgement command)
        {
            //Drop acknowledgments for other packets than the previous one
            if (command.BlockNumber != lastBlockNumber)
                return;

            //Notify our observers about our progress
            bytesSent += lastData.Length;
            await Context.RaiseOnProgress(bytesSent, CancellationToken.None);

            if (lastPacketWasSent)
            {
                //We're done here
                await Context.RaiseOnFinished(CancellationToken.None);
                await Context.SetState(new Closed());
            }
            else
            {
                SendNextPacket(Context.BlockCounterWrapping.CalculateNextBlockNumber(lastBlockNumber));
            }
        }

        public override Task OnError(Error command)
        {
            return Context.SetState(new ReceivedError(command));
        }

        public override Task OnCancel(TftpErrorPacket reason)
        {
            return Context.SetState(new CancelledByUser(reason));
        }

        #region Helper Methods
        private void SendNextPacket(ushort blockNumber)
        {
            if (Context.InputOutputStream == null)
                return;

            int packetLength = Context.InputOutputStream.Read(lastData, 0, lastData.Length);
            lastBlockNumber = blockNumber;

            if (packetLength != lastData.Length)
            {
                //This means we just sent the last packet
                lastPacketWasSent = true;
                Array.Resize(ref lastData, packetLength);
            }

            ITftpCommand dataCommand = new Data(blockNumber, lastData);
            SendAndRepeat(dataCommand);
        }

        #endregion
    }
}
