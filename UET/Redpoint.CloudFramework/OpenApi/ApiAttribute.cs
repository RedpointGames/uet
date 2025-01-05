namespace Redpoint.CloudFramework.OpenApi
{
    using System;

    /// <summary>
    /// Indicates that this MVC method should be exposed as an API method in the OpenAPI document.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ApiAttribute : Attribute
    {
    }
}
