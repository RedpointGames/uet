namespace Redpoint.ProcessExecution.Enumerable
{
    /// <summary>
    /// The abstract base class of process responses shared by <see cref="StandardOutputResponse"/>, <see cref="StandardErrorResponse"/> and <see cref="ExitCodeResponse"/>. You can use pattern matching with the switch statement on the returned <see cref="ProcessResponse"/> value.
    /// </summary>
    public abstract record class ProcessResponse
    {
        internal ProcessResponse()
        {
        }
    }
}
