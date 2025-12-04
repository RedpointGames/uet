namespace Redpoint.CloudFramework.Processor.Models
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository;
    using System.Threading.Tasks;

    [Kind("redpointQuartzSimpropTrigger")]
    internal sealed class RedpointQuartzSimpropTriggerModel : Model<RedpointQuartzTriggerModel>
    {
        public static async Task<Key> GetKey(IGlobalRepository globalRepository, string schedName, string triggerName, string triggerGroup)
        {
            var keyFactory = await globalRepository.GetKeyFactoryAsync<RedpointQuartzSimpropTriggerModel>(string.Empty);
            return keyFactory.CreateKey($"{schedName}:{triggerName}:{triggerGroup}");
        }

        // @todo
    }
}
