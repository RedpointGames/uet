namespace Redpoint.CloudFramework.Analyzer
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CloudFrameworkModelAnalyzer : DiagnosticAnalyzer
    {
        public const string InheritDiagnosticId = "CloudFrameworkModelAnalyzerInherit";
        public const string SealedDiagnosticId = "CloudFrameworkModelAnalyzerSealed";

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

#pragma warning restore RS2008 // Enable analyzer release tracking

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(_inheritRule, _sealedRule);
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
        }

        private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (!(context.Node is ClassDeclarationSyntax classDeclaration))
            {
                return;
            }

            var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

            var baseType = symbol.BaseType;
            if (baseType.IsGenericType &&
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
    }
}
