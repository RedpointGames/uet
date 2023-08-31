namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    public enum CoreAllocationPreference
    {
        /// <summary>
        /// The allocated core must be a local core.
        /// </summary>
        RequireLocal,

        /// <summary>
        /// Prefer a local core allocation.
        /// </summary>
        PreferLocal,

        /// <summary>
        /// Prefer a remote core allocation.
        /// </summary>
        PreferRemote,
    }
}
