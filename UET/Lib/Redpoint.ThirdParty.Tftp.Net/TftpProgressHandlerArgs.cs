namespace Tftp.Net
{
    public class TftpProgressHandlerArgs
    {
        public required ITftpTransfer Transfer { get; init; }

        public required TftpTransferProgress Progress { get; init; }
    }
}
