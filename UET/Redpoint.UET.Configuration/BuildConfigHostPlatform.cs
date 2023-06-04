namespace Redpoint.UET.Configuration.Plugin
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.UET.Configuration.Dynamic;
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public enum BuildConfigHostPlatform
    {
        Win64,
        Mac
    }
}
