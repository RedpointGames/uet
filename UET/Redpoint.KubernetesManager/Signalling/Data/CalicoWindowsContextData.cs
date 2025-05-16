namespace Redpoint.KubernetesManager.Signalling.Data
{
    internal class CalicoWindowsContextData : IAssociatedData
    {
        public string SourceVIP { get; }

        public CalicoWindowsContextData(string sourceVip)
        {
            SourceVIP = sourceVip;
        }
    }
}
