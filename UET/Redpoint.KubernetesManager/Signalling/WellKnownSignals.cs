namespace Redpoint.KubernetesManager.Signalling
{
    internal static class WellKnownSignals
    {
        /// <summary>
        /// When RKM is about to start, allowing components to do simple checks before anything else is done.
        /// </summary>
        public const string PreflightChecks = "preflight-checks";

        /// <summary>
        /// When RKM starts. This signal fires as soon as RKM starts up.
        /// </summary>
        public const string Started = "started";

        /// <summary>
        /// When RKM is stopping. This signal allows you to clean up component
        /// state.
        /// </summary>
        public const string Stopping = "stopping";
    }
}
