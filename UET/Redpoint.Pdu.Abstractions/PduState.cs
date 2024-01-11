namespace Redpoint.Pdu.Abstractions
{
    /// <summary>
    /// Represents the current state of a power distribution unit.
    /// </summary>
    public readonly record struct PduState
    {
        /// <summary>
        /// The length of time the power distribution unit has been powered on.
        /// </summary>
        public required readonly TimeSpan Uptime { get; init; }

        /// <summary>
        /// The metering information available at the PDU level, shared across all outlets.
        /// </summary>
        public required readonly PduMetering Metering { get; init; }
    }
}
