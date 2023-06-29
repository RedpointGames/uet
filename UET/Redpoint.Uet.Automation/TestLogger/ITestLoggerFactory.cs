namespace Redpoint.Uet.Automation.TestLogging
{
    public interface ITestLoggerFactory
    {
        ITestLogger CreateConsole();

        ITestLogger CreateNull();
    }
}