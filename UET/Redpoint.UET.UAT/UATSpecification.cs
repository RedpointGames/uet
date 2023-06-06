namespace Redpoint.UET.UAT
{
    using Redpoint.ProcessExecution;

    public class UATSpecification : BaseExecutionSpecification
    {
        public required string Command { get; init; }

        public bool DisableOpenGE { get; init; }
    }
}