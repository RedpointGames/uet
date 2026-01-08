using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Redpoint.Concurrency;

namespace Tftp.Net.Channel
{
    interface ITransferChannel : IAsyncDisposable
    {
        IAsyncEvent<TftpCommandEventArgs> OnCommandReceived { get; }
        IAsyncEvent<TftpTransferError> OnError { get; }

        EndPoint RemoteEndpoint { get; set; }

        void Open();
        void Send(ITftpCommand command);
    }
}
