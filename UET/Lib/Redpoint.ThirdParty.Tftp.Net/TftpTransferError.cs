using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tftp.Net
{
    /// <summary>
    /// Base class for all errors that may be passed to <code>ITftpTransfer.OnError</code>.
    /// </summary>
    public abstract class TftpTransferError
    {
        public abstract override String ToString();
    }

    /// <summary>
    /// Errors that are sent from the remote party using the TFTP Error Packet are represented
    /// by this class.
    /// </summary>
    public class TftpErrorPacket : TftpTransferError
    {
        /// <summary>
        /// Error code that was sent from the other party.
        /// </summary>
        public ushort ErrorCode { get; private set; }

        /// <summary>
        /// Error description that was sent by the other party.
        /// </summary>
        public string ErrorMessage { get; private set; }

        public TftpErrorPacket(ushort errorCode, string errorMessage)
        {
            if (String.IsNullOrEmpty(errorMessage))
                throw new ArgumentException("You must provide an errorMessage.");

            this.ErrorCode = errorCode;
            this.ErrorMessage = errorMessage;
        }

        public override string ToString()
        {
            return ErrorCode + " - " + ErrorMessage;
        }

        #region Predefined error packets from RFC 1350
        public static readonly TftpErrorPacket FileNotFound = new TftpErrorPacket(1, "File not found");
        public static readonly TftpErrorPacket AccessViolation = new TftpErrorPacket(2, "Access violation");
        public static readonly TftpErrorPacket DiskFull = new TftpErrorPacket(3, "Disk full or allocation exceeded");
        public static readonly TftpErrorPacket IllegalOperation = new TftpErrorPacket(4, "Illegal TFTP operation");
        public static readonly TftpErrorPacket UnknownTransferId = new TftpErrorPacket(5, "Unknown transfer ID");
        public static readonly TftpErrorPacket FileAlreadyExists = new TftpErrorPacket(6, "File already exists");
        public static readonly TftpErrorPacket NoSuchUser = new TftpErrorPacket(7, "No such user");
        #endregion
    }

    /// <summary>
    /// Network errors (i.e. socket exceptions) are represented by this class.
    /// </summary>
    public class NetworkError : TftpTransferError
    {
        public Exception Exception { get; private set; }

        public NetworkError(Exception exception)
        {
            this.Exception = exception;
        }

        public override string ToString()
        {
            return Exception.ToString();
        }
    }

    /// <summary>
    /// $(ITftpTransfer.RetryTimeout) has been exceeded more than $(ITftpTransfer.RetryCount) times in a row.
    /// </summary>
    public class TimeoutError : TftpTransferError
    {
        private readonly TimeSpan RetryTimeout;
        private readonly int RetryCount;

        public TimeoutError(TimeSpan retryTimeout, int retryCount)
        {
            this.RetryTimeout = retryTimeout;
            this.RetryCount = retryCount;
        }

        public override string ToString()
        {
            return "Timeout error. RetryTimeout (" + RetryTimeout + ") violated more than " + RetryCount + " times in a row";
        }
    }
}
