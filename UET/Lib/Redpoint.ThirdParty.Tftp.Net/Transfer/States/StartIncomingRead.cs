using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Tftp.Net.Transfer;

namespace Tftp.Net.Transfer.States
{
    class StartIncomingRead : BaseState
    {
        private readonly IEnumerable<TransferOption> optionsRequestedByClient;

        public StartIncomingRead(IEnumerable<TransferOption> optionsRequestedByClient)
        {
            this.optionsRequestedByClient = optionsRequestedByClient;
        }

        public override Task OnStateEnter()
        {
            Context.ProposedOptions = new TransferOptionSet(optionsRequestedByClient);
            return Task.CompletedTask;
        }

        public override async Task OnStart()
        {
            Context.FillOrDisableTransferSizeOption();
            Context.FinishOptionNegotiation(Context.ProposedOptions);
            List<TransferOption> options = Context.NegotiatedOptions.ToOptionList();
            if (options.Count > 0)
            {
                await Context.SetState(new SendOptionAcknowledgementForReadRequest());
            }
            else
            {
                //Otherwise just start sending
                await Context.SetState(new Sending());
            }
        }

        public override Task OnCancel(TftpErrorPacket reason)
        {
            return Context.SetState(new CancelledByUser(reason));
        }
    }
}
