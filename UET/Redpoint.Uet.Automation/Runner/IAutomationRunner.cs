namespace Redpoint.Uet.Automation.Runner
{
    using Redpoint.Uet.Automation.Model;
    using System;

    public interface IAutomationRunner : IAsyncDisposable
    {
        Task<TestResult[]> WaitForResultsAsync();
    }
}
