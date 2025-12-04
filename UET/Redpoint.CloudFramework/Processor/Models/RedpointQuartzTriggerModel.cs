namespace Redpoint.CloudFramework.Processor.Models
{
    using Google.Cloud.Datastore.V1;
    using NodaTime;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository;
    using System;
    using System.Threading.Tasks;

    [Kind("redpointQuartzTrigger")]
    internal sealed class RedpointQuartzTriggerModel : Model<RedpointQuartzTriggerModel>
    {
        public static async Task<Key> GetKey(IGlobalRepository globalRepository, string schedName, string triggerName, string triggerGroup)
        {
            var keyFactory = await globalRepository.GetKeyFactoryAsync<RedpointQuartzTriggerModel>(string.Empty);
            return keyFactory.CreateKey($"{schedName}:{triggerName}:{triggerGroup}");
        }

        [Type(FieldType.String), Indexed]
        public string? schedName { get; set; }

        [Type(FieldType.String), Indexed]
        public string? triggerName { get; set; }

        [Type(FieldType.String), Indexed]
        public string? triggerGroup { get; set; }

        [Type(FieldType.String), Indexed]
        public string? jobName { get; set; }

        [Type(FieldType.String), Indexed]
        public string? jobGroup { get; set; }

        [Type(FieldType.String)]
        public string? description { get; set; }

        [Type(FieldType.Timestamp), Indexed]
        public Instant? nextFireTime { get; set; }

        [Type(FieldType.Timestamp), Indexed]
        public Instant? prevFireTime { get; set; }

        [Type(FieldType.Integer), Indexed]
        public long? priority { get; set; }

        [Type(FieldType.String), Indexed]
        public string? triggerState { get; set; }

        [Type(FieldType.String), Indexed]
        public string? triggerType { get; set; }

        [Type(FieldType.Timestamp), Indexed]
        public Instant? startTime { get; set; }

        [Type(FieldType.Timestamp), Indexed]
        public Instant? endTime { get; set; }

        [Type(FieldType.String), Indexed]
        public string? calendarName { get; set; }

        [Type(FieldType.Integer), Indexed]
        public long? misfireInstr { get; set; }

        [Type(FieldType.String)]
        public string? jobDataBase64 { get; set; }
    }
}
