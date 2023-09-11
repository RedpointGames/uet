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

        internal void SetMessageDataUnsafe(object message)
        {
            _message.V = message;
        }

        public static async Task Serialize(Archive ar, Store<TcpDeserializedMessage> value)
        {
            if (ar == null) throw new ArgumentNullException(nameof(ar));
            if (value == null) throw new ArgumentNullException(nameof(value));

            await ar.Serialize(value.V.AssetPath).ConfigureAwait(false);
            await ar.Serialize(value.V.SenderAddress).ConfigureAwait(false);
            await ar.Serialize(value.V.RecipientAddresses).ConfigureAwait(false);
            await ar.Serialize(value.V.MessageScope).ConfigureAwait(false);
            await ar.Serialize(value.V.TimeSent).ConfigureAwait(false);
            await ar.Serialize(value.V.ExpirationTime).ConfigureAwait(false);
            await ar.Serialize(value.V.Annotations).ConfigureAwait(false);

            // The JSON content in this value does not have
            // a length prefix, so we can't use DynamicJsonSerialize.
            await ar.DynamicJsonFromRemainderOfStream(value.V.AssetPath, value.V._message!).ConfigureAwait(false);
        }

        public LegacyTcpDeserializedMessage ToLegacyMessage()
        {
            var message = new LegacyTcpDeserializedMessage
            {
                AssetPath = AssetPath.V.AssetName,
                SenderAddress = SenderAddress,
                RecipientAddresses = RecipientAddresses,
                MessageScope = MessageScope,
                TimeSent = TimeSent,
                ExpirationTime = ExpirationTime,
                Annotations = Annotations,
            };
            message.SetMessageDataUnsafe(_message.V!);
            return message;
        }
    }
}
