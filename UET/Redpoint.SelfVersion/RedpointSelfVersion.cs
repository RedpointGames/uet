using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

internal class RedpointSelfVersion
{
    [return: NotNullIfNotNull("attribute")]
    public static string? GetInformationalVersion(AssemblyInformationalVersionAttribute? attribute)
    {
        if (attribute == null)
        {
            return null;
        }
        var attributeVersion = attribute.InformationalVersion ?? string.Empty;
        var plusIndex = attributeVersion.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex != -1)
        {
            return attributeVersion.Substring(0, plusIndex);
        }
        return attributeVersion;
    }

    public static string? GetInformationalVersion()
    {
        return GetInformationalVersion(
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>());
    }
}