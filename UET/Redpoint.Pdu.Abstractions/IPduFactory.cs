namespace Redpoint.Pdu.Abstractions
{
    using Lextm.SharpSnmpLib.Security;
    using System.Net;

    /// <summary>
    /// An interface which can be used to check if the target is a supported power distribution unit of this factory, and if it is, return a <see cref="IPdu"/> instance representing that PDU.
    /// </summary>
    public interface IPduFactory
    {
        /// <summary>
        /// Returns a list of device models supported by this <see cref="IPduFactory"/>, including their default SNMP community and whether they use SNMP v3 for communication.
        /// </summary>
        /// <returns>A list of device models supported by this <see cref="IPduFactory"/>.</returns>
        IReadOnlyList<PduSupportedDeviceModel> GetSupportedDeviceModels();

        /// <summary>
        /// Attempt to connect to the PDU at the given address, using the provided SNMP community and (if this device uses SNMP v3) SNMP privacy provider.
        /// 
        /// If the target device is unresponsive, or if the target device isn't a device model supported by this <see cref="IPduFactory"/>, then this method will return null.
        /// </summary>
        /// <param name="address">The IP address of the power distribution unit.</param>
        /// <param name="snmpCommunity">The SNMP community for this power distribution unit.</param>
        /// <param name="snmpV3PrivacyProvider">The SNMP privacy provider for connecting to this power distribution unit.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>An instance of <see cref="IPdu"/> if the PDU responded and is supported by this factory, or null.</returns>
        Task<IPdu?> TryGetAsync(
            IPAddress address,
            string snmpCommunity,
            IPrivacyProvider? snmpV3PrivacyProvider = null,
            CancellationToken cancellationToken = default);
    }
}
