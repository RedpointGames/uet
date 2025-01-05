namespace React.Core
{
    using JavaScriptEngineSwitcher.Core;

    /// <summary>
    /// 
    /// </summary>
    public interface IReactJsEngineInitProvider
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="engine"></param>
        void InitEngine(IJsEngine engine);
    }
}
