namespace Tftp.Net
{
    class Error : ITftpCommand
    {
        public const ushort OpCode = 5;

        public ushort ErrorCode { get; private set; }
        public String Message { get; private set; }

        public Error(ushort errorCode, String message)
        {
            this.ErrorCode = errorCode;
            this.Message = message;
        }

        public Task Visit(ITftpCommandVisitor visitor)
        {
            return visitor.OnError(this);
        }
    }
}
