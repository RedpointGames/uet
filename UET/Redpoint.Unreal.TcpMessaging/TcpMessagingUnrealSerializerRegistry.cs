using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Redpoint.Unreal.TcpMessaging.Tests")]

namespace Redpoint.Unreal.TcpMessaging
{
    using Redpoint.Unreal.Serialization;
    using Redpoint.Unreal.TcpMessaging.MessageTypes;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    [SerializerRegistry]
    [SerializerRegistryAddSerializable(typeof(TcpDeserializedMessage))]
    [SerializerRegistryAddSerializable(typeof(TcpMessageHeader))]
    [SerializerRegistryAddSerializable(typeof(ArchiveArray<int, MessageAddress>))]
    [SerializerRegistryAddSerializable(typeof(ArchiveMap<int, Name, UnrealString>))]
    [SerializerRegistryAddTopLevelAssetPath(typeof(AutomationWorkerFindWorkers))]
    [SerializerRegistryAddTopLevelAssetPath(typeof(AutomationWorkerFindWorkersResponse))]
    [SerializerRegistryAddTopLevelAssetPath(typeof(AutomationWorkerRequestTests))]
    [SerializerRegistryAddTopLevelAssetPath(typeof(AutomationWorkerRequestTestsReplyComplete))]
    [SerializerRegistryAddTopLevelAssetPath(typeof(AutomationWorkerRunTests))]
    [SerializerRegistryAddTopLevelAssetPath(typeof(AutomationWorkerRunTestsReply))]
    [SerializerRegistryAddTopLevelAssetPath(typeof(AutomationWorkerSingleTestReply))]
    [SerializerRegistryAddTopLevelAssetPath(typeof(EngineServicePing))]
    [SerializerRegistryAddTopLevelAssetPath(typeof(EngineServicePong))]
    [SerializerRegistryAddTopLevelAssetPath(typeof(PortalRpcLocateServer))]
    [SerializerRegistryAddTopLevelAssetPath(typeof(SessionServiceLog))]
    [SerializerRegistryAddTopLevelAssetPath(typeof(SessionServiceLogSubscribe))]
    [SerializerRegistryAddTopLevelAssetPath(typeof(SessionServiceLogUnsubscribe))]
    [SerializerRegistryAddTopLevelAssetPath(typeof(SessionServicePing))]
    [SerializerRegistryAddTopLevelAssetPath(typeof(SessionServicePong))]
    internal partial class TcpMessagingUnrealSerializerRegistry : ISerializerRegistry
    {
    }


    [JsonSerializable(typeof(AutomationWorkerFindWorkers))]
    [JsonSerializable(typeof(AutomationWorkerFindWorkersResponse))]
    [JsonSerializable(typeof(AutomationWorkerRequestTests))]
    [JsonSerializable(typeof(AutomationWorkerRequestTestsReplyComplete))]
    [JsonSerializable(typeof(AutomationWorkerRunTests))]
    [JsonSerializable(typeof(AutomationWorkerRunTestsReply))]
    [JsonSerializable(typeof(AutomationWorkerSingleTestReply))]
    [JsonSerializable(typeof(EngineServicePing))]
    [JsonSerializable(typeof(EngineServicePong))]
    [JsonSerializable(typeof(PortalRpcLocateServer))]
    [JsonSerializable(typeof(SessionServiceLog))]
    [JsonSerializable(typeof(SessionServiceLogSubscribe))]
    [JsonSerializable(typeof(SessionServiceLogUnsubscribe))]
    [JsonSerializable(typeof(SessionServicePing))]
    [JsonSerializable(typeof(SessionServicePong))]
    internal partial class TcpMessagingUnrealSerializerRegistry_JsonSerializerContext : JsonSerializerContext
    {
    }
}
