namespace Redpoint.Pdu.Abstractions
{
    using Lextm.SharpSnmpLib;

    /// <summary>
    /// An interface which can be used to interact with a power distribution unit.
    /// </summary>
    public interface IPdu
    {
        /// <summary>
        /// Retrieve constant or infrequently changing information about the power distribution unit, including the device model, configured display name and number of outlets.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>Information about the power distribution unit itself.</returns>
        Task<PduInformation> GetInformationAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieve the current state of the power distribution unit, including it's current uptime and metering information available at the PDU level.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The current state of the power distribution unit.</returns>
        Task<PduState> GetStateAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempt to reset the accumulated energy statistics of the entire PDU.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <exception cref="NotSupportedException">Thrown if the power distribution unit does not support accumulated metering overall (i.e. it is not a metered PDU).</exception>
        /// <returns>An asynchronous task to await.</returns>
        Task ResetAccumulatedMeteringAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("This PDU does not support accumulated metering. Check the PduState.Metering.Type property to see if this PDU supports accumulated metering before attempting to reset the meter.");
        }

        /// <summary>
        /// Retrieve all of the outlets of the power distribution unit and their current state.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>An asynchronous enumerable of (index, state) pairs, where index is the index of the outlet on the power distribution unit and state is the current state of the outlet.</returns>
        IAsyncEnumerable<(int index, PduOutletState state)> GetOutletsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieve the state of an individual outlet on the power distribution unit.
        /// </summary>
        /// <param name="index">The index of the outlet, which must be a value between 0 (inclusive) and the value of <see cref="PduInformation.OutletCount"/> returned from <see cref="GetInformationAsync(CancellationToken)"/> (exclusive).</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the provided index is less than 0 or exceeds the number of outlets on the power distribution unit.</exception>
        /// <returns>The current state of the PDU outlet.</returns>
        Task<PduOutletState> GetOutletStateAsync(int index, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempt to turn on or off an individual outlet on the power distribution unit.
        /// </summary>
        /// <param name="index">The index of the outlet, which must be a value between 0 (inclusive) and the value of <see cref="PduInformation.OutletCount"/> returned from <see cref="GetInformationAsync(CancellationToken)"/> (exclusive).</param>
        /// <param name="desiredStatus">The desired status of the outlet. Only <see cref="PduOutletStatus.On"/> and <see cref="PduOutletStatus.Off"/> are valid values.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the provided index is less than 0 or exceeds the number of outlets on the power distribution unit.</exception>
        /// <exception cref="ArgumentException">Thrown if the desired status is not either <see cref="PduOutletStatus.On"/> or <see cref="PduOutletStatus.Off"/>.</exception>
        /// <exception cref="NotSupportedException">Thrown if the power distribution unit does not support turning on and off individual outlets (i.e. it is not a switched PDU).</exception>
        /// <returns>An asynchronous task to await.</returns>
        Task SetOutletStatusAsync(int index, PduOutletStatus desiredStatus, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempt to reset the accumulated energy statistics of the specified outlet.
        /// </summary>
        /// <param name="index">The index of the outlet, which must be a value between 0 (inclusive) and the value of <see cref="PduInformation.OutletCount"/> returned from <see cref="GetInformationAsync(CancellationToken)"/> (exclusive).</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the provided index is less than 0 or exceeds the number of outlets on the power distribution unit.</exception>
        /// <exception cref="NotSupportedException">Thrown if the power distribution unit does not support accumulated metering on individual outlets (i.e. it is not a metered PDU).</exception>
        /// <returns>An asynchronous task to await.</returns>
        Task ResetOutletAccumulatedMeteringAsync(int index, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("This PDU does not support accumulated metering per outlet. Check the PduOutletState.Metering.Type property to see if this PDU supports accumulated metering on the outlet before attempting to reset the meter.");
        }
    }
}
