namespace Redpoint.Uet.Automation.TestNotification
{
    public interface ITestNotificationFactory
    {
        ITestNotification CreateNull();

        ITestNotification CreateIo(CancellationToken cancellationToken);
    }
}
