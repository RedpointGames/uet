namespace Tftp.Net
{
    class Acknowledgement : ITftpCommand
    {
        public const ushort OpCode = 4;

        public ushort BlockNumber { get; private set; }

        public Acknowledgement(ushort blockNumber)
        {
            this.BlockNumber = blockNumber;
        }

        public Task Visit(ITftpCommandVisitor visitor)
        {
            return visitor.OnAcknowledgement(this);
        }
    }
}
