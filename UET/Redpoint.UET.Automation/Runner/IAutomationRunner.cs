namespace Redpoint.UET.Automation.Runner
{
    using Redpoint.UET.Automation.Model;
    using System;

    public interface IAutomationRunner : IAsyncDisposable
    {
        Task<TestResult[]> WaitForResultsAsync();
    }
}
