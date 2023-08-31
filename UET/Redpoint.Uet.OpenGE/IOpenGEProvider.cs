namespace Redpoint.Uet.OpenGE
{
    using Redpoint.ApplicationLifecycle;
    using Redpoint.OpenGE.Component.Dispatcher.PreprocessorCacheAccessor;
    using System.Threading.Tasks;

    public interface IOpenGEProvider : IApplicationLifecycle, IPreprocessorCacheAccessor
    {
        Task<OpenGEEnvironmentInfo> GetOpenGEEnvironmentInfo();
    }
}
