namespace Redpoint.CloudFramework.Prefix
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.GoogleInfrastructure;
    using Redpoint.CloudFramework.Models;

    public class GlobalPrefix : IGlobalPrefix
    {
        private readonly IReadOnlyDictionary<string, string> _prefixes;
        private readonly IReadOnlyDictionary<string, string> _reversePrefixes;
        private readonly IGoogleServices _googleServices;
        private static readonly char[] _dashSeparator = new[] { '-' };

        private class GlobalPrefixRegistration : IPrefixRegistration
        {
            private readonly Dictionary<string, string> _prefixes;
            private readonly Dictionary<string, string> _reversePrefixes;

            public GlobalPrefixRegistration()
            {
                _prefixes = new Dictionary<string, string>();
                _reversePrefixes = new Dictionary<string, string>();
            }

            public void RegisterPrefix<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(string prefix) where T : class, IModel, new()
            {
                var kind = new T().GetKind();

                if (_prefixes.TryGetValue(prefix, out string? existingKind))
                {
                    throw new InvalidOperationException($"Prefix '{prefix}' is used by both '{existingKind}' and '{kind}'. Remove the prefix conflict.");
                }
                if (_reversePrefixes.TryGetValue(kind, out string? existingPrefix))
                {
                    throw new InvalidOperationException($"Model kind '{kind}' already has prefix '{existingPrefix}' assigned to it, so you can't also register '{prefix}'. Remove the prefix conflict.");
                }

                _prefixes.Add(prefix, kind);
                _reversePrefixes.Add(kind, prefix);
            }

            public IReadOnlyDictionary<string, string> Prefixes => _prefixes;
            public IReadOnlyDictionary<string, string> ReversePrefixes => _reversePrefixes;
        }

        public GlobalPrefix(
            IPrefixProvider[] prefixProviders,
            IGoogleServices googleServices)
        {
            ArgumentNullException.ThrowIfNull(prefixProviders);

            _googleServices = googleServices;

            var registration = new GlobalPrefixRegistration();
            foreach (var provider in prefixProviders)
            {
                provider.RegisterPrefixes(registration);
            }

            _prefixes = registration.Prefixes;
            _reversePrefixes = registration.ReversePrefixes;
        }

        /// <summary>
        /// Parse a public identifier into a Google Datastore key object.
        /// </summary>
        /// <param name="datastoreNamespace">The datastore namespace of the resulting key.</param>
        /// <param name="identifier">The identifier to parse.</param>
        /// <returns>A key object.</returns>
        public Key Parse(string datastoreNamespace, string identifier)
        {
            var prefix = ParsePathElement(identifier);

            var k = new Key
            {
                PartitionId = new PartitionId(_googleServices.ProjectId, datastoreNamespace)
            };
            k.Path.Add(prefix);
            return k;
        }

        /// <summary>
        /// Parse a public identifier into a Google Datastore key object and verify it's kind.
        /// </summary>
        /// <param name="datastoreNamespace">The datastore namespace of the resulting key.</param>
        /// <param name="identifier">The identifier to parse.</param>
        /// <param name="kind">The resulting kind that the key must match.</param>
        /// <returns>A key object.</returns>
        public Key ParseLimited(string datastoreNamespace, string identifier, string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
            {
                throw new ArgumentNullException(nameof(kind));
            }

            var result = Parse(datastoreNamespace, identifier);
            if (result.Path.Last().Kind != kind)
            {
                throw new IdentifierWrongTypeException(identifier, kind);
            }

            return result;
        }

        /// <summary>
        /// Parse a public identifier into a Google Datastore key object and verify it's kind.
        /// </summary>
        /// <typeparam name="T">The datastore model that this must match.</typeparam>
        /// <param name="datastoreNamespace">The datastore namespace of the resulting key.</param>
        /// <param name="identifier">The identifier to parse.</param>
        /// <returns>A key object.</returns>
        public Key ParseLimited<T>(string datastoreNamespace, string identifier) where T : class, IModel, new()
        {
            return ParseLimited(datastoreNamespace, identifier, new T().GetKind());
        }

        /// <summary>
        /// Parse an internal or public identifier into a Google Datastore key.
        /// </summary>
        /// <param name="datastoreNamespace">The datastore namespace of the resulting key.</param>
        /// <param name="identifier">The identifier to parse.</param>
        /// <returns>A key object.</returns>
        public Key ParseInternal(string datastoreNamespace, string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentNullException(nameof(identifier));
            }

            var isNamespacedKey = false;
            if (identifier.StartsWith('#'))
            {
                isNamespacedKey = true;
                identifier = identifier.Substring(1);
            }

            var identifiers = GlobalPrefix.ParsePipeSeperated(identifier);
            var pathElements = new List<Key.Types.PathElement>();

            string projectId;
            string namespaceId;
            int offset = 0;

            if (isNamespacedKey)
            {
                if (identifiers[0] != "v1")
                {
                    throw new ArgumentException("Namespaced key is not a supported version");
                }

                projectId = identifiers[1];
                namespaceId = identifiers[2];

                offset = 3;
            }
            else
            {
                projectId = _googleServices.ProjectId;
                namespaceId = datastoreNamespace;
            }

            for (var i = offset; i < identifiers.Length; i++)
            {
                var component = identifiers[i];
                var colonIndex = component.IndexOf(':', StringComparison.Ordinal);
                if (colonIndex != -1)
                {
                    var kind = component.Substring(0, colonIndex);
                    var ident = component.Substring(colonIndex + 1);
                    if (ident.StartsWith("id=", StringComparison.Ordinal))
                    {
                        var id = long.Parse(ident.AsSpan("id=".Length), CultureInfo.InvariantCulture);
                        pathElements.Add(new Key.Types.PathElement(kind, id));
                    }
                    else if (ident.StartsWith("name=", StringComparison.Ordinal))
                    {
                        var name = ident.Substring("name=".Length);
                        pathElements.Add(new Key.Types.PathElement(kind, name));
                    }
                    else
                    {
                        throw new InvalidOperationException("Unknown ID or name for identifier '" + component + "'");
                    }
                }
                else
                {
                    pathElements.Add(ParsePathElement(component));
                }
            }

            var k = new Key
            {
                PartitionId = new PartitionId(projectId, namespaceId)
            };
            k.Path.AddRange(pathElements);
            return k;
        }

        /// <summary>
        /// Creates a public identifier from a Datastore key.
        /// </summary>
        /// <param name="key">The datastore key to create an identifier from.</param>
        /// <returns>The public identifier.</returns>
        public string Create(Key key)
        {
            ArgumentNullException.ThrowIfNull(key);
            if (key.Path.Count == 0)
            {
                throw new InvalidOperationException("Datastore key does not have any path elements; can not generate public identifier");
            }
            if (key.Path.Count > 1)
            {
                throw new InvalidOperationException("Datastore key has more than one path element (nested children), can not generate public identifier");
            }
            return CreatePathElement(key.Path[0]);
        }

        /// <summary>
        /// Creates a public or internal identifier from a Datastore key.
        /// </summary>
        /// <param name="key">The datastore key to create an identifier from.</param>
        /// <param name="pathGenerationMode"></param>
        /// <returns>The public or internal identifier.</returns>
        public string CreateInternal(Key key, PathGenerationMode pathGenerationMode = PathGenerationMode.Default)
        {
            ArgumentNullException.ThrowIfNull(key);

            var keyComponents = new List<string>
            {
                "v1",
                key.PartitionId.ProjectId,
                key.PartitionId.NamespaceId
            };

            for (var i = 0; i < key.Path.Count; i++)
            {
                var pathElement = key.Path[i];

                if (!_reversePrefixes.ContainsKey(pathElement.Kind) ||
                    pathGenerationMode == PathGenerationMode.NoShortPathComponents ||
                    pathElement.IdTypeCase != Key.Types.PathElement.IdTypeOneofCase.Id)
                {
                    if (pathElement.IdTypeCase == Key.Types.PathElement.IdTypeOneofCase.Id)
                    {
                        if (pathElement.Id <= 0)
                        {
                            throw new InvalidOperationException("Numeric component must be a positive value");
                        }
                    }

                    if (pathElement.IdTypeCase == Key.Types.PathElement.IdTypeOneofCase.Name)
                    {
                        keyComponents.Add(pathElement.Kind + ":name=" + pathElement.Name.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal));
                    }
                    else
                    {
                        keyComponents.Add(pathElement.Kind + ":id=" + pathElement.Id);
                    }
                }
                else
                {
                    keyComponents.Add(CreatePathElement(pathElement));
                }
            }

            return "#" + string.Join("|", keyComponents);
        }

        private static string[] ParsePipeSeperated(string value)
        {
            var results = new List<string>();
            var buffer = string.Empty;
            var isEscaped = false;
            for (var v = 0; v < value.Length; v++)
            {
                if (isEscaped)
                {
                    buffer += value[v];
                    isEscaped = false;
                }
                else
                {
                    if (value[v] == '\\')
                    {
                        isEscaped = true;
                        continue;
                    }

                    if (value[v] == '|')
                    {
                        results.Add(buffer);
                        buffer = string.Empty;
                        continue;
                    }

                    buffer += value[v];
                }
            }
            if (buffer.Length > 0)
            {
                results.Add(buffer);
            }
            return results.ToArray();
        }

        private Key.Types.PathElement ParsePathElement(string identifier)
        {
            ArgumentNullException.ThrowIfNull(identifier);

            var components = identifier.Split(_dashSeparator, 2);
            if (components.Length != 2)
            {
                throw new IdentifierInvalidException(identifier, "Missing seperator in identifier");
            }

            var prefix = components[0].ToLowerInvariant();
            if (!_prefixes.TryGetValue(prefix, out string? value))
            {
                throw new IdentifierInvalidException(identifier, "Unknown prefix in identifier");
            }
            if (string.IsNullOrWhiteSpace(components[1]))
            {
                throw new IdentifierInvalidException(identifier, "Missing numeric component to identifier");
            }
            var parsable = long.TryParse(components[1], NumberStyles.Any, CultureInfo.InvariantCulture,
                out var numericIdentifier);
            if (!parsable || numericIdentifier.ToString(CultureInfo.InvariantCulture) != components[1])
            {
                throw new IdentifierInvalidException(identifier, "Badly formatted numeric component in identifier");
            }

            return new Key.Types.PathElement(value, numericIdentifier);
        }

        private string CreatePathElement(Key.Types.PathElement pathElement)
        {
            if (string.IsNullOrWhiteSpace(pathElement.Kind))
            {
                throw new ArgumentException("Kind property on datastore key is null or empty", nameof(pathElement));
            }
            if (!_reversePrefixes.TryGetValue(pathElement.Kind, out string? value))
            {
                throw new ArgumentException("No prefix for object kind: " + pathElement.Kind, nameof(pathElement));
            }
            if (pathElement.IdTypeCase != Key.Types.PathElement.IdTypeOneofCase.Id)
            {
                throw new ArgumentException("Only numeric based Datastore keys can be publicly prefixed", nameof(pathElement));
            }
            if (pathElement.Id < 0)
            {
                throw new ArgumentException("Numeric component must be a positive value", nameof(pathElement));
            }
            return value + "-" + pathElement.Id;
        }
    }
}
