namespace Tftp.Net
{
    interface ITftpCommandVisitor
    {
        Task OnReadRequest(ReadRequest command);
        Task OnWriteRequest(WriteRequest command);
        Task OnData(Data command);
        Task OnAcknowledgement(Acknowledgement command);
        Task OnError(Error command);
        Task OnOptionAcknowledgement(OptionAcknowledgement command);
    }
}
