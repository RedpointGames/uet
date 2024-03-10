namespace Redpoint.Pdu.CyberPower
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
    using System.Threading;
    using System.Threading.Tasks;

    internal class Pdu81404Pdu : IPdu
    {
        private readonly IPEndPoint _endpoint;
        private readonly OctetString _community;

        private static readonly ObjectIdentifier _oidPduVendorSmi = new("1.3.6.1.2.1.1.2.0");
        private static readonly ObjectIdentifier _oidPduUptime = new("1.3.6.1.2.1.1.3.0");
        private static readonly ObjectIdentifier _oidPduContactPerson = new("1.3.6.1.2.1.1.4.0");
        private static readonly ObjectIdentifier _oidPduDisplayName = new("1.3.6.1.2.1.1.5.0");
        private static readonly ObjectIdentifier _oidPduPhysicalLocation = new("1.3.6.1.2.1.1.6.0");
        private static readonly ObjectIdentifier _oidCyberPowerNumOutlets = new("1.3.6.1.4.1.3808.1.1.3.1.8.0");
        private static readonly ObjectIdentifier _oidCyberPowerInstantaneousVoltage = new("1.3.6.1.4.1.3808.1.1.3.2.3.1.1.6.1");
        private static readonly ObjectIdentifier _oidCyberPowerMeteringLastReset = new("1.3.6.1.4.1.3808.1.1.3.2.3.1.1.11.1");
        private static readonly ObjectIdentifier _oidCyberPowerMeteringAccumulatedKwh = new("1.3.6.1.4.1.3808.1.1.3.2.3.1.1.10.1");

        private const int _outletStateCommandImmediateOn = 1;
        private const int _outletStateCommandImmediateOff = 2;

        public Pdu81404Pdu(
            IPEndPoint endpoint,
            OctetString community)
        {
            _endpoint = endpoint;
            _community = community;
        }

        private class VariableRef
        {
            public VariableRef(Variable v)
            {
                V = v;
            }

            public VariableRef(ObjectIdentifier oid)
            {
                V = new Variable(oid);
            }

            public Variable V { get; set; }

            public ISnmpData Data => V.Data;
        }

        private async Task GetSnmpVariablesAsync(
            IList<VariableRef> variableRefs,
            CancellationToken cancellationToken)
        {
            var desiredVariables = new Dictionary<ObjectIdentifier, (bool set, VariableRef r)>();
            foreach (var vr in variableRefs)
            {
                desiredVariables[vr.V.Id] = (false, vr);
            }
            while (desiredVariables.Any(kv => !kv.Value.set))
            {
                var queryVariables = desiredVariables.Values.Where(v => !v.set).Select(v => v.r.V).ToList();
                var providedVariables = await Messenger.GetAsync(
                    VersionCode.V1,
                    _endpoint,
                    _community,
                    queryVariables,
                    cancellationToken).ConfigureAwait(false);
                if (providedVariables.Count == 0)
                {
                    break;
                }
                foreach (var pv in providedVariables)
                {
                    var ev = desiredVariables[pv.Id];
                    ev.r.V = pv;
                    desiredVariables[pv.Id] = (true, ev.r);
                }
            }
        }

        private static string GetOctetString(VariableRef r)
        {
            if (r.V.Data is OctetString o)
            {
                return o.ToString();
            }
            return string.Empty;
        }

        public async Task<PduInformation> GetInformationAsync(CancellationToken cancellationToken)
        {
            var pduVendorSmi = new VariableRef(_oidPduVendorSmi);
            var pduContactPerson = new VariableRef(_oidPduContactPerson);
            var pduDisplayName = new VariableRef(_oidPduDisplayName);
            var pduPhysicalLocation = new VariableRef(_oidPduPhysicalLocation);
            var cyberPowerNumOutlets = new VariableRef(_oidCyberPowerNumOutlets);

            await GetSnmpVariablesAsync([
                pduVendorSmi,
                pduContactPerson,
                pduDisplayName,
                pduPhysicalLocation,
                cyberPowerNumOutlets
            ], cancellationToken).ConfigureAwait(false);

            return new PduInformation
            {
                VendorInformation = new PduVendorInformation
                {
                    DeviceModel = "PDU81404",
                    AuthoritativeSmi = ((ObjectIdentifier)pduVendorSmi.Data).ToString(),
                },
                OwnerInformation = new PduOwnerInformation
                {
                    DisplayName = GetOctetString(pduDisplayName),
                    ContactPerson = GetOctetString(pduContactPerson),
                    PhysicalLocation = GetOctetString(pduPhysicalLocation),
                },
                OutletCount = ((Integer32)cyberPowerNumOutlets.Data).ToInt32(),
            };
        }

        public async Task<PduState> GetStateAsync(CancellationToken cancellationToken)
        {
            var cyberPowerInstantaneousVoltage = new VariableRef(_oidCyberPowerInstantaneousVoltage);
            var cyberPowerMeteringLastReset = new VariableRef(_oidCyberPowerMeteringLastReset);
            var cyberPowerMeteringAccumulatedKwh = new VariableRef(_oidCyberPowerMeteringAccumulatedKwh);
            var pduUptime = new VariableRef(_oidPduUptime);

            await GetSnmpVariablesAsync([
                cyberPowerInstantaneousVoltage,
                cyberPowerMeteringLastReset,
                cyberPowerMeteringAccumulatedKwh,
                pduUptime,
            ], cancellationToken).ConfigureAwait(false);

            var startDate = DateTimeOffset.ParseExact(GetOctetString(cyberPowerMeteringLastReset), "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            var now = DateTimeOffset.UtcNow;

            return new PduState
            {
                Uptime = ((TimeTicks)pduUptime.Data).ToTimeSpan(),
                Metering = new PduMetering
                {
                    Type = PduMeteringType.Accumulated,
                    Voltage = ((Integer32)cyberPowerInstantaneousVoltage.Data).ToInt32() / 10.0,
                    AccumulatedStartTime = startDate,
                    AccumulatedDuration = now - startDate,
                    AccumulatedKilowattHours = ((Integer32)cyberPowerMeteringAccumulatedKwh.Data).ToInt32() / 10.0,
                },
            };
        }

        public async Task ResetAccumulatedMeteringAsync(CancellationToken cancellationToken)
        {
            await Messenger.SetAsync(
                VersionCode.V1,
                _endpoint,
                _community,
                [
                    new Variable(
                        new ObjectIdentifier("1.3.6.1.4.1.3808.1.1.6.3.2.1.12.1"),
                        new Integer32(3))
                ],
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<PduOutletState> GetKnownOutletStateAsync(
            int index,
            VariableRef cyberPowerInstantaneousVoltage,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var outletName = new VariableRef(new ObjectIdentifier($"1.3.6.1.4.1.3808.1.1.3.3.3.1.1.2.{index + 1}"));
            var outletState = new VariableRef(new ObjectIdentifier($"1.3.6.1.4.1.3808.1.1.3.3.3.1.1.4.{index + 1}"));
            var outletMeteringAccumulatedLastReset = new VariableRef(new ObjectIdentifier($"1.3.6.1.4.1.3808.1.1.3.3.5.1.1.14.{index + 1}"));
            var outletMeteringAccumulatedKwh = new VariableRef(new ObjectIdentifier($"1.3.6.1.4.1.3808.1.1.3.3.5.1.1.13.{index + 1}"));

            await GetSnmpVariablesAsync([
                outletName,
                outletState,
                outletMeteringAccumulatedLastReset,
                outletMeteringAccumulatedKwh,
            ], cancellationToken).ConfigureAwait(false);

            var outletNameValue = GetOctetString(outletName).Split(',').First();
            var outletStateValue = ((Integer32)outletState.Data).ToInt32();
            var outletMeteringAccumulatedLastResetValue = DateTimeOffset.ParseExact(GetOctetString(outletMeteringAccumulatedLastReset), "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            var outletMeteringAccumulatedKwhValue = ((Integer32)outletMeteringAccumulatedKwh.Data).ToInt32() / 10.0;

            return new PduOutletState
            {
                Name = outletNameValue,
                Status = outletStateValue switch
                {
                    _outletStateCommandImmediateOn => PduOutletStatus.On,
                    _outletStateCommandImmediateOff => PduOutletStatus.Off,
                    _ => PduOutletStatus.Unavailable,
                },
                Metering = new PduMetering
                {
                    Type = PduMeteringType.Accumulated,
                    Voltage = ((Integer32)cyberPowerInstantaneousVoltage.Data).ToInt32() / 10.0,
                    AccumulatedStartTime = outletMeteringAccumulatedLastResetValue,
                    AccumulatedDuration = now - outletMeteringAccumulatedLastResetValue,
                    AccumulatedKilowattHours = outletMeteringAccumulatedKwhValue,
                }
            };
        }

        public async IAsyncEnumerable<(int index, PduOutletState state)> GetOutletsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var cyberPowerNumOutlets = new VariableRef(_oidCyberPowerNumOutlets);
            var cyberPowerInstantaneousVoltage = new VariableRef(_oidCyberPowerInstantaneousVoltage);

            await GetSnmpVariablesAsync([
                cyberPowerNumOutlets,
                cyberPowerInstantaneousVoltage,
            ], cancellationToken).ConfigureAwait(false);

            var now = DateTimeOffset.UtcNow;

            var outletCount = ((Integer32)cyberPowerNumOutlets.Data).ToInt32();
            for (var index = 0; index < outletCount; index++)
            {
                yield return (
                    index,
                    await GetKnownOutletStateAsync(index, cyberPowerInstantaneousVoltage, now, cancellationToken).ConfigureAwait(false)
                );
            }
        }

        public async Task<PduOutletState> GetOutletStateAsync(int index, CancellationToken cancellationToken)
        {
            var cyberPowerNumOutlets = new VariableRef(_oidCyberPowerNumOutlets);
            var cyberPowerInstantaneousVoltage = new VariableRef(_oidCyberPowerInstantaneousVoltage);

            await GetSnmpVariablesAsync([
                cyberPowerNumOutlets,
                cyberPowerInstantaneousVoltage,
            ], cancellationToken).ConfigureAwait(false);

            var outletCount = ((Integer32)cyberPowerNumOutlets.Data).ToInt32();
            if (index < 0 || index >= outletCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return await GetKnownOutletStateAsync(index, cyberPowerInstantaneousVoltage, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
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

            var cyberPowerNumOutlets = new VariableRef(_oidCyberPowerNumOutlets);

            await GetSnmpVariablesAsync([
                cyberPowerNumOutlets,
            ], cancellationToken).ConfigureAwait(false);

            var outletCount = ((Integer32)cyberPowerNumOutlets.Data).ToInt32();
            if (index < 0 || index >= outletCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var outletState = new VariableRef(new ObjectIdentifier($"1.3.6.1.4.1.3808.1.1.3.3.3.1.1.4.{index + 1}"));
            var desiredOutletStatus = _outletStateCommandImmediateOn;
            switch (desiredStatus)
            {
                case PduOutletStatus.On:
                    desiredOutletStatus = _outletStateCommandImmediateOn;
                    break;
                case PduOutletStatus.Off:
                    desiredOutletStatus = _outletStateCommandImmediateOff;
                    break;
            }

            await Messenger.SetAsync(
                VersionCode.V1,
                _endpoint,
                _community,
                [
                    new Variable(outletState.V.Id, new Integer32(desiredOutletStatus))
                ],
                cancellationToken).ConfigureAwait(false);
        }

        public async Task ResetOutletAccumulatedMeteringAsync(int index, CancellationToken cancellationToken)
        {
            var cyberPowerNumOutlets = new VariableRef(_oidCyberPowerNumOutlets);

            await GetSnmpVariablesAsync([
                cyberPowerNumOutlets,
            ], cancellationToken).ConfigureAwait(false);

            var outletCount = ((Integer32)cyberPowerNumOutlets.Data).ToInt32();
            if (index < 0 || index >= outletCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            await Messenger.SetAsync(
                VersionCode.V1,
                _endpoint,
                _community,
                [
                    new Variable(
                        new ObjectIdentifier($"1.3.6.1.4.1.3808.1.1.3.3.4.3.1.8.{index+1}"),
                        new Integer32(2))
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }
}
