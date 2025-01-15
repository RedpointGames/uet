namespace Redpoint.CloudFramework.Event
{
    public enum EventProcessingOutcome
    {
        /// <summary>
        /// Indicates the event should be ignored by this processor.
        /// </summary>
        IgnoreEvent,

        /// <summary>
        /// Indicates the event should be retried later.
        /// </summary>
        RetryLater,

        /// <summary>
        /// Indicates that this processor has processed the event.
        /// </summary>
        Complete
    }
}
