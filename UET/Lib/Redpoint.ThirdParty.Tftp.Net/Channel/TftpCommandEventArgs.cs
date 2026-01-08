using System.Net;

namespace Tftp.Net.Channel
{
    internal class TftpCommandEventArgs
    {
        public required ITftpCommand Command { get; init; }

        public required EndPoint Endpoint { get; init; }
    }
}
