namespace Redpoint.CloudFramework.Processor
{
    using Google.Cloud.Datastore.V1;
    using NodaTime;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository;
    using System;
    using System.Threading.Tasks;

    [Kind("redpointScheduledJob")]
    internal sealed class ScheduledJobModel : Model<ScheduledJobModel>
    {
        public static async Task<Key> GetKey(IGlobalRepository globalRepository, string key)
        {
            var keyFactory = await globalRepository.GetKeyFactoryAsync<ScheduledJobModel>(string.Empty);
            return keyFactory.CreateKey(key);
        }

        [Type(FieldType.Timestamp)]
        public Instant? dateLastCompletedUtc { get; set; }
    }
}
