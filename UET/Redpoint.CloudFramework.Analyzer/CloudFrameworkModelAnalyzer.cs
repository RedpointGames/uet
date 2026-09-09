namespace Redpoint.CloudFramework.Analyzer
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CloudFrameworkModelAnalyzer : DiagnosticAnalyzer
    {
        public const string InheritDiagnosticId = "CloudFrameworkModelAnalyzerInherit";
        public const string SealedDiagnosticId = "CloudFrameworkModelAnalyzerSealed";
        public const string KeyDiagnosticId = "CloudFrameworkModelAnalyzerKey";

#pragma warning disable RS2008 // Enable analyzer release tracking

        private static readonly DiagnosticDescriptor _inheritRule = new DiagnosticDescriptor(
            InheritDiagnosticId,
            "Inheritance of Model<T> should have T match inheriting type",
            "Type {0} must inherit from Model<{0}>",
            "Cloud Framework",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Cloud Framework models must inherit from Model<T>, where T matches the type that is inheriting.");

        private static readonly DiagnosticDescriptor _sealedRule = new DiagnosticDescriptor(
            SealedDiagnosticId,
            "Types implementing Model<T> should be sealed",
            "Type {0} must be sealed",
            "Cloud Framework",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Cloud Framework models must be sealed.");

        private static readonly DiagnosticDescriptor _datastoreKeyRule = new DiagnosticDescriptor(
            KeyDiagnosticId,
            "Fields should be declared with UntypedKey or Key<T>",
            "Field {0} must be UntypedKey or Key<T>",
            "Cloud Framework",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Cloud Framework models must not use the internal Datastore key type for properties.");

#pragma warning restore RS2008 // Enable analyzer release tracking

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(_inheritRule, _sealedRule, _datastoreKeyRule);
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
        }

        private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (!(context.Node is ClassDeclarationSyntax classDeclaration))
            {
                return;
            }

            var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

            var baseType = symbol.BaseType;
            if (baseType != null &&
                baseType.IsGenericType &&
                baseType.TypeArguments.Length >= 1)
            {
                var unboundGeneric = baseType.ConstructUnboundGenericType();

                if (unboundGeneric.ContainingNamespace.ToString() == "Redpoint.CloudFramework.Models" &&
                    unboundGeneric.Name == "Model")
                {
                    if (!SymbolEqualityComparer.Default.Equals(symbol, baseType.TypeArguments[0]))
                    {
                        var diagnostic = Diagnostic.Create(_inheritRule, symbol.Locations[0], symbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }

                    if (!symbol.IsSealed)
                    {
                        var diagnostic = Diagnostic.Create(_sealedRule, symbol.Locations[0], symbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static ITypeSymbol NormalizeType(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
            {
                return NormalizeType(arrayTypeSymbol.ElementType);
            }

            if (typeSymbol.ContainingNamespace != null &&
                typeSymbol.ContainingNamespace.ToString() == "System" &&
                typeSymbol.Name == "Nullable")
            {
                if (typeSymbol is INamedTypeSymbol namedTypeSymbol &&
                    namedTypeSymbol.TypeArguments.Length >= 1)
                {
                    return NormalizeType(namedTypeSymbol.TypeArguments[0]);
                }
            }

            if (typeSymbol.ContainingNamespace != null &&
                typeSymbol.ContainingNamespace.ToString() == "System.Collections.Generic" &&
                (typeSymbol.Name == "List" || typeSymbol.Name == "IReadOnlyList"))
            {
                if (typeSymbol is INamedTypeSymbol namedTypeSymbol &&
                    namedTypeSymbol.TypeArguments.Length >= 1)
                {
                    return NormalizeType(namedTypeSymbol.TypeArguments[0]);
                }
            }

            return typeSymbol;
        }

        private static void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (!(context.Node is PropertyDeclarationSyntax propertyDeclaration))
            {
                return;
            }

            var symbol = context.SemanticModel.GetDeclaredSymbol(propertyDeclaration);
            if (symbol == null)
            {
                return;
            }

            var typeSymbol = NormalizeType(symbol.Type);
            if (typeSymbol == null)
            {
                return;
            }

            if (typeSymbol != null &&
                typeSymbol.Name == "Key" &&
                typeSymbol.ContainingNamespace != null &&
                typeSymbol.ContainingNamespace.ToString() == "Google.Cloud.Datastore.V1" &&
                symbol.GetAttributes().Any(x =>
                    x != null &&
                    x.AttributeClass != null &&
                    x.AttributeClass.ContainingNamespace != null &&
                    x.AttributeClass.ContainingNamespace.ToString() == "Redpoint.CloudFramework.Models" &&
                    x.AttributeClass.Name == "TypeAttribute"))
            {
                var diagnostic = Diagnostic.Create(_datastoreKeyRule, symbol.Locations[0], symbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
