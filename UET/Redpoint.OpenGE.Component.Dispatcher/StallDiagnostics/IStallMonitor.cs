namespace Redpoint.OpenGE.Component.Dispatcher.StallDiagnostics
{
    internal interface IStallMonitor : IAsyncDisposable
    {
        void MadeProgress();
    }
}
