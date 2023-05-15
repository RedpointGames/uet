namespace Redpoint.Unreal.TcpMessaging.MessageTypes
{
    using Redpoint.Unreal.Serialization;
    using System.Text.Json.Serialization;

    [TopLevelAssetPath("/Script/SessionMessages", "SessionServicePing")]
    public record class SessionServicePing
    {
        [JsonPropertyName("UserName")]
        public string UserName = string.Empty;
    }
}
