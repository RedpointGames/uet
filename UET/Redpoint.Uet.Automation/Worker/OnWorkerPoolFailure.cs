namespace Redpoint.Uet.Automation.Worker
{
    /// <summary>
    /// Raised if the worker pool encounters an unrecoverable error, such as a configuration
    /// error that makes it impossible to launch the desired workers.
    /// </summary>
    /// <param name="reason">The reason for the failure.</param>
    /// <returns>Awaitable task.</returns>
    public delegate Task OnWorkerPoolFailure(string reason);
}
