namespace Redpoint.Unreal.Serialization.SourceCodeGenerator
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    internal sealed class UnrealSyntaxReceiver : ISyntaxContextReceiver
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