namespace Redpoint.Windows.HostNetworkingService.ComWrapper
{
    using System.Runtime.InteropServices.Marshalling;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System;

    [GeneratedComInterface]
    [Guid(IID)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [SupportedOSPlatform("windows")]
    internal partial interface INetFwPolicy2 : IDispatch
    {
        public new const string IID = "98325047-C671-4174-8D81-DEFCD3F03186";

        NetFwProfileType2 GetCurrentProfileTypes();

        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetFirewallEnabled(NetFwProfileType2 ProfileType);

        void SetFirewallEnabled(NetFwProfileType2 ProfileType, [MarshalAs(UnmanagedType.Bool)] bool Enabled);

        int GetExcludedInterfaces(NetFwProfileType2 ProfileType);

        void SetExcludedInterfaces(NetFwProfileType2 ProfileType, int Interfaces);

        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetBlockAllInboundTraffic(NetFwProfileType2 ProfileType);

        void SetBlockAllInboundTraffic(NetFwProfileType2 ProfileType, [MarshalAs(UnmanagedType.Bool)] bool Block);

        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetNotificationsDisabled(NetFwProfileType2 ProfileType);

        void SetNotificationsDisabled(NetFwProfileType2 ProfileType, [MarshalAs(UnmanagedType.Bool)] bool Disabled);

        int GetUnicastResponsesToMulticastBroadcastDisabled(NetFwProfileType2 ProfileType);

        void SetUnicastResponsesToMulticastBroadcastDisabled(NetFwProfileType2 ProfileType, int disabled);

        INetFwRules GetRules();

        nint GetServiceRestriction();

        void EnableRuleGroup(
            NetFwProfileType2 ProfileTypesBitmask,
            [MarshalAs(UnmanagedType.BStr)] string Group,
            [MarshalAs(UnmanagedType.Bool)] bool Enable);

        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsRuleGroupEnabled(
            NetFwProfileType2 ProfileTypesBitmask,
            [MarshalAs(UnmanagedType.BStr)] string Group);

        void RestoreLocalFirewallDefaults();

        NetFwAction GetDefaultInboundAction(NetFwProfileType2 ProfileType);

        void SetDefaultInboundAction(NetFwProfileType2 ProfileType, NetFwAction Action);

        NetFwAction GetDefaultOutboundAction(NetFwProfileType2 ProfileType);

        void SetDefaultOutboundAction(NetFwProfileType2 ProfileType, NetFwAction Action);

        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetIsRuleGroupCurrentlyEnabled([MarshalAs(UnmanagedType.BStr)] string Group);

        NetFwModifyState GetLocalPolicyModifyState();
    }
}