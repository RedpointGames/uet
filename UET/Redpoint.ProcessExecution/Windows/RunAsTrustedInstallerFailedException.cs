namespace Redpoint.ProcessExecution.Windows
{
    using System;

    public class RunAsTrustedInstallerFailedException : Exception
    {
        public RunAsTrustedInstallerFailedException(string message) : base(message)
        {
        }
    }
}
