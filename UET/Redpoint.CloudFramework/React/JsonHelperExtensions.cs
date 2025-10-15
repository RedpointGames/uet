namespace Redpoint.CloudFramework.React
{
    using Microsoft.AspNetCore.Mvc.Rendering;
    using System.IO;
    using System.Text.Encodings.Web;
    using System.Text.Json;

    public static class JsonHelperExtensions
    {
        public static object? SerializeForReact(this IJsonHelper Json, HtmlEncoder encoder, object input)
        {
            ArgumentNullException.ThrowIfNull(Json);

            return input;
        }
    }
}
