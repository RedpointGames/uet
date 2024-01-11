namespace Redpoint.Pdu.Abstractions
{
    /// <summary>
    /// Represents constant or infrequently changing information about a power distribution unit.
    /// </summary>
    public readonly record struct PduInformation
    {
        /// <summary>
        /// Information about the power distribution unit, as set by the vendor or manufacturer.
        /// </summary>
        public required readonly PduVendorInformation VendorInformation { get; init; }

        /// <summary>
        /// Information about the power distribution unit, as configured by the entity that owns it.
        /// </summary>
        public required readonly PduOwnerInformation OwnerInformation { get; init; }

        /// <summary>
        /// The number of outlets on this power distribution unit.
        /// </summary>
        public required readonly int OutletCount { get; init; }
    }
}
