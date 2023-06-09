namespace Redpoint.Unreal.TcpMessaging.Tests
{
    using Redpoint.Unreal.Serialization;
    using Redpoint.Unreal.TcpMessaging.MessageTypes;
    using System.Text.Json;

    public class JsonDeserializationTests
    {
        [Fact]
        public void DeserializesToSessionServicePongCorrectly()
        {
            var json = @"{
	""Authorized"": false,
	""BuildDate"": ""Jan 30 2023"",
	""DeviceName"": ""THANOS"",
	""InstanceId"":
	{
		""A"": 48243451,
		""B"": 1199744660,
		""C"": 470315417,
		""D"": -501357788
	},
	""InstanceName"": ""THANOS-35756"",
	""PlatformName"": ""WindowsEditor"",
	""SessionId"":
	{
		""A"": -987697361,
		""B"": 1122596128,
		""C"": 1518865851,
		""D"": -1255206303
	},
	""SessionName"": """",
	""SessionOwner"": ""jrhod"",
	""Standalone"": true
}";
            var value = JsonSerializer.Deserialize(json, typeof(SessionServicePong), new JsonSerializerOptions
            {
                IncludeFields = true,
            });

            Assert.IsType<SessionServicePong>(value);

            var sessionServicePong = (SessionServicePong)value!;

            Assert.Equal("THANOS", sessionServicePong.DeviceName);
        }
    }

    public class TcpDeserializedMessageTests
    {
        [Fact]
        public async Task DeserializeEngineServicePing()
        {
            var bytes = Convert.FromBase64String("FwAAAC9TY3JpcHQvRW5naW5lTWVzc2FnZXMAEgAAAEVuZ2luZVNlcnZpY2VQaW5nAMsnVUfrbwhAgiR9nYxmancAAAAAAoALzuU0UtsI/z839HUoyisAAAAAewANAAoAfQA=");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true, new[] { new TcpMessagingUnrealSerializerRegistry() });

                var message = new Store<TcpDeserializedMessage>(new());
                await archive.Serialize(message);

                Assert.Equal(new TopLevelAssetPath(new Name(new Store<string>("/Script/EngineMessages")), new Name(new Store<string>("EngineServicePing"))), message.V.AssetPath.V);
                Assert.Equal(new MessageAddress(new Guid("cb275547-0840-eb6f-9d7d-2482776a668c")), message.V.SenderAddress.V);
                Assert.Empty(message.V.RecipientAddresses.V.Data);
                Assert.Equal(2, message.V.MessageScope.V);
                Assert.Equal(638194159350320000L, message.V.TimeSent.V.Ticks);
                Assert.Equal(3155378975999999999L, message.V.ExpirationTime.V.Ticks);
                Assert.Empty(message.V.Annotations.V.Data);
                Assert.IsType<EngineServicePing>(message.V.GetMessageData());
            }
        }

        [Fact]
        public async Task SerializeEngineServicePing()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false, new[] { new TcpMessagingUnrealSerializerRegistry() });

                var message = new Store<TcpDeserializedMessage>(new TcpDeserializedMessage
                {
                    SenderAddress = new(new MessageAddress(new Guid("cb275547-0840-eb6f-9d7d-2482776a668c"))),
                    MessageScope = new(2),
                    TimeSent = new(new DateTimeOffset(638194159350320000L, TimeSpan.Zero)),
                    ExpirationTime = new(new DateTimeOffset(3155378975999999999L, TimeSpan.Zero)),
                });
                message.V.SetMessageData(new EngineServicePing());

                await archive.Serialize(message);

                var data = Convert.ToBase64String(stream.ToArray());

                // @note: This differs from Unreal only in that we don't emit the JSON data as indented / with newlines.
                Assert.Equal(
                    "FwAAAC9TY3JpcHQvRW5naW5lTWVzc2FnZXMAEgAAAEVuZ2luZVNlcnZpY2VQaW5nAMsnVUfrbwhAgiR9nYxmancAAAAAAoALzuU0UtsI/z839HUoyisAAAAAewB9AA==",
                    data);
            }
        }

        [Fact]
        public async Task SerializeEnginePingMessageDoesNotCrash()
        {
            using (var memory = new MemoryStream())
            {
                var nextMessageRaw = new TcpDeserializedMessage
                {
                    SenderAddress = new(new MessageAddress(Guid.NewGuid())),
                    RecipientAddresses = new(new ArchiveArray<int, MessageAddress>(new[] { new MessageAddress() })),
                    MessageScope = new(MessageScope.All),
                    TimeSent = new(DateTimeOffset.UtcNow),
                    ExpirationTime = new(DateTimeOffset.MaxValue),
                };
                nextMessageRaw.SetMessageData(new EngineServicePing());

                var memoryArchive = new Archive(memory, false, new[] { new TcpMessagingUnrealSerializerRegistry() });

                Store<TcpDeserializedMessage> nextMessage = new(nextMessageRaw);
                await memoryArchive.Serialize(nextMessage);
            }
        }
    }
}