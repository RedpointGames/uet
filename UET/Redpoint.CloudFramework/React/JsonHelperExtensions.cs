namespace Redpoint.CloudFramework.React
{
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Newtonsoft.Json;
    using System.IO;
    using System.Text.Encodings.Web;

    public static class JsonHelperExtensions
    {
        public static object? SerializeForReact(this IJsonHelper Json, HtmlEncoder encoder, object input)
        {
            ArgumentNullException.ThrowIfNull(Json);

            string json;
            using (var writer = new StringWriter())
            {
                Json.Serialize(input).WriteTo(writer, encoder);
                json = writer.ToString();
            }
            return JsonConvert.DeserializeObject(json);
        }
    }
}
