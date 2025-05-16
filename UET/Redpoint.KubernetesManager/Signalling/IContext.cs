namespace Redpoint.KubernetesManager.Signalling
{
    using System.Threading.Tasks;

    internal interface IContext
    {
        /// <summary>
        /// Sets a flag with the associated data, which other components may be waiting on.
        /// Flags can only be set once, and are designed for once-off thresholds (not recurring
        /// events). If you need recurrance, use signals instead.
        /// </summary>
        /// <param name="name">The flag name from <see cref="WellKnownFlags"/>.</param>
        /// <param name="data">The data to be associated with the flag, if any.</param>
        void SetFlag(string name, IAssociatedData? data = null);

        /// <summary>
        /// Waits for the specified flag to be set and then continues.
        /// </summary>
        /// <param name="name">The flag name from <see cref="WellKnownFlags"/>.</param>
        /// <returns>The data associated with the flag, if any.</returns>
        Task WaitForFlagAsync(string name);

        /// <summary>
        /// Waits for the specified flag to be set and then continues.
        /// </summary>
        /// <param name="name">The flag name from <see cref="WellKnownFlags"/>.</param>
        /// <returns>The data associated with the flag, if any.</returns>
        Task<T> WaitForFlagAsync<T>(string name) where T : IAssociatedData;

        /// <summary>
        /// Waits for the specified flag to be set and then continues. This ignores the
        /// shutdown cancellation token so it continues to work during cleanup steps.
        /// </summary>
        /// <param name="name">The flag name from <see cref="WellKnownFlags"/>.</param>
        /// <returns>The data associated with the flag, if any.</returns>
        Task WaitForUninterruptableFlagAsync(string name);

        /// <summary>
        /// Raises a signal that other components can respond to. Each time you raise a signal
        /// the registered callbacks will be fired again.
        /// </summary>
        /// <param name="name">The signal name from <see cref="WellKnownSignals"/>.</param>
        /// <param name="data">The associated data (if any).</param>
        Task RaiseSignalAsync(string name, IAssociatedData? data, CancellationToken cancellationToken);

        /// <summary>
        /// Forces RKM to stop with a critical error. Used by components when they detect that
        /// Kubernetes will not be able to run on this machine.
        /// </summary>
        void StopOnCriticalError();

        /// <summary>
        /// The role of this instance.
        /// </summary>
        RoleType Role { get; }
    }
}
