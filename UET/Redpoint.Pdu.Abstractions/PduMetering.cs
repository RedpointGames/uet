namespace Redpoint.Pdu.Abstractions
{
    /// <summary>
    /// Represents metering of power usage on an object, either a PDU overall or an individual PDU outlet.
    /// </summary>
    public readonly record struct PduMetering
    {
        /// <summary>
        /// The type of metering information available.
        /// </summary>
        public required readonly PduMeteringType Type { get; init; }

        /// <summary>
        /// The voltage of the object when the metering information was received. This is provided regardless of the metering type, since it will be constant for any real PDU.
        /// </summary>
        public required readonly double Voltage { get; init; }

        /// <summary>
        /// The amperes being drawn on this object at the instant in time when the metering information was received.
        /// </summary>
        public readonly double InstantaneousAmperes { get; init; }

        /// <summary>
        /// The watts being drawn on this object at the instant in time when the metering information was received.
        /// </summary>
        public readonly double InstantaneousWatts => Voltage * InstantaneousAmperes;

        /// <summary>
        /// The duration over which the accumulated metering has been performed.
        /// </summary>
        public readonly TimeSpan AccumulatedDuration { get; init; }

        /// <summary>
        /// The point in time at which accumulated metering started.
        /// </summary>
        public readonly DateTimeOffset AccumulatedStartTime { get; init; }

        /// <summary>
        /// The kilowatt hours accumulated on the meter since metering started.
        /// </summary>
        public readonly double AccumulatedKilowattHours { get; init; }
    }
}
