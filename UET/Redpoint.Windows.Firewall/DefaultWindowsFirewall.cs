using Redpoint.Windows.HostNetworkingService.ComWrapper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace Redpoint.Windows.Firewall
{
    [SupportedOSPlatform("windows")]
    public class DefaultWindowsFirewall : IWindowsFirewall
    {
        INetFwPolicy2? _policy;

        private static T? CreateInstance<T>(string classId, string interfaceId) where T : class
        {
            var clsid = new Guid(classId);
            var iid = new Guid(interfaceId);
            int hr = Ole32.CoCreateInstance(
                ref clsid,
                0,
                (uint)Ole32.CLSCTX.CLSCTX_LOCAL_SERVER,
                ref iid,
                out object comObject);
            if (hr == 0)
            {
                return (T)comObject;
            }
            else
            {
                return null;
            }
        }

        public DefaultWindowsFirewall()
        {
            _policy = CreateInstance<INetFwPolicy2>(
                ClassIds.NetFwPolicy2,
                INetFwPolicy2.IID);
        }

        public void UpsertPortRule(
            string name,
            bool allow,
            int port,
            Protocol protocol)
        {
            if (_policy == null)
            {
                throw new WindowsFirewallNotAvailableException();
            }

            var rules = _policy.GetRules();

            INetFwRule? rule = null;
            try
            {
                rule = rules.Item(name);
            }
            catch (FileNotFoundException)
            {
            }
            bool exists = rule != null;
            if (rule == null)
            {
                rule = CreateInstance<INetFwRule>(ClassIds.NetFwRule, INetFwRule.IID);
            }
            if (rule == null)
            {
                throw new InvalidOperationException();
            }

            rule.SetName(name);
            rule.SetDescription(string.Empty);
            rule.SetApplicationName(null);
            rule.SetServiceName(null);
            rule.SetProtocol(protocol switch
            {
                Protocol.Tcp => NetFwIpProtocol.ProtocolTcp,
                Protocol.Udp => NetFwIpProtocol.ProtocolUdp,
                Protocol.Any => NetFwIpProtocol.ProtocolAny,
                _ => NetFwIpProtocol.ProtocolAny,
            });
            rule.SetLocalPorts(port.ToString(CultureInfo.InvariantCulture));
            rule.SetRemotePorts(null);
            rule.SetLocalAddresses(null);
            rule.SetRemoteAddresses(null);
            //rule.SetIcmpTypesAndCodes(null);
            rule.SetDirection(NetFwRuleDirection.In);
            //rule.SetInterfaces(0);
            rule.SetInterfaceTypes(null);
            rule.SetEnabled(-1 /* VARIANT_BOOL = true */);
            rule.SetGrouping(null);
            rule.SetProfiles(3);
            rule.SetEdgeTraversal(true);
            rule.SetAction(allow ? NetFwAction.Allow : NetFwAction.Block);

            if (!exists)
            {
                rules.Add(rule);
            }
        }

        public void UpsertApplicationRule(
            string name,
            bool allow,
            string path)
        {
            if (_policy == null)
            {
                throw new WindowsFirewallNotAvailableException();
            }

            var rules = _policy.GetRules();

            INetFwRule? rule = null;
            try
            {
                rule = rules.Item(name);
            }
            catch (FileNotFoundException)
            {
            }
            bool exists = rule != null;
            if (rule == null)
            {
                rule = CreateInstance<INetFwRule>(ClassIds.NetFwRule, INetFwRule.IID);
            }
            if (rule == null)
            {
                throw new InvalidOperationException();
            }

            rule.SetName(name);
            rule.SetDescription(string.Empty);
            rule.SetApplicationName(path);
            rule.SetServiceName(null);
            rule.SetProtocol(NetFwIpProtocol.ProtocolAny);
            //rule.SetLocalPorts(null);
            //rule.SetRemotePorts(null);
            rule.SetLocalAddresses(null);
            rule.SetRemoteAddresses(null);
            //rule.SetIcmpTypesAndCodes(null);
            rule.SetDirection(NetFwRuleDirection.In);
            //rule.SetInterfaces(0);
            rule.SetInterfaceTypes(null);
            rule.SetEnabled(-1 /* VARIANT_BOOL = true */);
            rule.SetGrouping(null);
            rule.SetProfiles(3);
            rule.SetEdgeTraversal(true);
            rule.SetAction(allow ? NetFwAction.Allow : NetFwAction.Block);

            if (!exists)
            {
                rules.Add(rule);
            }
        }
    }
}
