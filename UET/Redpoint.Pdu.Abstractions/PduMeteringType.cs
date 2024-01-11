namespace Redpoint.Pdu.Abstractions
{
    /// <summary>
    /// Represents the type of metering information available on this object, which can either be the PDU overall or an individual PDU outlet. This enumeration represents flags, as some objects provide both instantaneous metering and accumulated metering.
    /// </summary>
    [Flags]
    public enum PduMeteringType
    {
        /// <summary>
        /// This object does not provide metering. If your PDU does not provide individual outlet metering, then this will be the value for the <see cref="PduMetering"/> struct on the outlet state. The <see cref="PduMetering.Voltage"/> field is always populated, even if the the type of metering is <see cref="None"/>.
        /// </summary>
        None = 0x0,

        /// <summary>
        /// Provides the instantaneous load on this object. The <see cref="PduMetering.InstantaneousAmperes"/> and <see cref="PduMetering.InstantaneousWatts"/> fields will be populated.
        /// </summary>
        Instantaneous = 0x1,

        /// <summary>
        /// Provides accumulated load on this object. The <see cref="PduMetering.AccumulatedDuration"/>, <see cref="PduMetering.AccumulatedKilowattHours"/> and <see cref="PduMetering.AccumulatedStartTime"/> fields will be populated.
        /// </summary>
        Accumulated = 0x2,
    }
}
