namespace Redpoint.Pdu.Abstractions
{
    /// <summary>
    /// Represents the status of a single outlet on a PDU.
    /// </summary>
    public enum PduOutletStatus
    {
        /// <summary>
        /// The outlet is currently on.
        /// </summary>
        On,

        /// <summary>
        /// The outlet is currently off.
        /// </summary>
        Off,

        /// <summary>
        /// The outlet is not available; e.g. it doesn't exist.
        /// </summary>
        Unavailable,

        /// <summary>
        /// The outlet is not receiving power due to a physical
        /// interruption such as breaker being triggered.
        /// </summary>
        PhysicallyLost,
    }
}
