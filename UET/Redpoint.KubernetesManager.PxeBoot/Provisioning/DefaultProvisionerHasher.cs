namespace Redpoint.KubernetesManager.PxeBoot.Provisioning
{
    using Redpoint.Hashing;
    using Redpoint.KubernetesManager.Configuration.Sources;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.Variable;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;

    internal class DefaultProvisionerHasher : IProvisionerHasher
    {
        private readonly IVariableProvider _variableProvider;
        private readonly KubernetesRkmJsonSerializerContext _jsonSerializerContext;

        public DefaultProvisionerHasher(
            IVariableProvider variableProvider,
            IEnumerable<IProvisioningStep> provisioningSteps)
        {
            _variableProvider = variableProvider;
            _jsonSerializerContext = KubernetesRkmJsonSerializerContext.CreateStringEnumWithAdditionalConverters(
                new RkmNodeProvisionerStepJsonConverter(provisioningSteps));
        }

        public string GetProvisionerHash(
            ServerSideVariableContext context)
        {
            var provisionerSpecJson = JsonSerializer.Serialize(
                context.RkmNodeProvisioner.Spec,
                _jsonSerializerContext.RkmNodeProvisionerSpec);
            var effectiveArgumentsJson = JsonSerializer.Serialize(
                _variableProvider.ComputeParameterValuesNodeProvisioningEndpoint(
                    context),
                _jsonSerializerContext.DictionaryStringString);

            return Hash.Sha1AsHexString(
                provisionerSpecJson + "\n" + effectiveArgumentsJson,
                Encoding.UTF8);
        }
    }
}
