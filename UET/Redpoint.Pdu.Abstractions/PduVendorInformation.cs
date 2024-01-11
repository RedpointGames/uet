namespace Redpoint.Pdu.Abstractions
{
    /// <summary>
    /// Represents information about a power distribution unit, as set by the vendor or manufacturer of the device.
    /// </summary>
    public readonly record struct PduVendorInformation
    {
        /// <summary>
        /// The model of the device.
        /// </summary>
        public required readonly string DeviceModel { get; init; }

        /// <summary>
        /// The authoritative SMI prefix for the vendor or for this device as allocated by the vendor. This may be an empty string.
        /// </summary>
        public required readonly string AuthoritativeSmi { get; init; }
    }
}
