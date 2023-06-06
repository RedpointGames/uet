namespace Redpoint.Unreal.TcpMessaging
{
    using System;
    using System.Reflection;
    using Redpoint.Unreal.Serialization;

    public record class TcpDeserializedMessage : ISerializable<TcpDeserializedMessage>
    {
        public Store<TopLevelAssetPath> AssetPath = new(new());
        public Store<MessageAddress> SenderAddress = new(new());
        public Store<ArchiveArray<int, MessageAddress>> RecipientAddresses = new(new());
        public Store<byte> MessageScope = new(new());
        public Store<DateTimeOffset> TimeSent = new(DateTimeOffset.MinValue);
        public Store<DateTimeOffset> ExpirationTime = new(DateTimeOffset.MinValue);
        public Store<ArchiveMap<int, Name, UnrealString>> Annotations = new(new());

        private Store<object?> _message = new(null);

        public object GetMessageData()
        {
            if (_message.V == null)
            {
                throw new InvalidOperationException();
            }
            return _message.V;
        }

        public T GetMessageData<T>() where T : notnull, new()
        {
            if (_message.V == null)
            {
                throw new InvalidOperationException();
            }
            return (T)_message.V;
        }

        public void SetMessageData<T>(T message) where T : notnull, new()
        {
            var attr = typeof(T).GetCustomAttribute<TopLevelAssetPathAttribute>();
            if (attr == null)
            {
                throw new InvalidOperationException($"Can't use {typeof(T).FullName} for SetMessageData as it does not have [TopLevelAssetPath]");
            }
            AssetPath.V = new TopLevelAssetPath(attr.PackageName, attr.AssetName);
            _message.V = message;
        }

        public static async Task Serialize(Archive ar, Store<TcpDeserializedMessage> value)
        {
            await ar.Serialize(value.V.AssetPath);
            await ar.Serialize(value.V.SenderAddress);
            await ar.Serialize(value.V.RecipientAddresses);
            await ar.Serialize(value.V.MessageScope);
            await ar.Serialize(value.V.TimeSent);
            await ar.Serialize(value.V.ExpirationTime);
            await ar.Serialize(value.V.Annotations);

            // The JSON content in this value does not have
            // a length prefix, so we can't use DynamicJsonSerialize.
            await ar.DynamicJsonFromRemainderOfStream(value.V.AssetPath, value.V._message!);
        }
    }
}
