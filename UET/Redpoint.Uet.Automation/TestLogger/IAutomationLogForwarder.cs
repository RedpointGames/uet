namespace Redpoint.Uet.Automation.TestLogger
{
    using Redpoint.ApplicationLifecycle;

    public interface IAutomationLogForwarder : IApplicationLifecycle
    {
        string? GetPipeName();
    }
}
