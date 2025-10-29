namespace Redpoint.Uet.Uat
{
    public class EngineUefsRequiresRemountException : Exception
    {
        public EngineUefsRequiresRemountException()
            : base("The UEFS mount for the engine is not ready to serve requests, and it must be remounted.")
        {
        }
    }
}