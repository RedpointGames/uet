namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using System;
    using System.Text.Json;

    public class JsonValueWasIncorrectKindException : Exception
    {
        public JsonValueWasIncorrectKindException(string propertyName, JsonValueKind actualKind, JsonValueKind expectedKind)
            : base($"The value of the '{propertyName}' property has kind '{actualKind}', but this code expects values with a kind '{expectedKind}'.")
        {
        }
    }
}
