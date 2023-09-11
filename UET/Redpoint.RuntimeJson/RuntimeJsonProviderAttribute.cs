namespace Redpoint.RuntimeJson
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RuntimeJsonProviderAttribute : Attribute
    {
        public RuntimeJsonProviderAttribute(Type typeOfJsonSerializerContext)
        {
            TypeOfJsonSerializerContext = typeOfJsonSerializerContext;
        }

        public Type TypeOfJsonSerializerContext { get; }
    }
}