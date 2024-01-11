namespace Redpoint.Pdu.Abstractions
{
    /// <summary>
    /// Represents a snapshot of the state of an outlet on a PDU.
    /// </summary>
    public readonly record struct PduOutletState
    {
        /// <summary>
        /// The status of the outlet when this state snapshot was taken.
        /// </summary>
        public required readonly PduOutletStatus Status { get; init; }

        /// <summary>
        /// The name of the outlet. If no name is set, this is an empty
        /// string; it is never a null value.
        /// </summary>
        public required readonly string Name { get; init; }

        /// <summary>
        /// Metering information about this outlet. You must check <see cref="PduMetering.Type"/> to know what fields of this struct will be populated.
        /// </summary>
        public required readonly PduMetering Metering { get; init; }
    }
}
