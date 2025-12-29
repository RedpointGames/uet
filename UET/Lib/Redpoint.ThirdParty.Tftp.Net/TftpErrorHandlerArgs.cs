namespace Tftp.Net
{
    public class TftpErrorHandlerArgs
    {
        public required ITftpTransfer Transfer { get; init; }

        public required TftpTransferError Error { get; init; }
    }
}
