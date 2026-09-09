namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    internal enum KeyConverterPolicy
    {
        // Same namespace policy as enclosing model.
        Default,

        // A reference to a local key from a global model.
        Local,

        // A reference to a global key from a local model.
        Global,

        // An unsafe reference to a key in any namespace.
        Unsafe,
    }
}
