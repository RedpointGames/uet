namespace Redpoint.UET.Automation.TestNotification.Io
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    [JsonSerializable(typeof(List<IoChange>))]
    internal partial class IoJsonSerializerContext : JsonSerializerContext
    {
    }
}
