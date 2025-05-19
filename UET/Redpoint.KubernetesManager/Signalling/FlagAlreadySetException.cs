namespace Redpoint.KubernetesManager.Signalling
{
    public class FlagAlreadySetException : Exception
    {
        public FlagAlreadySetException() : base("Each flag may only be set once!")
        {
        }
    }
}
