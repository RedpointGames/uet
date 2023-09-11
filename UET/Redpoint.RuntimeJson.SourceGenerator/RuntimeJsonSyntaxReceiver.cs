namespace Redpoint.RuntimeJson.SourceGenerator
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    internal sealed class RuntimeJsonSyntaxReceiver : ISyntaxContextReceiver
    {
        public List<RuntimeJsonSerializerEntry> Entries = new List<RuntimeJsonSerializerEntry>();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (context.Node is ClassDeclarationSyntax classDeclarationSyntax)
            {
                var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax);
                if (classSymbol == null)
                {
                    return;
                }
                var providerAttribute = classSymbol.GetAttributes().FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == "Redpoint.RuntimeJson.RuntimeJsonProviderAttribute");
                if (providerAttribute != null)
                {
                    var serializableTypeAttributes = classSymbol.GetAttributes().Where(x => x.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonSerializableAttribute").ToArray();
                    if (serializableTypeAttributes.Length > 0)
                    {
                        // This is a serializer we need to generate code for.
                        var entry = new RuntimeJsonSerializerEntry
                        {
                            Namespace = classSymbol.ContainingNamespace?.ToDisplayString(),
                            Class = classSymbol.Name,
                            JsonSerializerContextType = providerAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? string.Empty,
                        };
                        foreach (var typeAttribute in serializableTypeAttributes)
                        {
                            var targetType = typeAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? string.Empty;
                            entry.SerializableClassNames.Add(targetType);
                        }
                        Entries.Add(entry);
                    }
                }
            }
        }
    }
}