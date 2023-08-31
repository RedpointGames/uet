namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    public enum CoreFulfillerConstraint
    {
        /// <summary>
        /// Include both requests that require a local core and those that can be remoted.
        /// </summary>
        All,

        /// <summary>
        /// Include only local-required requests.
        /// </summary>
        LocalRequiredOnly,

        /// <summary>
        /// Include only local-required and local-preferred requests.
        /// </summary>
        LocalRequiredAndPreferred,

        /// <summary>
        /// Include only local-preferred and remote-preferred requests.
        /// </summary>
        LocalPreferredAndRemote,
    }
}
