namespace Redpoint.OpenGE.Agent
{
    public interface IOpenGEAgent
    {
        Task StartAsync();
        Task StopAsync();
    }
}