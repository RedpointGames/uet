namespace Tftp.Net
{
    abstract class ReadOrWriteRequest
    {
        private readonly ushort opCode;

        public String Filename { get; private set; }
        public TftpTransferMode Mode { get; private set; }
        public IEnumerable<TransferOption> Options { get; private set; }

        protected ReadOrWriteRequest(ushort opCode, String filename, TftpTransferMode mode, IEnumerable<TransferOption> options)
        {
            this.opCode = opCode;
            this.Filename = filename;
            this.Mode = mode;
            this.Options = options;
        }
    }
}
