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
        public void DeserializeEngineServicePing()
        {
            var bytes = Convert.FromBase64String("FwAAAC9TY3JpcHQvRW5naW5lTWVzc2FnZXMAEgAAAEVuZ2luZVNlcnZpY2VQaW5nAMsnVUfrbwhAgiR9nYxmancAAAAAAoALzuU0UtsI/z839HUoyisAAAAAewANAAoAfQA=");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true);

                var message = new TcpDeserializedMessage();
                archive.Serialize(ref message);

                Assert.Equal(new TopLevelAssetPath("/Script/EngineMessages", "EngineServicePing"), message.AssetPath);
                Assert.Equal(new MessageAddress(new Guid("cb275547-0840-eb6f-9d7d-2482776a668c")), message.SenderAddress);
                Assert.Empty(message.RecipientAddresses.Data);
                Assert.Equal(2, message.MessageScope);
                Assert.Equal(638194159350320000L, message.TimeSent.Ticks);
                Assert.Equal(3155378975999999999L, message.ExpirationTime.Ticks);
                Assert.Empty(message.Annotations.Data);
                Assert.IsType<EngineServicePing>(message.GetMessageData());
            }
        }

        [Fact]
        public void SerializeEngineServicePing()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false);

                var message = new TcpDeserializedMessage
                {
                    SenderAddress = new MessageAddress(new Guid("cb275547-0840-eb6f-9d7d-2482776a668c")),
                    MessageScope = 2,
                    TimeSent = new DateTimeOffset(638194159350320000L, TimeSpan.Zero),
                    ExpirationTime = new DateTimeOffset(3155378975999999999L, TimeSpan.Zero),
                };
                message.SetMessageData(new EngineServicePing());

                archive.Serialize(ref message);

                var data = Convert.ToBase64String(stream.ToArray());

                // @note: This differs from Unreal only in that we don't emit the JSON data as indented / with newlines.
                Assert.Equal(
                    "FwAAAC9TY3JpcHQvRW5naW5lTWVzc2FnZXMAEgAAAEVuZ2luZVNlcnZpY2VQaW5nAMsnVUfrbwhAgiR9nYxmancAAAAAAoALzuU0UtsI/z839HUoyisAAAAAewB9AA==",
                    data);
            }
        }
    }
}