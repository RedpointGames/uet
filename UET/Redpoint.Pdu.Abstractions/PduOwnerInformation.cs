namespace Redpoint.Pdu.Abstractions
{
    /// <summary>
    /// Represents information about a power distribution unit, as configured by the entity that owns it.
    /// </summary>
    public readonly record struct PduOwnerInformation
    {
        /// <summary>
        /// The display name of the PDU. This may be an empty string.
        /// </summary>
        public required readonly string DisplayName { get; init; }

        /// <summary>
        /// The person within the organisation who manages this PDU, and their contact information. This may be an empty string.
        /// </summary>
        public required readonly string ContactPerson { get; init; }

        /// <summary>
        /// The physical location of this PDU. This may be an empty string.
        /// </summary>
        public required readonly string PhysicalLocation { get; init; }
    }
}
