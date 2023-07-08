namespace Redpoint.RuntimeJson
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RuntimeJsonProviderAttribute : Attribute
    {
        public RuntimeJsonProviderAttribute(Type typeOfJsonSerializerContext)
        {
            TypeOfJsonSerializerContext = typeOfJsonSerializerContext;
        }

        public Type TypeOfJsonSerializerContext { get; }
    }
}