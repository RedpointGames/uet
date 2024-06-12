namespace UET.Commands.Config
{
    using System.Collections.Generic;
    using System.Xml;

    internal interface IXmlConfigHelper
    {
        string? GetValue(XmlDocument document, IReadOnlyList<string> path);

        void SetValue(XmlDocument document, IReadOnlyList<string> path, string value);

        void DeleteValue(XmlDocument document, IReadOnlyList<string> path);
    }
}
