namespace Redpoint.KubernetesManager.PxeBoot.Variable
{
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning;

    internal class ServerSideVariableContext
    {
        public required RkmNode RkmNode { get; init; }

        public required RkmNodeGroup RkmNodeGroup { get; init; }

        public required RkmNodeProvisioner RkmNodeProvisioner { get; init; }

        public required string ApiHostAddress { get; init; }

        public required int ApiHostHttpPort { get; init; }

        public required int ApiHostHttpsPort { get; init; }

        public static ServerSideVariableContext FromNodeGroupProvisioner(
            INodeProvisioningEndpointContext context)
        {
            return new ServerSideVariableContext
            {
                RkmNode = context.RkmNode!,
                RkmNodeGroup = context.RkmNodeGroup!,
                RkmNodeProvisioner = context.RkmNodeGroupProvisioner!,
                ApiHostAddress = context.HostAddress,
                ApiHostHttpPort = context.HostHttpPort,
                ApiHostHttpsPort = context.HostHttpsPort,
            };
        }

        public static ServerSideVariableContext FromNodeProvisionerWithoutContextLoadedObjects(
            INodeProvisioningEndpointContext context,
            RkmNode rkmNode,
            RkmNodeGroup rkmNodeGroup,
            RkmNodeProvisioner rkmNodeProvisioner)
        {
            return new ServerSideVariableContext
            {
                RkmNode = rkmNode,
                RkmNodeGroup = rkmNodeGroup,
                RkmNodeProvisioner = rkmNodeProvisioner,
                ApiHostAddress = context.HostAddress,
                ApiHostHttpPort = context.HostHttpPort,
                ApiHostHttpsPort = context.HostHttpsPort,
            };
        }
    }
}
