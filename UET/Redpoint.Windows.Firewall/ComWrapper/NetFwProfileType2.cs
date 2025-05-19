namespace Redpoint.Windows.HostNetworkingService.ComWrapper
{
    [Flags]
    internal enum NetFwProfileType2
    {
        Domain = 1,
        Private = 2,
        Public = 4,
        All = 0x7fffffff,
    }
}