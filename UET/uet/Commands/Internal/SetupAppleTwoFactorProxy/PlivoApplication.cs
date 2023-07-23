namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    internal class PlivoApplication
    {
        [JsonPropertyName("app_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AppId { get; set; }

        [JsonPropertyName("app_name"), JsonRequired]
        public string? AppName { get; set; }

        [JsonPropertyName("default_app"), JsonRequired]
        public bool DefaultApp { get; set; }

        [JsonPropertyName("enabled"), JsonRequired]
        public bool Enabled { get; set; }

        [JsonPropertyName("answer_url"), JsonRequired]
        public string? AnswerUrl { get; set; }

        [JsonPropertyName("answer_method"), JsonRequired]
        public string? AnswerMethod { get; set; }

        [JsonPropertyName("fallback_answer_url"), JsonRequired]
        public string? FallbackAnswerUrl { get; set; }

        [JsonPropertyName("fallback_answer_method")]
        public string? FallbackAnswerMethod { get; set; }

        [JsonPropertyName("hangup_url"), JsonRequired]
        public string? HangupUrl { get; set; }

        [JsonPropertyName("hangup_method"), JsonRequired]
        public string? HangupMethod { get; set; }

        [JsonPropertyName("message_url"), JsonRequired]
        public string? MessageUrl { get; set; }

        [JsonPropertyName("message_method"), JsonRequired]
        public string? MessageMethod { get; set; }

        [JsonPropertyName("public_uri"), JsonRequired]
        public bool PublicUri { get; set; }

        [JsonPropertyName("sip_uri"), JsonRequired]
        public string? SipUri { get; set; }

        [JsonPropertyName("subaccount")]
        public string? Subaccount { get; set; }

        [JsonPropertyName("log_incoming_messages")]
        public bool LogIncomingMessages { get; set; }
    }
}
