namespace Redpoint.CloudFramework
{
    using System.Diagnostics.CodeAnalysis;

    internal class DynamicReferencePolicy
    {
        public const DynamicallyAccessedMemberTypes ModelPolicy = DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor;
    }
}
