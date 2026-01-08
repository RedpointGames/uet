namespace Tftp.Net
{
    class ReadRequest : ReadOrWriteRequest, ITftpCommand
    {
        public const ushort OpCode = 1;

        public ReadRequest(String filename, TftpTransferMode mode, IEnumerable<TransferOption> options)
            : base(OpCode, filename, mode, options) { }

        public Task Visit(ITftpCommandVisitor visitor)
        {
            return visitor.OnReadRequest(this);
        }
    }
}
