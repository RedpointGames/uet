namespace Redpoint.CloudFramework.Processor.Models
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository;
    using System.Threading.Tasks;

    [Kind("redpointQuartzPausedTriggerGroup")]
    internal sealed class RedpointQuartzPausedTriggerGroupModel : Model<RedpointQuartzTriggerModel>
    {
        public static async Task<Key> GetKey(IGlobalRepository globalRepository, string schedName, string triggerGroup)
        {
            var keyFactory = await globalRepository.GetKeyFactoryAsync<RedpointQuartzPausedTriggerGroupModel>(string.Empty);
            return keyFactory.CreateKey($"{schedName}:{triggerGroup}");
        }

        // @todo
    }
}
