using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Io.Mappers
{
    public class MapperContext
    {
        /// <summary>
        /// The ID of the webhook event that is mapping this entity. Webhook event IDs are auto-increment
        /// values in the database, so mappers can use this value to ensure entities don't get updated
        /// with stale data.
        /// </summary>
        public long? WebhookEventId { get; set; }

        /// <summary>
        /// The timestamp at which the webhook event was received. If this mapping isn't associated with a webhook, this will be set to the current timestamp instead.
        /// </summary>
        public Instant WebhookReceivedAt { get; set; } = SystemClock.Instance.GetCurrentInstant();
    }
}
