namespace Tftp.Net
{
    class Data : ITftpCommand
    {
        public const ushort OpCode = 3;

        public ushort BlockNumber { get; private set; }
        public byte[] Bytes { get; private set; }

        public Data(ushort blockNumber, byte[] data)
        {
            this.BlockNumber = blockNumber;
            this.Bytes = data;
        }

        public Task Visit(ITftpCommandVisitor visitor)
        {
            return visitor.OnData(this);
        }
    }
}
