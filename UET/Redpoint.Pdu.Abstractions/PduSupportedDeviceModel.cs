namespace Redpoint.Pdu.Abstractions
{
    using Lextm.SharpSnmpLib.Security;
    using System.Net;

    /// <summary>
    /// Represents a device model supported by an implementation of <see cref="IPduFactory"/>, for which <see cref="IPduFactory.TryGetAsync(IPAddress, string, IPrivacyProvider?, CancellationToken)"/> should be able to successfully connect to.
    /// </summary>
    public readonly record struct PduSupportedDeviceModel
    {
        /// <summary>
        /// The manufacturer or vendor of the device.
        /// </summary>
        public required readonly string ManufacturerName { get; init; }

        /// <summary>
        /// The model of the supported device.
        /// </summary>
        public required readonly string DeviceModel { get; init; }

        /// <summary>
        /// The default SNMP community for devices of this type.
        /// </summary>
        public required readonly string DefaultSnmpCommunity { get; init; }

        /// <summary>
        /// If true, this device uses SNMP v3 to communicate and a
        /// privacy provider must be set.
        /// </summary>
        public required readonly bool UsesSnmpV3 { get; init; }
    }
}
