namespace Redpoint.Unreal.Serialization.SourceCodeGenerator
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Text;
    using System.Text;

    [Generator(LanguageNames.CSharp)]
    public class UnrealSerializationSourceGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxContextReceiver is UnrealSyntaxReceiver receiver))
            {
                return;
            }

            if (receiver.Entries.Count == 0)
            {
                return;
            }

            var sourceBuilder = new StringBuilder();
            sourceBuilder.Append($@"using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Redpoint.Unreal.Serialization;

#nullable enable
");
            foreach (var entry in receiver.Entries)
            {
                var topLevelEntries = new List<(string fullyQualifiedClassName, string className, string packageName, string assetName)>();
                foreach (var type in entry.TopLevelAssetPathClassNames)
                {
                    var referencedClass = context.Compilation.GetTypeByMetadataName(type);
                    if (referencedClass == null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create("REFM", "UnrealSerializer", $"Unable to find referenced type '{type}' in compilation unit.", DiagnosticSeverity.Error, DiagnosticSeverity.Error, true, 0));
                        continue;
                    }
                    var topLevelAssetPath = referencedClass.GetAttributes().FirstOrDefault(x => x.AttributeClass != null && x.AttributeClass.ToDisplayString() == "Redpoint.Unreal.Serialization.TopLevelAssetPathAttribute");
                    if (topLevelAssetPath == null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create("ATRM", "UnrealSerializer", $"Referenced type '{type}' is missing [TopLevelAssetPath] attribute.", DiagnosticSeverity.Error, DiagnosticSeverity.Error, true, 0));
                        continue;
                    }
                    var packageName = topLevelAssetPath.ConstructorArguments[0].Value as string;
                    var assetName = topLevelAssetPath.ConstructorArguments[1].Value as string;
                    if (packageName != null && assetName != null)
                    {
                        topLevelEntries.Add((type, referencedClass.Name, packageName, assetName));
                    }
                    else
                    {
                        context.ReportDiagnostic(Diagnostic.Create("INVC", "UnrealSerializer", $"{topLevelAssetPath.ConstructorArguments[0].Value!.GetType().FullName}", DiagnosticSeverity.Error, DiagnosticSeverity.Error, true, 0));
                    }
                }

                sourceBuilder.Append($@"
namespace {entry.Namespace}
{{
    partial class {entry.Class}
    {{
        public bool CanHandleStoreType(Type type)
        {{");
                foreach (var type in entry.SerializableClassNames)
                {
                    sourceBuilder.Append($@"
            if (type == typeof({type})) {{ return true; }}");
                }
                sourceBuilder.Append($@"
            return false;
        }}

        public Task SerializeStoreType<T>(Archive ar, Store<T> value)
        {{");
                foreach (var type in entry.SerializableClassNames)
                {
                    sourceBuilder.Append($@"
            if (typeof(T) == typeof({type})) {{ return {type}.Serialize(ar, (Store<{type}>)(object)value); }}");
                }
                sourceBuilder.Append($@"
            throw new InvalidOperationException(""This type isn't supported by this serializable registry."");
        }}

        public bool CanHandleTopLevelAssetPath(TopLevelAssetPath topLevelAssetPath)
        {{");
                foreach (var (_, _, packageName, assetName) in topLevelEntries)
                {
                    sourceBuilder.Append($@"
            if (topLevelAssetPath.Is(""{packageName}"", ""{assetName}"")) {{ return true; }}");
                }
                sourceBuilder.Append($@"
            return false;
        }}

        public object? DeserializeTopLevelAssetPath(TopLevelAssetPath topLevelAssetPath, string json, JsonSerializerOptions jsonOptions)
        {{");
                foreach (var (_, type, packageName, assetName) in topLevelEntries)
                {
                    sourceBuilder.Append($@"
            if (topLevelAssetPath.Is(""{packageName}"", ""{assetName}"")) {{ return JsonSerializer.Deserialize(json, (new {entry.Class}_JsonSerializerContext(new JsonSerializerOptions(jsonOptions))).{type}); }}");
                }
                sourceBuilder.Append($@"
            throw new InvalidOperationException(""This type isn't supported by this serializable registry."");
        }}

        public string SerializeTopLevelAssetPath(TopLevelAssetPath topLevelAssetPath, object? value, JsonSerializerOptions jsonOptions)
        {{");
                foreach (var (fullyQualifiedType, type, packageName, assetName) in topLevelEntries)
                {
                    sourceBuilder.Append($@"
            if (topLevelAssetPath.Is(""{packageName}"", ""{assetName}"")) {{ return JsonSerializer.Serialize<{fullyQualifiedType}>(({fullyQualifiedType})value!, (new {entry.Class}_JsonSerializerContext(new JsonSerializerOptions(jsonOptions))).{type}); }}");
                }
                sourceBuilder.Append($@"
            throw new InvalidOperationException(""This type isn't supported by this serializable registry."");
        }}
    }}
");
                // @note: Source generators can't be chained, so we just have to manually create the type in our project.
                /*
                if (topLevelEntries.Count > 0)
                {
                    sourceBuilder.AppendLine();
                    foreach (var topLevelEntry in topLevelEntries)
                    {
                        sourceBuilder.AppendLine($"    [JsonSerializable(typeof({topLevelEntry.className}))]");
                    }
                    sourceBuilder.Append($@"    internal partial class {entry.Class}_JsonSerializerContext : JsonSerializerContext
    {{
    }}
");
                }
                */
                sourceBuilder.Append($@"}}
");
            }
            context.AddSource("UnrealSerialization.g.cs", sourceBuilder.ToString());
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            var unrealSyntaxReceiver = new UnrealSyntaxReceiver();
            context.RegisterForSyntaxNotifications(() => unrealSyntaxReceiver);
        }
    }

    internal class UnrealSerializerEntry
    {
        public string? Namespace { get; set; }
        public string? Class { get; set; }
        public List<string> SerializableClassNames { get; } = new List<string>();
        public List<string> TopLevelAssetPathClassNames { get; } = new List<string>();
    }

    internal class UnrealSyntaxReceiver : ISyntaxContextReceiver
    {
        public List<UnrealSerializerEntry> Entries = new List<UnrealSerializerEntry>();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (context.Node is ClassDeclarationSyntax classDeclarationSyntax)
            {
                var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax);
                if (classSymbol == null)
                {
                    return;
                }
                var registryAttribute = classSymbol.GetAttributes().FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == "Redpoint.Unreal.Serialization.SerializerRegistryAttribute");
                if (registryAttribute != null)
                {
                    var serializableTypeAttributes = classSymbol.GetAttributes().Where(x => x.AttributeClass?.ToDisplayString() == "Redpoint.Unreal.Serialization.SerializerRegistryAddSerializableAttribute").ToArray();
                    var topLevelPathAttributes = classSymbol.GetAttributes().Where(x => x.AttributeClass?.ToDisplayString() == "Redpoint.Unreal.Serialization.SerializerRegistryAddTopLevelAssetPathAttribute").ToArray();
                    if (serializableTypeAttributes.Length > 0 || topLevelPathAttributes.Length > 0)
                    {
                        // This is a serializer we need to generate code for.
                        var entry = new UnrealSerializerEntry
                        {
                            Namespace = classSymbol.ContainingNamespace?.ToDisplayString(),
                            Class = classSymbol.Name,
                        };
                        foreach (var typeAttribute in serializableTypeAttributes)
                        {
                            var targetType = typeAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? string.Empty;
                            entry.SerializableClassNames.Add(targetType);
                        }
                        foreach (var typeAttribute in topLevelPathAttributes)
                        {
                            var targetType = typeAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? string.Empty;
                            entry.TopLevelAssetPathClassNames.Add(targetType);
                        }
                        Entries.Add(entry);
                    }
                }
            }
        }
    }
}