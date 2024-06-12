namespace UET.Commands.Config
{
    using System.Collections.Generic;
    using System.Xml;

    internal class DefaultXmlConfigHelper : IXmlConfigHelper
    {
        private const string _ns = "https://www.unrealengine.com/BuildConfiguration";

        public string? GetValue(XmlDocument document, IReadOnlyList<string> path)
        {
            XmlNode node = document;
            for (var pathIndex = 0; pathIndex < path.Count; pathIndex++)
            {
                var foundChild = false;
                foreach (var child in node.ChildNodes.OfType<XmlElement>())
                {
                    if (child.Name == path[pathIndex] &&
                        child.NamespaceURI == _ns)
                    {
                        node = child;
                        foundChild = true;
                        break;
                    }
                }
                if (!foundChild)
                {
                    return null;
                }
            }
            return node is XmlElement el ? el.InnerText : null;
        }

        public void SetValue(XmlDocument document, IReadOnlyList<string> path, string value)
        {
            XmlNode node = document;
            for (var pathIndex = 0; pathIndex < path.Count; pathIndex++)
            {
                var foundChild = false;
                foreach (var child in node.ChildNodes.OfType<XmlElement>())
                {
                    if (child.Name == path[pathIndex] &&
                        child.NamespaceURI == _ns)
                    {
                        node = child;
                        foundChild = true;
                        break;
                    }
                }
                if (!foundChild)
                {
                    var newElement = node.OwnerDocument!.CreateElement(path[pathIndex], _ns);
                    node.AppendChild(newElement);
                    node = newElement;
                }
            }
            if (node is XmlElement el)
            {
                el.InnerText = value;
            }
        }

        public void DeleteValue(XmlDocument document, IReadOnlyList<string> path)
        {
            XmlNode node = document;
            var foundTarget = false;
            for (var pathIndex = 0; pathIndex < path.Count; pathIndex++)
            {
                var foundChild = false;
                foreach (var child in node.ChildNodes.OfType<XmlElement>())
                {
                    if (child.Name == path[pathIndex] &&
                        child.NamespaceURI == _ns)
                    {
                        node = child;
                        foundChild = true;
                        break;
                    }
                }
                if (!foundChild)
                {
                    break;
                }
                else if (pathIndex == path.Count - 1)
                {
                    // We found the last element, which we absolutely want to remove.
                    foundTarget = true;
                }
            }
            if (foundTarget && node is XmlElement)
            {
                var parent = node.ParentNode!;
                parent.RemoveChild(node);
                node = parent;
            }
            // Then do general cleanup of the hierarchy if we're at an empty leaf.
            while (
                node.ParentNode != null &&
                !(node is XmlDocument) &&
                !node.ChildNodes.OfType<XmlElement>().Any())
            {
                var target = node;
                node = node.ParentNode!;
                node.RemoveChild(target);
            }
        }
    }
}
