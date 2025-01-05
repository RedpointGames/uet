namespace Redpoint.CloudFramework.Event.PubSub
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IPubSub
    {
        Task PublishAsync(SerializedEvent jsonObject);

        Task SubscribeAndLoopUntilCancelled(
            string subscriptionName,
            string[] eventTypes,
            SubscriptionCleanupPolicy cleanupPolicy,
            Func<SerializedEvent, Task<bool>> onMessage,
            CancellationToken cancellationToken);
    }

    public enum SubscriptionCleanupPolicy
    {
        NoCleanup,
        DeleteSubscription,
    }
}
