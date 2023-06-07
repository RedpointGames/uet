namespace Redpoint.UET.Automation.TestLogger
{
    using Redpoint.ApplicationLifecycle;

    public interface IAutomationLogForwarder : IApplicationLifecycle
    {
        string? GetPipeName();
    }
}
