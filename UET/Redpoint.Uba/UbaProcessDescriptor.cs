namespace Redpoint.Uba
{
    using Redpoint.Concurrency;
    using Redpoint.ProcessExecution;
    using System;
    using System.Runtime.ExceptionServices;
    using System.Threading;

    internal class UbaProcessDescriptor
    {
        public required ProcessSpecification ProcessSpecification { get; init; }

        public required ICaptureSpecification CaptureSpecification { get; init; }

        public required CancellationToken CancellationToken { get; init; }

        public required DateTimeOffset DateQueuedUtc { get; init; }

        public required bool PreferRemote { get; set; }

        public required bool AllowRemote { get; set; }

        public required Gate CompletionGate { get; init; }

        public int ExitCode { get; set; }

        public ExceptionDispatchInfo? ExceptionDispatchInfo { get; set; }
    }
}
