using System.Net;

namespace Tftp.Net
{
    public class TftpServerEventHandlerArgs
    {
        public required ITftpTransfer Transfer { get; init; }

        public required EndPoint EndPoint { get; init; }
    }
}

