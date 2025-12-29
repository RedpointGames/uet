namespace Tftp.Net
{
    class WriteRequest : ReadOrWriteRequest, ITftpCommand
    {
        public const ushort OpCode = 2;

        public WriteRequest(String filename, TftpTransferMode mode, IEnumerable<TransferOption> options)
            : base(OpCode, filename, mode, options) { }

        public Task Visit(ITftpCommandVisitor visitor)
        {
            return visitor.OnWriteRequest(this);
        }
    }
}
