namespace Redpoint.Pdu.Serveredge
{
    using Lextm.SharpSnmpLib;
    using Lextm.SharpSnmpLib.Messaging;
    using Lextm.SharpSnmpLib.Security;
    using Redpoint.Pdu.Abstractions;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;

    /// <summary>
    /// An implementation of <see cref="IPduFactory"/> that supports SERVEREDGE power distribution units.
    /// </summary>
    public class ServeredgePduFactory : IPduFactory
    {
        private static readonly PduSupportedDeviceModel[] _supportedModels = new[]
        {
            new PduSupportedDeviceModel
            {
                ManufacturerName = "Serveredge",
                DeviceModel = "SEDG-8PSW-C13",
                DefaultSnmpCommunity = "public",
                UsesSnmpV3 = false,
            }
        };

        /// <inheritdoc />
        public IReadOnlyList<PduSupportedDeviceModel> GetSupportedDeviceModels()
        {
            return _supportedModels;
        }

        /// <inheritdoc />
        public async Task<IPdu?> TryGetAsync(
            IPAddress address,
            string snmpCommunity,
            IPrivacyProvider? snmpV3PrivacyProvider = null,
            CancellationToken cancellationToken = default)
        {
            Variable smiPrefix;
            Variable modelNumber;
            IList<Variable> deviceInformation;
            try
            {
                deviceInformation = await Messenger.GetAsync(
                    VersionCode.V1,
                    new IPEndPoint(address, 161),
                    new OctetString(snmpCommunity),
                    [
                        new Variable(new ObjectIdentifier("1.3.6.1.2.1.1.2.0")),
                        new Variable(new ObjectIdentifier("1.3.6.1.4.1.17420.1.2.9.1.19.0"))
                    ],
                    cancellationToken)
                    .ConfigureAwait(false);
                smiPrefix = deviceInformation[0];
                modelNumber = deviceInformation[1];
            }
            catch (ErrorException)
            {
                // The device doesn't support those SNMP variables, which means
                // it can't be a SERVEREDGE device.
                return null;
            }
            catch (TimeoutException)
            {
                // The device isn't responding, or the address is incorrect.
                return null;
            }

            if (!(smiPrefix.Data is ObjectIdentifier smiPrefixValue) ||
                !(modelNumber.Data is OctetString modelNumberValue))
            {
                // This device isn't a supported SERVEREDGE device.
                return null;
            }
            if (smiPrefixValue != new ObjectIdentifier("1.3.6.1.4.1.17420"))
            {
                // This device isn't a supported SERVEREDGE device.
                return null;
            }

            switch (modelNumberValue.ToString())
            {
                case "SEDG-8PSW-C13":
                    // This device is the SEDG-8PSW-C13.
                    return new Sedg8PswC13Pdu(
                        new IPEndPoint(address, 161),
                        new OctetString(snmpCommunity));
                default:
                    // This device isn't a supported SERVEREDGE device.
                    return null;
            }
        }
    }
}
