namespace Redpoint.CloudFramework.Repository.Validation
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Converters.Model;
    using System.Threading.Tasks;

    internal class DefaultModelValidator : IModelValidator
    {
        private readonly IModelConverter<Entity> _entityConverter;
        private readonly IModelConverter<string> _jsonConverter;

        public DefaultModelValidator(
            IModelConverter<Entity> entityConverter,
            IModelConverter<string> jsonConverter)
        {
            _entityConverter = entityConverter;
            _jsonConverter = jsonConverter;
        }

        public void ValidateModelFields<T>() where T : Model<T>, new()
        {
            var t = new T();

            var key = new Key
            {
                PartitionId = new PartitionId("test"),
                Path = { new Key.Types.PathElement { Kind = t.GetKind(), Id = 1 } }
            };

            t.Key = key;

            var entity = _entityConverter.To(string.Empty, t, true, _ => key);
            var json = _jsonConverter.To(string.Empty, t, true, _ => key);
            var fromEntity = _entityConverter.From<T>(string.Empty, entity);
            var fromJson = _jsonConverter.From<T>(string.Empty, json);
        }
    }
}
