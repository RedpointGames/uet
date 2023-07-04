namespace UET.Commands.Internal.GenerateJsonSchema
{
    using System.Threading.Tasks;
    using System.IO;

    internal interface IJsonSchemaGenerator
    {
        ValueTask GenerateAsync(Stream outputStream);
    }
}
