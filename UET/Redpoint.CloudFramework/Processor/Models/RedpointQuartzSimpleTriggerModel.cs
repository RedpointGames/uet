namespace Redpoint.CloudFramework.Processor.Models
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository;
    using System;
    using System.Threading.Tasks;

    [Kind("redpointQuartzSimpleTrigger")]
    internal sealed class RedpointQuartzSimpleTriggerModel : Model<RedpointQuartzTriggerModel>
    {
        public static async Task<Key> GetKey(IGlobalRepository globalRepository, string schedName, string triggerName, string triggerGroup)
        {
            var keyFactory = await globalRepository.GetKeyFactoryAsync<RedpointQuartzSimpleTriggerModel>(string.Empty);
            return keyFactory.CreateKey($"{schedName}:{triggerName}:{triggerGroup}");
        }

        [Type(FieldType.String), Indexed]
        public string? schedName { get; set; }

        [Type(FieldType.String), Indexed]
        public string? triggerName { get; set; }

        [Type(FieldType.String), Indexed]
        public string? triggerGroup { get; set; }

        [Type(FieldType.Integer), Indexed, Default(0)]
        public long repeatCount { get; set; }

        [Type(FieldType.Integer), Indexed, Default(0)]
        public long repeatInterval { get; set; }

        [Type(FieldType.Integer), Indexed, Default(0)]
        public long timesTriggered { get; set; }
    }
}
