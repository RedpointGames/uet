namespace Redpoint.ProcessExecution.Windows
{
    using System.Collections.Generic;

    internal class WindowsChrootState
    {
        public required IDictionary<char, string> PerProcessDriveMappings { get; init; }
        public nint[]? HandlesToCloseOnProcessExit { get; set; }
    }
}
