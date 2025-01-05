namespace Redpoint.CloudFramework.Event.PubSub
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Null implementation used when there is no other implementation to use.
    /// </summary>
    internal class NullPubSub : IPubSub
    {
        public Task PublishAsync(SerializedEvent jsonObject)
        {
            return Task.CompletedTask;
        }

        public Task SubscribeAndLoopUntilCancelled(string subscriptionName, string[] eventTypes, SubscriptionCleanupPolicy cleanupPolicy, Func<SerializedEvent, Task<bool>> onMessage, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
