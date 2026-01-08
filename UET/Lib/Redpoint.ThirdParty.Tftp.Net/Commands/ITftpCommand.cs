namespace Tftp.Net
{
    interface ITftpCommand
    {
        Task Visit(ITftpCommandVisitor visitor);
    }
}
