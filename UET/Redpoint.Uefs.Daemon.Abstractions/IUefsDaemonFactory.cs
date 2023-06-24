namespace Redpoint.Uefs.Daemon.Abstractions
{
    public interface IUefsDaemonFactory
    {
        IUefsDaemon CreateDaemon(string rootPath);
    }
}
