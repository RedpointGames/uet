using Redpoint.Concurrency;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Tftp.Net.Channel;
using Tftp.Net.Trace;
using Tftp.Net.Transfer;
using Tftp.Net.Transfer.States;

namespace Tftp.Net.Transfer
{
    class TftpTransfer : ITftpTransfer
    {
        protected ITransferState state;
        protected readonly ITransferChannel connection;
        protected Timer timer;

        public TransferOptionSet ProposedOptions { get; set; }
        public TransferOptionSet NegotiatedOptions { get; private set; }
        public bool WasStarted { get; private set; }
        public Stream InputOutputStream { get; protected set; }

        public static async Task<TftpTransfer> CreateTftpTransferAsync(ITransferChannel connection, String filename, ITransferState initialState)
        {
            var transfer = new TftpTransfer(connection, filename);
            await transfer.InitAsync(initialState);
            return transfer;
        }

        protected async Task InitAsync(ITransferState initialState)
        {
            this.ProposedOptions = TransferOptionSet.NewDefaultSet();
            this.RetryCount = 5;
            await this.SetState(initialState);
            this.connection.OnCommandReceived.Add(connection_OnCommandReceived);
            this.connection.OnError.Add(connection_OnError);
            this.connection.Open();
            this.timer = new Timer(timer_OnTimer, null, 500, 500);
        }

        protected TftpTransfer(ITransferChannel connection, String filename)
        {
            this.Filename = filename;
            this.connection = connection;
        }

        private void timer_OnTimer(object context)
        {
            try
            {
                lock (this)
                {
                    state.OnTimer();
                }
            }
            catch (Exception e)
            {
                TftpTrace.Trace("Ignoring unhandled exception: " + e, this);
            }
        }

        private Task connection_OnCommandReceived(TftpCommandEventArgs args, CancellationToken cancellationToken)
        {
            lock (this)
            {
                state.OnCommand(args.Command, args.Endpoint);
                return Task.CompletedTask;
            }
        }

        private async Task connection_OnError(TftpTransferError error, CancellationToken cancellationToken)
        {
            await RaiseOnError(error, cancellationToken);
        }

        internal virtual async Task SetState(ITransferState newState)
        {
            state = DecorateForLogging(newState);
            state.Context = this;
            await state.OnStateEnter();
        }

        protected virtual ITransferState DecorateForLogging(ITransferState state)
        {
            return TftpTrace.Enabled ? new LoggingStateDecorator(state, this) : state;
        }

        internal ITransferChannel GetConnection()
        {
            return connection;
        }

        internal async Task RaiseOnProgress(long bytesTransferred, CancellationToken cancellationToken)
        {
            await _onProgress.BroadcastAsync(
                new TftpProgressHandlerArgs
                {
                    Progress = new TftpTransferProgress(bytesTransferred, ExpectedSize),
                    Transfer = this,
                },
                cancellationToken);
        }

        internal async Task RaiseOnError(TftpTransferError error, CancellationToken cancellationToken)
        {
            await _onError.BroadcastAsync(
                new TftpErrorHandlerArgs
                {
                    Error = error,
                    Transfer = this,
                },
                cancellationToken);
        }

        internal async Task RaiseOnFinished(CancellationToken cancellationToken)
        {
            await _onFinished.BroadcastAsync(
                this,
                cancellationToken);
        }

        internal void FinishOptionNegotiation(TransferOptionSet negotiated)
        {
            NegotiatedOptions = negotiated;
            if (!NegotiatedOptions.IncludesBlockSizeOption)
                NegotiatedOptions.BlockSize = TransferOptionSet.DEFAULT_BLOCKSIZE;

            if (!NegotiatedOptions.IncludesTimeoutOption)
                NegotiatedOptions.Timeout = TransferOptionSet.DEFAULT_TIMEOUT_SECS;
        }

        public override string ToString()
        {
            return GetHashCode() + " (" + Filename + ")";
        }

        internal void FillOrDisableTransferSizeOption()
        {
            try
            {
                ProposedOptions.TransferSize = InputOutputStream.Length;
            }
            catch (NotSupportedException) { }
            finally
            {
                if (ProposedOptions.TransferSize <= 0)
                    ProposedOptions.IncludesTransferSizeOption = false;
            }
        }

        #region ITftpTransfer

        private readonly AsyncEvent<TftpProgressHandlerArgs> _onProgress = new();
        private readonly AsyncEvent<ITftpTransfer> _onFinished = new();
        private readonly AsyncEvent<TftpErrorHandlerArgs> _onError = new();

        public IAsyncEvent<TftpProgressHandlerArgs> OnProgress => _onProgress;
        public IAsyncEvent<ITftpTransfer> OnFinished => _onFinished;
        public IAsyncEvent<TftpErrorHandlerArgs> OnError => _onError;

        public string Filename { get; private set; }
        public int RetryCount { get; set; }
        public virtual TftpTransferMode TransferMode { get; set; }
        public object UserContext { get; set; }
        public virtual TimeSpan RetryTimeout
        {
            get { return TimeSpan.FromSeconds(NegotiatedOptions != null ? NegotiatedOptions.Timeout : ProposedOptions.Timeout); }
            set { ThrowExceptionIfTransferAlreadyStarted(); ProposedOptions.Timeout = value.Seconds; }
        }

        public virtual long ExpectedSize
        {
            get { return NegotiatedOptions != null ? NegotiatedOptions.TransferSize : ProposedOptions.TransferSize; }
            set { ThrowExceptionIfTransferAlreadyStarted(); ProposedOptions.TransferSize = value; }
        }

        public virtual int BlockSize
        {
            get { return NegotiatedOptions != null ? NegotiatedOptions.BlockSize : ProposedOptions.BlockSize; }
            set { ThrowExceptionIfTransferAlreadyStarted(); ProposedOptions.BlockSize = value; }
        }

        private BlockCounterWrapAround wrapping = BlockCounterWrapAround.ToZero;
        public virtual BlockCounterWrapAround BlockCounterWrapping
        {
            get { return wrapping; }
            set { ThrowExceptionIfTransferAlreadyStarted(); wrapping = value; }
        }

        private void ThrowExceptionIfTransferAlreadyStarted()
        {
            if (WasStarted)
                throw new InvalidOperationException("You cannot change tftp transfer options after the transfer has been started.");
        }

        public void Start(Stream data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            if (WasStarted)
                throw new InvalidOperationException("This transfer has already been started.");

            this.WasStarted = true;
            this.InputOutputStream = data;

            lock (this)
            {
                state.OnStart();
            }
        }

        public void Cancel(TftpErrorPacket reason)
        {
            if (reason == null)
                throw new ArgumentNullException("reason");

            lock (this)
            {
                state.OnCancel(reason);
            }
        }

        public virtual async ValueTask DisposeAsync()
        {
            lock (this)
            {
                timer.Dispose();
                Cancel(new TftpErrorPacket(0, "ITftpTransfer has been disposed."));

                if (InputOutputStream != null)
                {
                    InputOutputStream.Close();
                    InputOutputStream = null;
                }
            }

            await connection.DisposeAsync();
        }

        #endregion
    }
}
