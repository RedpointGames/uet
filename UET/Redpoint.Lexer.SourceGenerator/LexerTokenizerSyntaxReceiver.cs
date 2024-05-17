namespace Redpoint.Lexer.SourceGenerator
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using System.Collections.Immutable;
    using System.Net;
    using System.Text;

    internal sealed class LexerTokenizerSyntaxReceiver : ISyntaxContextReceiver, ILexerSyntaxReceiver
    {
        private const string _redpointLexerNamespace = "Redpoint.Lexer";
        private const string _lexerTokenizerAttributeFullName = "Redpoint.Lexer.LexerTokenizerAttribute";
        private const string _permitNewlineContinuationsAttributeFullName = "Redpoint.Lexer.PermitNewlineContinuationsAttribute";

        private readonly List<LexerTokenizerGenerationSpec> _generationSpecs = new List<LexerTokenizerGenerationSpec>();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (!(context.Node is MethodDeclarationSyntax method))
            {
                return;
            }
            var declaredSymbol = context.SemanticModel.GetDeclaredSymbol(method);
            if (declaredSymbol == null)
            {
                return;
            }
            var permitNewlineContinuations = false;
            string? tokenizerPattern = null;
            foreach (var attributeData in declaredSymbol.GetAttributes())
            {
                var attributeType = attributeData.AttributeClass;
                if (attributeType is null)
                {
                    continue;
                }
                if (attributeType.ContainingAssembly.Name == _redpointLexerNamespace)
                {
                    switch (attributeType.ToDisplayString())
                    {
                        case _lexerTokenizerAttributeFullName:
                            {
                                ImmutableArray<TypedConstant> ctorArgs = attributeData.ConstructorArguments;
                                tokenizerPattern = (string)ctorArgs[0].Value!;
                                break;
                            }
                        case _permitNewlineContinuationsAttributeFullName:
                            permitNewlineContinuations = true;
                            break;
                    }
                }
            }
            if (tokenizerPattern == null)
            {
                // @note: For performance, if we have other lexer-aware attributes
                // in the future we probably want to unify all attribute scanning
                // under the syntax receiver instead of doing the ILexerSyntaxReceiver
                // thing.
                return;
            }
            _generationSpecs.Add(new LexerTokenizerGenerationSpec
            {
                AccessibilityModifiers = "public",
                TokenizerPattern = tokenizerPattern,
                PermitNewlineContinuations = permitNewlineContinuations,
                MethodName = declaredSymbol.Name,
                ContainingNamespaceName = declaredSymbol.ContainingNamespace.ToDisplayString(),
                ContainingClasses = GetClassNameTree(declaredSymbol.ContainingType),
                DeclaringFilename = Path.GetFileNameWithoutExtension(context.Node.SyntaxTree.FilePath),
            });
        }

        private static LexerTokenizerClassEntry[] GetClassNameTree(INamedTypeSymbol directParent)
        {
            if (directParent.ContainingType == null)
            {
                return new[]
                {
                    new LexerTokenizerClassEntry
                    {
                        Name = directParent.Name,
                        IsStatic = directParent.IsStatic,
                    },
                };
            }
            var entries = new List<LexerTokenizerClassEntry>();
            var current = directParent;
            while (current != null)
            {
                entries.Add(new LexerTokenizerClassEntry
                {
                    Name = current.Name,
                    IsStatic = current.IsStatic,
                });
                current = current.ContainingType;
            }
            entries.Reverse();
            return entries.ToArray();
        }

        public void Execute(GeneratorExecutionContext context)
        {
            foreach (var generationSpecsByFilename in _generationSpecs
                .GroupBy(x => x.DeclaringFilename))
            {
                var sourceBuilder = new StringBuilder();
                sourceBuilder.AppendLine(
                    """
                    using System;
                    using System.Text;
                    using System.Runtime.CompilerServices;
                    using Redpoint.Lexer;

                    #nullable enable

                    """);
                foreach (var generationSpecByNamespace in generationSpecsByFilename
                    .GroupBy(x => x.ContainingNamespaceName))
                {
                    sourceBuilder.AppendLine(
                        $$"""
                        namespace {{generationSpecByNamespace.Key}}
                        {
                        """);
                    foreach (var generationSpecByFullClassName in generationSpecsByFilename
                        .GroupBy(x => string.Join(".", x.ContainingClasses)))
                    {
                        var indent = 4;
                        foreach (var className in generationSpecByFullClassName.First().ContainingClasses)
                        {
                            sourceBuilder.AppendLine(
                                $$"""
                                {{(className.IsStatic ? "static " : "")}}partial class {{className.Name}}
                                {
                                """.WithIndent(indent));
                            indent += 4;
                        }
                        var first = true;
                        foreach (var generationSpec in generationSpecByFullClassName)
                        {
                            if (!first)
                            {
                                sourceBuilder.AppendLine();
                            }
                            else
                            {
                                first = false;
                            }
                            var returnType = generationSpec.PermitNewlineContinuations
                                ? "LexerFragment"
                                : "ReadOnlySpan<char>";
                            sourceBuilder.AppendLine(
                                $$"""
                                /// <summary>
                                /// Attempt to consume a token that matches the expression '{{WebUtility.HtmlEncode(generationSpec.TokenizerPattern)}}'
                                /// from the start of the specified span. If the token is found, the span is moved past the token and
                                /// the cursor is updated based on the characters and newlines processed. If the token is not found,
                                /// an empty span is returned.
                                /// </summary>
                                /// <param name="span">The reference to the span to attempt to consume the token from.</param>
                                /// <param name="cursor">The cursor that stores information about the number of characters and newlines processed. No special initialization is needed for a cursor; you simply declare a <see cref="LexerCursor" /> on the stack and pass it in by reference.</param>
                                /// <returns>A span that contains the token consumed from the span, or an empty span.</returns>
                                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                                {{generationSpec.AccessibilityModifiers}} static partial {{returnType}} {{generationSpec.MethodName}}(ref ReadOnlySpan<char> span, ref LexerCursor cursor)
                                {
                                """.WithIndent(indent));
                            GenerateLexingCode(
                                sourceBuilder,
                                generationSpec.TokenizerPattern.AsSpan(),
                                generationSpec.PermitNewlineContinuations,
                                indent + 4);
                            sourceBuilder.AppendLine("}".WithIndent(indent));
                        }
                        foreach (var className in generationSpecByFullClassName.Key.Split('.'))
                        {
                            indent -= 4;
                            sourceBuilder.AppendLine("}".WithIndent(indent));
                        }
                    }
                    sourceBuilder.AppendLine("}");
                }
                context.AddSource($"{generationSpecsByFilename.Key}.g.cs", sourceBuilder.ToString());
            }
        }

        private struct LexingMatch
        {
            public string? Literal;
            public List<(char start, char end)>? Ranges;
            public int Min;
            public int? Max;
        }

        private static void GenerateLexingCode(
            StringBuilder sourceBuilder,
            ReadOnlySpan<char> pattern,
            bool permitNewlineContinuations,
            int indent)
        {
            string currentLiteral = string.Empty;
            List<(char start, char end)> currentRanges = new List<(char start, char end)>();
            var matches = new List<LexingMatch>();
        ConsumePotentialLiteral:
            if (pattern.IsEmpty)
            {
                if (!string.IsNullOrEmpty(currentLiteral))
                {
                    matches.Add(new LexingMatch
                    {
                        Literal = currentLiteral,
                        Ranges = null,
                        Min = 1,
                        Max = 1,
                    });
                }
                goto GenerateCode;
            }
            switch (pattern[0])
            {
                case '[':
                    // Start of a range block.
                    if (!string.IsNullOrEmpty(currentLiteral))
                    {
                        matches.Add(new LexingMatch
                        {
                            Literal = currentLiteral,
                            Ranges = null,
                            Min = 1,
                            Max = 1,
                        });
                        currentLiteral = string.Empty;
                    }
                    pattern = pattern.Slice(1);
                    goto ConsumeRanges;
                case '\\':
                    // Start of an escape character.
                    currentLiteral += pattern[1];
                    pattern = pattern.Slice(2);
                    goto ConsumePotentialLiteral;
                default:
                    // Some other literal character.
                    currentLiteral += pattern[0];
                    pattern = pattern.Slice(1);
                    goto ConsumePotentialLiteral;
            }
        ConsumeRanges:
            char start, end;
            switch (pattern[0])
            {
                case '\\':
                    start = pattern[1];
                    pattern = pattern.Slice(2);
                    break;
                case ']':
                    // No more ranges.
                    var rangeMatch = new LexingMatch
                    {
                        Literal = null,
                        Ranges = currentRanges,
                        Min = 1,
                        Max = 1,
                    };
                    currentRanges = new List<(char start, char end)>();
                    pattern = pattern.Slice(1);
                    var rangeModifierBreak = false;
                    while (!pattern.IsEmpty && !rangeModifierBreak)
                    {
                        switch (pattern[0])
                        {
                            case '?':
                                rangeMatch.Min = 0;
                                pattern = pattern.Slice(1);
                                break;
                            case '*':
                                rangeMatch.Min = 0;
                                rangeMatch.Max = null;
                                pattern = pattern.Slice(1);
                                break;
                            case '+':
                                rangeMatch.Min = 1;
                                rangeMatch.Max = null;
                                pattern = pattern.Slice(1);
                                break;
                            default:
                                rangeModifierBreak = true;
                                break;
                        }
                    }
                    matches.Add(rangeMatch);
                    goto ConsumePotentialLiteral;
                default:
                    start = pattern[0];
                    pattern = pattern.Slice(1);
                    break;
            }
            if (pattern[0] == '-')
            {
                pattern = pattern.Slice(1);
                switch (pattern[0])
                {
                    case '\\':
                        end = pattern[1];
                        pattern = pattern.Slice(2);
                        break;
                    case ']':
                        end = start;
                        // No slice, we want our ConsumeRanges to
                        // pick up on this.
                        break;
                    default:
                        end = pattern[0];
                        pattern = pattern.Slice(1);
                        break;
                }
            }
            else
            {
                end = start;
            }
            currentRanges.Add((start, end));
            goto ConsumeRanges;
        GenerateCode:
            sourceBuilder.AppendLine(
                """
                if (span.IsEmpty)
                {
                    return default;
                }
                """.WithIndent(indent));
            if (permitNewlineContinuations)
            {
                sourceBuilder.AppendLine("var containsNewlineContinuations = false;".WithIndent(indent));
            }
            sourceBuilder.AppendLine(
                """
                var currentSpan = span;
                LexerCursor localCursor = default;
                """.WithIndent(indent));
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (i != 0)
                {
                    sourceBuilder.AppendLine(
                        $"Segment{i}:".WithIndent(indent));
                }
                sourceBuilder.AppendLine(
                    "{".WithIndent(indent));
                indent += 4;
                string onOneNoFind;
                string onOneFind;
                string onMultiFind;
                if (match.Max != 1)
                {
                    if (match.Min != 0 ||
                        match.Max.HasValue)
                    {
                        sourceBuilder.AppendLine(
                            $"var findCount = 0;".WithIndent(indent));
                    }
                    sourceBuilder.AppendLine(
                        $$"""
                        Segment{{i}}Retry:
                        {
                        """.WithIndent(indent));
                    indent += 4;
                    if (match.Min != 0)
                    {
                        onOneNoFind =
                            $$"""
                            if (findCount < {{match.Min}})
                            {
                                return default;
                            }
                            else
                            {
                                goto Segment{{i + 1}};
                            }
                            """;
                    }
                    else
                    {
                        onOneNoFind =
                            $$"""
                            goto Segment{{i + 1}};
                            """;
                    }
                    if (match.Max.HasValue)
                    {
                        onOneFind =
                            $$"""
                            findCount++;
                            if (findCount == {{match.Max}})
                            {
                                goto Segment{{i + 1}};
                            }
                            else
                            {
                                goto Segment{{i}}Retry;
                            }
                            """;
                        onMultiFind =
                            $$"""
                            if (findCount == {{match.Max}})
                            {
                                goto Segment{{i + 1}};
                            }
                            else
                            {
                                goto Segment{{i}}Retry;
                            }
                            """;
                    }
                    else if (match.Min != 1)
                    {
                        onOneFind =
                            $$"""
                            findCount++;
                            goto Segment{{i}}Retry;
                            """;
                        onMultiFind =
                            $$"""
                            goto Segment{{i}}Retry;
                            """;
                    }
                    else
                    {
                        onOneFind =
                            $$"""
                            goto Segment{{i}}Retry;
                            """;
                        onMultiFind =
                            $$"""
                            goto Segment{{i}}Retry;
                            """;
                    }
                }
                else
                {
                    if (match.Min == 0)
                    {
                        onOneNoFind = $"goto Segment{i + 1};";
                        onOneFind = $"goto Segment{i + 1};";
                        onMultiFind = $"goto Segment{i + 1};";
                    }
                    else
                    {
                        onOneNoFind = $"return default;";
                        onOneFind = $"goto Segment{i + 1};";
                        onMultiFind = $"goto Segment{i + 1};";
                    }
                }
                if (match.Literal != null)
                {
                    if (permitNewlineContinuations)
                    {
                        sourceBuilder.AppendLine(
                            $$"""
                            if (currentSpan.TryConsumeSequence(
                                "{{match.Literal.Replace("\\", "\\\\")}}",
                                ref localCursor,
                                ref containsNewlineContinuations))
                            {
                            {{onOneFind.WithIndent(4)}}
                            }
                            else
                            {
                            {{onOneNoFind.WithIndent(4)}}
                            }
                            """.WithIndent(indent));
                    }
                    else
                    {
                        sourceBuilder.AppendLine(
                            $$"""
                            if (currentSpan.StartsWith(
                                "{{match.Literal.Replace("\\", "\\\\")}}",
                                StringComparison.Ordinal))
                            {
                                currentSpan.Consume({{match.Literal.Length}}, ref localCursor);
                            {{onOneFind.WithIndent(4)}}
                            }
                            else
                            {
                            {{onOneNoFind.WithIndent(4)}}
                            }
                            """.WithIndent(indent));
                    }
                }
                else if (match.Ranges != null)
                {
                    sourceBuilder.AppendLine(
                        $$"""
                        if (currentSpan.IsEmpty)
                        {
                        {{onOneNoFind.WithIndent(4)}}
                        }
                        ref readonly var character = ref currentSpan[0];
                        """.WithIndent(indent));
                    if (permitNewlineContinuations)
                    {
                        sourceBuilder.AppendLine(
                            $$"""
                            if (character == '\\')
                            {
                                if (currentSpan.ConsumeNewlineContinuations(ref localCursor) > 0)
                                {
                                    containsNewlineContinuations = true;
                                }
                                if (currentSpan.IsEmpty)
                                {
                                {{onOneNoFind.WithIndent(8)}}
                                }
                                character = ref currentSpan[0];
                            }
                            """.WithIndent(indent));
                    }
                    for (var r = 0; r < match.Ranges.Count; r++)
                    {
                        var range = match.Ranges[r];
                        if (range.start == range.end)
                        {
                            sourceBuilder.AppendLine(
                                $$"""
                                if (character == (char){{(int)range.start}})
                                {
                                """.WithIndent(indent));
                        }
                        else
                        {
                            sourceBuilder.AppendLine(
                                $$"""
                                if (character >= (char){{(int)range.start}} &&
                                    character <= (char){{(int)range.end}})
                                {
                                """.WithIndent(indent));
                        }
                        indent += 4;
                        sourceBuilder.AppendLine(
                            $$"""
                            currentSpan.Consume(1, ref localCursor);
                            """.WithIndent(indent));
                        if (match.Max == 1)
                        {
                            sourceBuilder.AppendLine(onOneFind.WithIndent(indent));
                        }
                        else
                        {
                            if (match.Max.HasValue ||
                                match.Min != 0)
                            {
                                sourceBuilder.AppendLine(
                                    $$"""
                                    findCount++;
                                    """.WithIndent(indent));
                            }
                            if (range.start == range.end)
                            {
                                sourceBuilder.AppendLine(
                                    $$"""
                                    var sequenceLength = currentSpan.IndexOfAnyExcept((char){{(int)range.start}});
                                    """.WithIndent(indent));
                            }
                            else
                            {
                                sourceBuilder.AppendLine(
                                    $$"""
                                    var sequenceLength = currentSpan.IndexOfAnyExceptInRange((char){{(int)range.start}}, (char){{(int)range.end}});
                                    """.WithIndent(indent));
                            }
                            if (match.Max != null)
                            {
                                sourceBuilder.AppendLine(
                                    $$"""
                                    if (sequenceLength == -1)
                                    {
                                        sequenceLength = Math.Min(
                                            currentSpan.Length,
                                            {{match.Max}} - findCount);
                                    }
                                    else
                                    {
                                        sequenceLength = Math.Min(
                                            sequenceLength,
                                            {{match.Max}} - findCount);
                                    }
                                    """.WithIndent(indent));
                            }
                            else
                            {
                                sourceBuilder.AppendLine(
                                    $$"""
                                    if (sequenceLength == -1)
                                    {
                                        sequenceLength = currentSpan.Length;
                                    }
                                    """.WithIndent(indent));
                            }
                            if (match.Max.HasValue ||
                                match.Min != 0)
                            {
                                sourceBuilder.AppendLine(
                                    $$"""
                                    findCount += sequenceLength;    
                                    """.WithIndent(indent));
                            }
                            sourceBuilder.AppendLine(
                                $$"""
                                if (sequenceLength != 0)
                                {
                                    currentSpan.Consume(sequenceLength, ref localCursor);
                                }
                                {{onMultiFind}}
                                """.WithIndent(indent));
                        }
                        indent -= 4;
                        sourceBuilder.AppendLine(
                            "}".WithIndent(indent));
                    }
                    sourceBuilder.AppendLine(onOneNoFind.WithIndent(indent));
                }
                else
                {
                    sourceBuilder.AppendLine(onOneFind.WithIndent(indent));
                }
                if (match.Max != 1)
                {
                    indent -= 4;
                    sourceBuilder.AppendLine(
                        $$"""
                        }
                        """.WithIndent(indent));
                }
                indent -= 4;
                sourceBuilder.AppendLine("}".WithIndent(indent));
            }
            sourceBuilder.AppendLine(
                $$"""
                Segment{{matches.Count}}:
                {
                    var resultSpan = span.Slice(0, localCursor.CharactersConsumed);
                    span = span.Slice(localCursor.CharactersConsumed);
                    cursor.Add(in localCursor);
                """.WithIndent(indent));
            indent += 4;
            if (permitNewlineContinuations)
            {
                sourceBuilder.AppendLine(
                    """
                    return new LexerFragment
                    {
                        Span = resultSpan,
                        ContainsNewlineContinuations = containsNewlineContinuations,
                    };
                    """.WithIndent(indent));
            }
            else
            {
                sourceBuilder.AppendLine("return resultSpan;".WithIndent(indent));
            }
            indent -= 4;
            sourceBuilder.AppendLine("}".WithIndent(indent));
        }
    }
}
