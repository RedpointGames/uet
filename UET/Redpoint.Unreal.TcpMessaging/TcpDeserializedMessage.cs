namespace Redpoint.Unreal.TcpMessaging
{
    using System;
    using System.Reflection;
    using System.Text.Json.Nodes;
    using Redpoint.Unreal.Serialization;

    public record class TcpDeserializedMessage : ISerializable<TcpDeserializedMessage>
    {
        public TopLevelAssetPath AssetPath = new();
        public MessageAddress SenderAddress = new();
        public ArchiveArray<int, MessageAddress> RecipientAddresses = new();
        public byte MessageScope = new();
        public DateTimeOffset TimeSent = DateTimeOffset.MinValue;
        public DateTimeOffset ExpirationTime = DateTimeOffset.MinValue;
        public ArchiveMap<int, Name, UnrealString> Annotations = new();

        private object? _message;

        public object GetMessageData()
        {
            if (_message == null)
            {
                throw new InvalidOperationException();
            }
            return _message;
        }

        public T GetMessageData<T>() where T : notnull, new()
        {
            if (_message == null)
            {
                throw new InvalidOperationException();
            }
            return (T)_message;
        }

        public void SetMessageData<T>(T message) where T : notnull, new()
        {
            var attr = typeof(T).GetCustomAttribute<TopLevelAssetPathAttribute>();
            if (attr == null)
            {
                throw new InvalidOperationException($"Can't use {typeof(T).FullName} for SetMessageData as it does not have [TopLevelAssetPath]");
            }
            AssetPath = new TopLevelAssetPath(attr.PackageName, attr.AssetName);
            _message = message;
        }

        public static void Serialize(Archive ar, ref TcpDeserializedMessage value)
        {
            ar.Serialize(ref value.AssetPath);
            ar.Serialize(ref value.SenderAddress);
            ar.Serialize(ref value.RecipientAddresses);
            ar.Serialize(ref value.MessageScope);
            ar.Serialize(ref value.TimeSent);
            ar.Serialize(ref value.ExpirationTime);
            ar.Serialize(ref value.Annotations);

            // The JSON content in this value does not have
            // a length prefix, so we can't use DynamicJsonSerialize.
            ar.DynamicJsonFromRemainderOfStream(value.AssetPath, ref value._message);
        }
    }
}
