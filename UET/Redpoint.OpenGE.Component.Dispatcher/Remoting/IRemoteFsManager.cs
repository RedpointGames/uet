namespace Redpoint.OpenGE.Component.Dispatcher.Remoting
{
    using System.Threading.Tasks;

    internal interface IRemoteFsManager
    {
        Task<int> StartRemoteFsIfNeededAsync();
    }
}
