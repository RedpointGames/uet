namespace Redpoint.Pdu.Serveredge
{
    using Lextm.SharpSnmpLib;
    using Lextm.SharpSnmpLib.Messaging;
    using Redpoint.Pdu.Abstractions;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class Sedg8PswC13Pdu : IPdu
    {
        private readonly IPEndPoint _endpoint;
        private readonly OctetString _community;

        private const double _pduVoltage = 240;
        private static readonly ObjectIdentifier _oidPduInstantaneousAmps = new("1.3.6.1.4.1.17420.1.2.9.1.11.0");
        private static readonly ObjectIdentifier _oidPduOutletStatus = new("1.3.6.1.4.1.17420.1.2.9.1.13.0");
        private static readonly ObjectIdentifier _oidPduVendorSmi = new("1.3.6.1.2.1.1.2.0");
        private static readonly ObjectIdentifier _oidPduUptime = new("1.3.6.1.2.1.1.3.0");
        private static readonly ObjectIdentifier _oidPduContactPerson = new("1.3.6.1.2.1.1.4.0");
        private static readonly ObjectIdentifier _oidPduDisplayName = new("1.3.6.1.2.1.1.5.0");
        private static readonly ObjectIdentifier _oidPduPhysicalLocation = new("1.3.6.1.2.1.1.6.0");
        private static ObjectIdentifier GetOidPduOutletName(int index)
        {
            return new($"1.3.6.1.4.1.17420.1.2.9.1.{index}.0");
        }

        public Sedg8PswC13Pdu(
            IPEndPoint endpoint,
            OctetString community)
        {
            _endpoint = endpoint;
            _community = community;
        }

        private Task<IList<Variable>> GetSnmpVariablesAsync(
            IList<Variable> variables,
            CancellationToken cancellationToken)
        {
            return Messenger.GetAsync(
                VersionCode.V1,
                _endpoint,
                _community,
                variables,
                cancellationToken);
        }

        public async Task<PduInformation> GetInformationAsync(CancellationToken cancellationToken)
        {
            var pduOutletStatus = new Variable(_oidPduOutletStatus);
            var pduVendorSmi = new Variable(_oidPduVendorSmi);
            var pduContactPerson = new Variable(_oidPduContactPerson);
            var pduDisplayName = new Variable(_oidPduDisplayName);
            var pduPhysicalLocation = new Variable(_oidPduPhysicalLocation);

            await GetSnmpVariablesAsync([
                pduOutletStatus,
                pduVendorSmi,
                pduContactPerson,
                pduDisplayName,
                pduPhysicalLocation
            ], cancellationToken).ConfigureAwait(false);

            return new PduInformation
            {
                VendorInformation = new PduVendorInformation
                {
                    DeviceModel = "SEDG-8PSW-C13",
                    AuthoritativeSmi = ((ObjectIdentifier)pduVendorSmi.Data).ToString(),
                },
                OwnerInformation = new PduOwnerInformation
                {
                    DisplayName = ((OctetString)pduDisplayName.Data).ToString(),
                    ContactPerson = ((OctetString)pduContactPerson.Data).ToString(),
                    PhysicalLocation = ((OctetString)pduPhysicalLocation.Data).ToString(),
                },
                OutletCount = ((OctetString)pduOutletStatus.Data).ToString().Split(',').Length,
            };
        }

        public async Task<PduState> GetStateAsync(CancellationToken cancellationToken)
        {
            var pduInstantaneousAmps = new Variable(_oidPduInstantaneousAmps);
            var pduUptime = new Variable(_oidPduUptime);

            await GetSnmpVariablesAsync([
                pduInstantaneousAmps,
                pduUptime,
            ], cancellationToken).ConfigureAwait(false);

            return new PduState
            {
                Uptime = ((TimeTicks)pduUptime.Data).ToTimeSpan(),
                Metering = new PduMetering
                {
                    Type = PduMeteringType.Instantaneous,
                    Voltage = _pduVoltage,
                    // 1 amp = 10 in this SNMP variable
                    InstantaneousAmperes = ((Integer32)pduInstantaneousAmps.Data).ToInt32() / 10,
                },
            };
        }

        public async IAsyncEnumerable<(int index, PduOutletState state)> GetOutletsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var pduOutletStatus = new Variable(_oidPduOutletStatus);

            await GetSnmpVariablesAsync([
                pduOutletStatus
            ], cancellationToken).ConfigureAwait(false);

            var outletStatuses = ((OctetString)pduOutletStatus.Data).ToString().Split(',');
            var outletCount = outletStatuses.Length;

            var outletNames = new List<Variable>();
            for (var i = 0; i < outletCount; i++)
            {
                outletNames.Add(new Variable(GetOidPduOutletName(i + 1)));
            }

            await GetSnmpVariablesAsync(outletNames, cancellationToken).ConfigureAwait(false);

            for (var i = 0; i < outletCount; i++)
            {
                var outletName = ((OctetString)outletNames[i].Data).ToString().Split(',').First();
                var outletStatus = int.Parse(outletStatuses[i], CultureInfo.InvariantCulture);
                yield return (i, new PduOutletState
                {
                    Name = outletName,
                    Status = outletStatus switch
                    {
                        1 => PduOutletStatus.On,
                        0 => PduOutletStatus.Off,
                        -1 => PduOutletStatus.Unavailable,
                        -2 => PduOutletStatus.PhysicallyLost,
                        _ => PduOutletStatus.Unavailable,
                    },
                    Metering = new PduMetering
                    {
                        Type = PduMeteringType.None,
                        Voltage = _pduVoltage,
                    }
                });
            }
        }

        public async Task<PduOutletState> GetOutletStateAsync(int index, CancellationToken cancellationToken)
        {
            var pduOutletStatus = new Variable(_oidPduOutletStatus);

            await GetSnmpVariablesAsync([
                pduOutletStatus
            ], cancellationToken).ConfigureAwait(false);

            var outletStatuses = ((OctetString)pduOutletStatus.Data).ToString().Split(',');
            var outletCount = outletStatuses.Length;

            if (index < 0 || index >= outletCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var outletName = new Variable(GetOidPduOutletName(index + 1));

            await GetSnmpVariablesAsync([outletName], cancellationToken).ConfigureAwait(false);

            var outletNameValue = ((OctetString)outletName.Data).ToString().Split(',').First();
            var outletStatus = int.Parse(outletStatuses[index], CultureInfo.InvariantCulture);
            return new PduOutletState
            {
                Name = outletNameValue,
                Status = outletStatus switch
                {
                    1 => PduOutletStatus.On,
                    0 => PduOutletStatus.Off,
                    -1 => PduOutletStatus.Unavailable,
                    -2 => PduOutletStatus.PhysicallyLost,
                    _ => PduOutletStatus.Unavailable,
                },
                Metering = new PduMetering
                {
                    Type = PduMeteringType.None,
                    Voltage = _pduVoltage,
                }
            };
        }

        public async Task SetOutletStatusAsync(
            int index,
            PduOutletStatus desiredStatus,
            CancellationToken cancellationToken)
        {
            if (desiredStatus != PduOutletStatus.On ||
                desiredStatus != PduOutletStatus.Off)
            {
                throw new ArgumentException("Desired outlet status must be either On or Off.", nameof(desiredStatus));
            }

            var pduOutletStatus = new Variable(_oidPduOutletStatus);

            await GetSnmpVariablesAsync([
                pduOutletStatus
            ], cancellationToken).ConfigureAwait(false);

            var outletStatuses = ((OctetString)pduOutletStatus.Data).ToString().Split(',');
            var outletCount = outletStatuses.Length;

            if (index < 0 || index >= outletCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            switch (desiredStatus)
            {
                case PduOutletStatus.On:
                    outletStatuses[index] = "1";
                    break;
                case PduOutletStatus.Off:
                    outletStatuses[index] = "0";
                    break;
            }

            var newOutletStatus = string.Join(',', outletStatuses);

            await Messenger.SetAsync(
                VersionCode.V1,
                _endpoint,
                _community,
                [
                    new Variable(
                        _oidPduOutletStatus,
                        new OctetString(newOutletStatus))
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }
}
