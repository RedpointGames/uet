namespace Redpoint.KubernetesManager.Signalling
{
    internal class FlagAlreadySetException : Exception
    {
        public FlagAlreadySetException() : base("Each flag may only be set once!")
        {
        }
    }
}
