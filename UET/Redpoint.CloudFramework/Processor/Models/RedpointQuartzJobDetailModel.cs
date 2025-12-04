namespace Redpoint.CloudFramework.Processor.Models
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [Kind("redpointQuartzJobDetail")]
    internal sealed class RedpointQuartzJobDetailModel : Model<RedpointQuartzJobDetailModel>
    {
        public static async Task<Key> GetKey(IGlobalRepository globalRepository, string schedName, string jobName, string jobGroup)
        {
            var keyFactory = await globalRepository.GetKeyFactoryAsync<RedpointQuartzJobDetailModel>(string.Empty);
            return keyFactory.CreateKey($"{schedName}:{jobName}:{jobGroup}");
        }

        [Type(FieldType.String), Indexed]
        public string? schedName { get; set; }

        [Type(FieldType.String), Indexed]
        public string? jobName { get; set; }

        [Type(FieldType.String), Indexed]
        public string? jobGroup { get; set; }

        [Type(FieldType.String)]
        public string? description { get; set; }

        [Type(FieldType.String), Indexed]
        public string? jobClassName { get; set; }

        [Type(FieldType.Boolean), Indexed, Default(false)]
        public bool isDurable { get; set; }

        [Type(FieldType.Boolean), Indexed, Default(false)]
        public bool isNonconcurrent { get; set; }

        [Type(FieldType.Boolean), Indexed, Default(false)]
        public bool isUpdateData { get; set; }

        [Type(FieldType.Boolean), Indexed, Default(false)]
        public bool requestsRecovery { get; set; }

        [Type(FieldType.String)]
        public string? jobDataBase64 { get; set; }
    }
}
