namespace Redpoint.CloudFramework.Processor.Models
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository;
    using System.Threading.Tasks;

    [Kind("redpointQuartzFiredTrigger")]
    internal sealed class RedpointQuartzFiredTriggerModel : Model<RedpointQuartzTriggerModel>
    {
        public static async Task<Key> GetKey(IGlobalRepository globalRepository, string schedName, string entryId)
        {
            var keyFactory = await globalRepository.GetKeyFactoryAsync<RedpointQuartzFiredTriggerModel>(string.Empty);
            return keyFactory.CreateKey($"{schedName}:{entryId}");
        }

        // @todo
    }
}
