namespace Redpoint.OpenGE.Agent
{
    using Microsoft.Extensions.DependencyInjection;

    public static class AgentServiceExtensions
    {
        public static void AddOpenGEAgent(this IServiceCollection services)
        {
            services.AddSingleton<IOpenGEAgentFactory, DefaultOpenGEAgentFactory>();
        }
    }
}
