namespace Redpoint.Uet.Patching.Runtime
{
    using System;

    internal class ConsoleUetPatchLogging : IUetPatchLogging
    {
        public void LogInfo(string message)
        {
            Console.WriteLine($"Redpoint.Uet.Patching.Runtime [info] {message}");
        }

        public void LogWarning(string message)
        {
            Console.WriteLine($"Redpoint.Uet.Patching.Runtime [warn] {message}");
        }

        public void LogError(string message)
        {
            Console.WriteLine($"Redpoint.Uet.Patching.Runtime [fail] {message}");
        }
    }
}