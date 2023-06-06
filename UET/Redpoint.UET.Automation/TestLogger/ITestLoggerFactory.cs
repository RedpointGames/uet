namespace Redpoint.UET.Automation.TestLogging
{
    public interface ITestLoggerFactory
    {
        ITestLogger CreateConsole();

        ITestLogger CreateNull();
    }
}