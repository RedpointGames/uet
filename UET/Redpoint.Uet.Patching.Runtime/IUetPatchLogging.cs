namespace Redpoint.Uet.Patching.Runtime
{
    internal interface IUetPatchLogging
    {
        void LogInfo(string message);

        void LogWarning(string message);

        void LogError(string message);
    }
}
