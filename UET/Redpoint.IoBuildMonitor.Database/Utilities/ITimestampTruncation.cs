namespace Io.Database.Utilities
{
    using NodaTime;

    public interface ITimestampTruncation
    {
        Instant TruncateToMinute(Instant timestamp);
    }
}
