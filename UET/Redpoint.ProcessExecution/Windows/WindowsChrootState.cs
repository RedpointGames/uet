namespace Redpoint.ProcessExecution.Windows
{
    using System.Collections.Generic;

    internal class WindowsChrootState
    {
        public required IReadOnlyDictionary<char, string> PerProcessDriveMappings { get; init; }
        public nint[]? HandlesToCloseOnProcessExit { get; set; }
    }
}
