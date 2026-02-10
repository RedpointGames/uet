namespace Io.Database
{
    using NodaTime;

    public class WebhookEventEntity : IHasId
    {
        public long Id { get; set; }

        public long? ProjectId { get; set; }

        public Instant? CreatedAt { get; set; }

        public string? ObjectKind { get; set; }

        public string? Data { get; set; }

        public string? ReservedBy { get; set; }

        public Instant? ReservationTimeout { get; set; }

        public bool? Done { get; set; }
    }
}
