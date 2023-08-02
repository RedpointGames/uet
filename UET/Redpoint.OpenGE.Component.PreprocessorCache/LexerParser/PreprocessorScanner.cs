namespace Redpoint.OpenGE.Component.PreprocessorCache.LexerParser
{
    using Google.Protobuf;
    using Google.Protobuf.Collections;
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Collections.Generic;
    using System.IO.Hashing;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text.RegularExpressions;

    internal class PreprocessorScanner
    {
        private static string[] _directives = new[]
        {
            "#include",
            "#define",
            "#undef",
            // @note: Must be before #if.
            "#ifdef",
            "#ifndef",
            "#if",
            "#elif",
            "#else",
            "#endif",
        };

        private static Regex _functionDefine = new Regex("^([A-Za-z_][A-Za-z0-9_]*)\\(([a-zA-Z0-9,_\\s]*)\\)\\s");
        private static Regex _variableDefine = new Regex("^([A-Za-z_][A-Za-z0-9_]*)(\\s|$)");

        private static PreprocessorDirective MakeDirective(PreprocessorExpression? expression, PreprocessorDirective directive)
        {
            if (expression != null)
            {
                var referencedIdentifiers = new HashSet<string>();
                GetUniqueIdentifiers(expression, referencedIdentifiers);
                if (directive.DirectiveCase == PreprocessorDirective.DirectiveOneofCase.Define &&
                    directive.Define.IsFunction)
                {
                    foreach (var param in directive.Define.Parameters)
                    {
                        referencedIdentifiers.Remove(param);
                    }
                }
                directive.ReferencedIdentifiers.AddRange(referencedIdentifiers);
            }
            return directive;
        }

        internal class ScanResult
        {
            public List<PreprocessorDirective> Directives { get; } = new List<PreprocessorDirective>();
            public Dictionary<long, PreprocessorCondition> Conditions { get; } = new Dictionary<long, PreprocessorCondition>();
        }

        private static void ProcessDirective(
            string directive,
            string value,
            Stack<PreprocessorDirective> targetBlocks,
            ScanResult scanResult)
        {
            void AddDirective(PreprocessorDirective directive)
            {
                if (targetBlocks.Count == 0)
                {
                    scanResult.Directives.Add(directive);
                }
                else
                {
                    var target = targetBlocks.Peek();
                    if (target.DirectiveCase == PreprocessorDirective.DirectiveOneofCase.Block)
                    {
                        target.Block.Subdirectives.Add(directive);
                    }
                    else if (target.DirectiveCase == PreprocessorDirective.DirectiveOneofCase.If)
                    {
                        target.If.Subdirectives.Add(directive);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
            }

            switch (directive)
            {
                case "#include":
                    var include = value.Trim();
                    var includeQuote = include.StartsWith('"');
                    var includeAngle = include.StartsWith('<');
                    if (includeQuote)
                    {
                        AddDirective(MakeDirective(null, new PreprocessorDirective
                        {
                            Include = new PreprocessorDirectiveInclude
                            {
                                Normal = include.Trim('"'),
                            }
                        }));
                        return;
                    }
                    else if (includeAngle)
                    {
                        AddDirective(MakeDirective(null, new PreprocessorDirective
                        {
                            Include = new PreprocessorDirectiveInclude
                            {
                                System = include.TrimStart('<').TrimEnd('>'),
                            }
                        }));
                        return;
                    }
                    else
                    {
                        var expr = PreprocessorExpressionParser.ParseExpansion(PreprocessorExpressionLexer.Lex(include));
                        AddDirective(MakeDirective(expr, new PreprocessorDirective
                        {
                            Include = new PreprocessorDirectiveInclude
                            {
                                Expansion = expr,
                            }
                        }));
                        return;
                    }
                case "#define":
                    var defineExpr = value.TrimStart();
                    var functionMatch = _functionDefine.Match(defineExpr);
                    var variableMatch = _variableDefine.Match(defineExpr);
                    if (functionMatch.Success)
                    {
                        var exprText = value.Length > functionMatch.Length ? value.Substring(functionMatch.Length) : string.Empty;
                        var expr = string.IsNullOrWhiteSpace(exprText)
                            ? new PreprocessorExpression
                            {
                                Chain = new PreprocessorExpressionChain()
                            }
                            : PreprocessorExpressionParser.ParseExpansion(PreprocessorExpressionLexer.Lex(exprText));
                        var funcDefine = new PreprocessorDirectiveDefine
                        {
                            Identifier = functionMatch.Groups[1].Value,
                            IsFunction = true,
                            Expansion = expr,
                        };
                        funcDefine.Parameters.AddRange(
                            functionMatch.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                        AddDirective(MakeDirective(expr, new PreprocessorDirective
                        {
                            Define = funcDefine,
                        }));
                        return;
                    }
                    else if (variableMatch.Success)
                    {
                        var exprText = value.Length > variableMatch.Length ? value.Substring(variableMatch.Length) : string.Empty;
                        var expr = string.IsNullOrWhiteSpace(exprText)
                            ? new PreprocessorExpression
                            {
                                Chain = new PreprocessorExpressionChain()
                            }
                            : PreprocessorExpressionParser.ParseExpansion(PreprocessorExpressionLexer.Lex(exprText));
                        AddDirective(MakeDirective(expr, new PreprocessorDirective
                        {
                            Define = new PreprocessorDirectiveDefine
                            {
                                Identifier = variableMatch.Groups[1].Value,
                                IsFunction = false,
                                Expansion = expr,
                            },
                        }));
                        return;
                    }
                    else
                    {
                        // Lenience.
                    }
                    return;
                case "#undef":
                    AddDirective(MakeDirective(null, new PreprocessorDirective
                    {
                        Undefine = new PreprocessorDirectiveUndefine
                        {
                            Identifier = value.Trim(),
                        }
                    }));
                    return;
                case "#if":
                case "#ifdef":
                case "#ifndef":
                    var conditionHash = ComputeConditionHash(
                        scanResult,
                        directive switch
                        {
                            "#if" => PreprocessorExpressionParser.ParseCondition(PreprocessorExpressionLexer.Lex(value)),
                            "#ifdef" => new PreprocessorExpression
                            {
                                Defined = value.Trim(),
                            },
                            "#ifndef" => new PreprocessorExpression
                            {
                                Unary = new PreprocessorExpressionUnary
                                {
                                    Type = PreprocessorExpressionTokenType.LogicalNot,
                                    Expression = new PreprocessorExpression
                                    {
                                        Defined = value.Trim(),
                                    }
                                },
                            },
                            _ => throw new NotSupportedException()
                        });
                    var ifDirective = MakeDirective(null, new PreprocessorDirective
                    {
                        If = new PreprocessorDirectiveIf
                        {
                            ConditionHash = conditionHash,
                        }
                    });
                    AddDirective(ifDirective);
                    targetBlocks.Push(ifDirective);
                    return;
                case "#elif":
                    if (targetBlocks.Count == 0 || targetBlocks.Peek().DirectiveCase != PreprocessorDirective.DirectiveOneofCase.If)
                    {
                        throw new Exception("Got #elif without an #if block.");
                    }
                    var elifConditionHash = ComputeConditionHash(
                        scanResult,
                        PreprocessorExpressionParser.ParseCondition(PreprocessorExpressionLexer.Lex(value)));
                    var elifDirective = MakeDirective(null, new PreprocessorDirective
                    {
                        If = new PreprocessorDirectiveIf
                        {
                            ConditionHash = elifConditionHash,
                        }
                    });
                    var currentIf = targetBlocks.Pop();
                    currentIf.If.HasElseBranch = true;
                    currentIf.If.ElseBranch = elifDirective;
                    // @note: We don't call AddDirective because this branch
                    // will be reached via ElseBranch.
                    targetBlocks.Push(elifDirective);
                    return;
                case "#else":
                    if (targetBlocks.Count == 0 || targetBlocks.Peek().DirectiveCase != PreprocessorDirective.DirectiveOneofCase.If)
                    {
                        throw new Exception("Got #else without an #if block.");
                    }
                    var elseDirective = MakeDirective(null, new PreprocessorDirective
                    {
                        Block = new PreprocessorDirectiveBlock()
                    });
                    var currentIfFromElse = targetBlocks.Pop();
                    currentIfFromElse.If.HasElseBranch = true;
                    currentIfFromElse.If.ElseBranch = elseDirective;
                    // @note: We don't call AddDirective because this branch
                    // will be reached via ElseBranch.
                    targetBlocks.Push(elseDirective);
                    return;
                case "#endif":
                    if (targetBlocks.Count == 0 ||
                        targetBlocks.Peek().DirectiveCase != PreprocessorDirective.DirectiveOneofCase.If &&
                        targetBlocks.Peek().DirectiveCase != PreprocessorDirective.DirectiveOneofCase.Block)
                    {
                        throw new Exception("Got #endif without an #if/#elif/#else block.");
                    }
                    targetBlocks.Pop();
                    return;
            }
        }

        private static void GetUniqueIdentifiers(PreprocessorExpression expression, HashSet<string> identifiers)
        {
            switch (expression.ExprCase)
            {
                case PreprocessorExpression.ExprOneofCase.None:
                    return;
                case PreprocessorExpression.ExprOneofCase.Invoke:
                    identifiers.Add(expression.Invoke.Identifier);
                    foreach (var arg in expression.Invoke.Arguments)
                    {
                        GetUniqueIdentifiers(arg, identifiers);
                    }
                    return;
                case PreprocessorExpression.ExprOneofCase.Token:
                    switch (expression.Token.DataCase)
                    {
                        case PreprocessorExpressionToken.DataOneofCase.Identifier:
                            identifiers.Add(expression.Token.Identifier);
                            return;
                        case PreprocessorExpressionToken.DataOneofCase.None:
                        case PreprocessorExpressionToken.DataOneofCase.Type:
                        case PreprocessorExpressionToken.DataOneofCase.Number:
                        case PreprocessorExpressionToken.DataOneofCase.Text:
                        case PreprocessorExpressionToken.DataOneofCase.Whitespace:
                            return;
                        default:
                            throw new NotSupportedException($"GetUniqueIdentifiers DataCase = {expression.Token.DataCase}");
                    }
                case PreprocessorExpression.ExprOneofCase.Unary:
                    GetUniqueIdentifiers(expression.Unary.Expression, identifiers);
                    return;
                case PreprocessorExpression.ExprOneofCase.Binary:
                    GetUniqueIdentifiers(expression.Binary.Left, identifiers);
                    GetUniqueIdentifiers(expression.Binary.Right, identifiers);
                    return;
                case PreprocessorExpression.ExprOneofCase.Chain:
                    foreach (var expr in expression.Chain.Expressions)
                    {
                        GetUniqueIdentifiers(expr, identifiers);
                    }
                    return;
                case PreprocessorExpression.ExprOneofCase.Whitespace:
                    return;
                case PreprocessorExpression.ExprOneofCase.Defined:
                    identifiers.Add(expression.Defined);
                    return;
                default:
                    throw new NotSupportedException($"GetUniqueIdentifiers ExprCase = {expression.ExprCase}");
            }
        }

        private static long ComputeConditionHash(ScanResult scanResult, PreprocessorExpression preprocessorExpression)
        {
            var bytes = preprocessorExpression.ToByteArray();
            var hash = BitConverter.ToInt64(XxHash64.Hash(bytes));
            if (!scanResult.Conditions.ContainsKey(hash))
            {
                var identifiers = new HashSet<string>();
                GetUniqueIdentifiers(preprocessorExpression, identifiers);
                var condition = new PreprocessorCondition
                {
                    ConditionHash = hash,
                    Condition = preprocessorExpression,
                };
                condition.DependentOnIdentifiers.AddRange(identifiers);
                scanResult.Conditions.Add(hash, condition);
            }
            return hash;
        }

        internal static ScanResult Scan(IEnumerable<string> lines)
        {
            var result = new ScanResult();
            var enumerator = lines.GetEnumerator();
            var lineNumber = 0;
            var inBlockComment = false;
            var continuingPreviousDirectiveLine = false;
            var previousDirectiveLine = string.Empty;
            var targetBlocks = new Stack<PreprocessorDirective>();
            while (enumerator.MoveNext())
            {
                lineNumber++;
                var line = enumerator.Current.TrimStart();

                // If we're in a block comment, we need to consume lines until we're not.
                if (inBlockComment)
                {
                    var commentBlockEndIndex = line.IndexOf("*/");
                    if (commentBlockEndIndex == -1)
                    {
                        // This line isn't terminating the block comment.
                        continue;
                    }
                    else
                    {
                        if (commentBlockEndIndex == line.Length - 2)
                        {
                            // The end of the line is the end of the block comment.
                            inBlockComment = false;
                            continue;
                        }
                        else
                        {
                            // Allow the rest of the line to be parsed.
                            line = line.Substring(commentBlockEndIndex + 2);
                            inBlockComment = false;
                        }
                    }
                }

                // Is this a directive at all?
                if (!continuingPreviousDirectiveLine && !line.StartsWith('#'))
                {
                    continue;
                }

                string? directive = null;
                if (!continuingPreviousDirectiveLine)
                {
                    // Strip all whitespace between the '#' and first non-whitespace
                    // character to allow for directives like "#  if".
                    line = '#' + line.Substring(1).TrimStart();

                    // Is it a directive we care about?
                    directive = _directives.FirstOrDefault(x => line.StartsWith(x));
                    if (directive == null)
                    {
                        continue;
                    }
                }

                // If the line has a // in it, strip off the comment.
                var commentIndex = line.IndexOf("//");
                if (commentIndex != -1)
                {
                    line = line.Substring(0, commentIndex);
                }

                // If the line has a /* in it, strip it out until we reach */.
                var commentBlockStartIndex = line.IndexOf("/*");
                while (commentBlockStartIndex != -1)
                {
                    var lineBefore = line.Substring(0, commentBlockStartIndex);
                    var lineAfter = line.Substring(commentBlockStartIndex + 2);
                    var commentBlockEndIndex = lineAfter.IndexOf("*/");
                    if (commentBlockEndIndex != -1)
                    {
                        if (commentBlockEndIndex == lineAfter.Length - 2)
                        {
                            // Comment block ends at the end of the line.
                            line = lineBefore;
                            commentBlockStartIndex = -1;
                        }
                        else
                        {
                            // Comment block ends and then we have more content.
                            line = lineBefore + lineAfter.Substring(commentBlockEndIndex + 2);
                            commentBlockStartIndex = line.IndexOf("/*");
                        }
                    }
                    else
                    {
                        line = lineBefore; // No terminator on this line.
                        inBlockComment = true;
                        commentBlockStartIndex = -1;
                    }
                }

                // If the line ends in \, we need to grab more lines to generate the full directive value.
                if (line.TrimEnd().EndsWith('\\'))
                {
                    previousDirectiveLine = previousDirectiveLine == string.Empty
                        ? line.TrimEnd().TrimEnd('\\')
                        : previousDirectiveLine + '\n' + line.TrimEnd().TrimEnd('\\');
                    continuingPreviousDirectiveLine = true;
                    continue;
                }
                else if (continuingPreviousDirectiveLine)
                {
                    line = previousDirectiveLine + '\n' + line;
                    directive = _directives.FirstOrDefault(x => line.StartsWith(x))!;
                    if (directive == null)
                    {
                        throw new InvalidOperationException("Evaluating the directive should already have passed.");
                    }
                    previousDirectiveLine = string.Empty;
                    continuingPreviousDirectiveLine = false;
                }

                // Split the directive to get the value on the right.
                var components = line.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (directive != components[0])
                {
                    continue;
                }

                // Determine the value associated with the directive.
                string value = string.Empty;
                if (components.Length == 2)
                {
                    value = components[1];
                }

                // Handle the directive.
                try
                {
                    ProcessDirective(
                        directive,
                        value,
                        targetBlocks,
                        result);
                }
                catch (Exception ex)
                {
                    throw new PreprocessorScannerException(lineNumber, line, ex);
                }
            }

            // Go and trim the directive tree, removing any if/block directives that
            // have no content.
            TrimDirectives(result.Directives);

            // Rescan what condition hashes are actually used, and remove the ones
            // that aren't.
            var usedConditionHashes = new HashSet<long>();
            ScanForUsedConditionHashes(usedConditionHashes, result.Directives);
            var unusedConditionHashes = result.Conditions.Keys.ToHashSet();
            unusedConditionHashes.ExceptWith(usedConditionHashes);
            foreach (var hash in unusedConditionHashes)
            {
                result.Conditions.Remove(hash);
            }

            return result;
        }

        private static void ScanForUsedConditionHashes(
            HashSet<long> usedConditionHashes,
            IEnumerable<PreprocessorDirective> directives)
        {
            foreach (var directive in directives)
            {
                switch (directive.DirectiveCase)
                {
                    case PreprocessorDirective.DirectiveOneofCase.If:
                        usedConditionHashes.Add(directive.If.ConditionHash);
                        if (directive.If.HasElseBranch)
                        {
                            ScanForUsedConditionHashes(usedConditionHashes, new[] { directive.If.ElseBranch });
                        }
                        ScanForUsedConditionHashes(usedConditionHashes, directive.If.Subdirectives);
                        break;
                    case PreprocessorDirective.DirectiveOneofCase.Block:
                        ScanForUsedConditionHashes(usedConditionHashes, directive.Block.Subdirectives);
                        break;
                }
            }
        }

        private static void TrimDirectives(IList<PreprocessorDirective> directives)
        {
            for (int i = 0; i < directives.Count; i++)
            {
                var directive = directives[i];
                switch (directive.DirectiveCase)
                {
                    case PreprocessorDirective.DirectiveOneofCase.If:
                        if (directive.If.HasElseBranch)
                        {
                            TrimElseBranch(directive.If);
                        }
                        if (directive.If.Subdirectives.Count > 0)
                        {
                            TrimDirectives(directive.If.Subdirectives);
                        }
                        if (directive.If.Subdirectives.Count == 0 &&
                            !directive.If.HasElseBranch)
                        {
                            directives.RemoveAt(i);
                            i--;
                        }
                        break;
                    case PreprocessorDirective.DirectiveOneofCase.Block:
                        if (directive.Block.Subdirectives.Count > 0)
                        {
                            TrimDirectives(directive.Block.Subdirectives);
                        }
                        if (directive.Block.Subdirectives.Count == 0)
                        {
                            directives.RemoveAt(i);
                            i--;
                        }
                        break;
                }
            }
        }

        private static void TrimElseBranch(PreprocessorDirectiveIf @if)
        {
            switch (@if.ElseBranch.DirectiveCase)
            {
                case PreprocessorDirective.DirectiveOneofCase.If:
                    if (@if.ElseBranch.If.HasElseBranch)
                    {
                        TrimElseBranch(@if.ElseBranch.If);
                    }
                    if (@if.ElseBranch.If.Subdirectives.Count > 0)
                    {
                        TrimDirectives(@if.ElseBranch.If.Subdirectives);
                    }
                    if (@if.ElseBranch.If.Subdirectives.Count == 0 &&
                        !@if.ElseBranch.If.HasElseBranch)
                    {
                        @if.HasElseBranch = false;
                    }
                    break;
                case PreprocessorDirective.DirectiveOneofCase.Block:
                    if (@if.ElseBranch.Block.Subdirectives.Count > 0)
                    {
                        TrimDirectives(@if.ElseBranch.Block.Subdirectives);
                    }
                    if (@if.ElseBranch.Block.Subdirectives.Count == 0)
                    {
                        @if.HasElseBranch = false;
                    }
                    break;
            }
        }
    }
}
