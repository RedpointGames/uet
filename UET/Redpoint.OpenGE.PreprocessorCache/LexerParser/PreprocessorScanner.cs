namespace Redpoint.OpenGE.PreprocessorCache.LexerParser
{
    using Google.Protobuf;
    using PreprocessorCacheApi;
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

        private static PreprocessorDirective MakeDirective(Stack<long> conditionHashes, HashSet<long> conditionHashesUsed, PreprocessorDirective directive)
        {
            directive.ConditionHashes.AddRange(conditionHashes);
            foreach (var hash in conditionHashes)
            {
                conditionHashesUsed.Add(hash);
            }
            return directive;
        }

        internal class ScanResult
        {
            public List<PreprocessorDirective> Directives { get; } = new List<PreprocessorDirective>();
            public Dictionary<long, PreprocessorCondition> Conditions { get; } = new Dictionary<long, PreprocessorCondition>();
        }

        private static PreprocessorDirective? ProcessDirective(
            string directive,
            string value,
            ScanResult scanResult,
            Stack<long> conditionHashes,
            Stack<int> conditionHashesToPopOnEndIf,
            HashSet<long> conditionHashesUsed)
        {
            switch (directive)
            {
                case "#include":
                    var include = value.Trim();
                    var includeQuote = include.StartsWith('"');
                    var includeAngle = include.StartsWith('<');
                    if (includeQuote)
                    {
                        return MakeDirective(conditionHashes, conditionHashesUsed, new PreprocessorDirective
                        {
                            Include = new PreprocessorDirectiveInclude
                            {
                                Normal = include.Trim('"'),
                            }
                        });
                    }
                    else if (includeAngle)
                    {
                        return MakeDirective(conditionHashes, conditionHashesUsed, new PreprocessorDirective
                        {
                            Include = new PreprocessorDirectiveInclude
                            {
                                System = include.TrimStart('<').TrimEnd('>'),
                            }
                        });
                    }
                    else
                    {
                        return MakeDirective(conditionHashes, conditionHashesUsed, new PreprocessorDirective
                        {
                            Include = new PreprocessorDirectiveInclude
                            {
                                Expansion = PreprocessorExpressionParser.ParseExpansion(PreprocessorExpressionLexer.Lex(include)),
                            }
                        });
                    }
                case "#define":
                    var defineExpr = value.TrimStart();
                    var functionMatch = _functionDefine.Match(defineExpr);
                    var variableMatch = _variableDefine.Match(defineExpr);
                    if (functionMatch.Success)
                    {
                        var exprText = value.Length > functionMatch.Length ? value.Substring(functionMatch.Length) : string.Empty;
                        var funcDefine = new PreprocessorDirectiveDefine
                        {
                            Identifier = functionMatch.Groups[1].Value,
                            IsFunction = true,
                            Expansion =
                                string.IsNullOrWhiteSpace(exprText)
                                ? new PreprocessorExpression
                                {
                                    Chain = new PreprocessorExpressionChain()
                                }
                                : PreprocessorExpressionParser.ParseExpansion(PreprocessorExpressionLexer.Lex(exprText)),
                        };
                        funcDefine.Parameters.AddRange(
                            functionMatch.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                        return MakeDirective(conditionHashes, conditionHashesUsed, new PreprocessorDirective
                        {
                            Define = funcDefine,
                        });
                    }
                    else if (variableMatch.Success)
                    {
                        var exprText = value.Length > variableMatch.Length ? value.Substring(variableMatch.Length) : string.Empty;
                        return MakeDirective(conditionHashes, conditionHashesUsed, new PreprocessorDirective
                        {
                            Define = new PreprocessorDirectiveDefine
                            {
                                Identifier = variableMatch.Groups[1].Value,
                                IsFunction = false,
                                Expansion =
                                    string.IsNullOrWhiteSpace(exprText)
                                    ? new PreprocessorExpression
                                    {
                                        Chain = new PreprocessorExpressionChain()
                                    }
                                    : PreprocessorExpressionParser.ParseExpansion(PreprocessorExpressionLexer.Lex(exprText)),
                            },
                        });
                    }
                    else
                    {
                        // Lenience.
                    }
                    return null;
                case "#undef":
                    return MakeDirective(conditionHashes, conditionHashesUsed, new PreprocessorDirective
                    {
                        Undefine = new PreprocessorDirectiveUndefine
                        {
                            Identifier = value.Trim(),
                        }
                    });
                case "#if":
                    conditionHashes.Push(ComputeConditionHash(
                        scanResult,
                        PreprocessorExpressionParser.ParseCondition(PreprocessorExpressionLexer.Lex(value))));
                    conditionHashesToPopOnEndIf.Push(1);
                    return null;
                case "#ifdef":
                    conditionHashes.Push(ComputeConditionHash(
                        scanResult,
                        new PreprocessorExpression
                        {
                            Defined = value.Trim(),
                        }));
                    conditionHashesToPopOnEndIf.Push(1);
                    return null;
                case "#ifndef":
                    conditionHashes.Push(ComputeConditionHash(
                        scanResult,
                        new PreprocessorExpression
                        {
                            Unary = new PreprocessorExpressionUnary
                            {
                                Type = PreprocessorExpressionTokenType.LogicalNot,
                                Expression = new PreprocessorExpression
                                {
                                    Defined = value.Trim(),
                                }
                            },
                        }));
                    conditionHashesToPopOnEndIf.Push(1);
                    return null;
                case "#elif":
                    var fromElifExpr = conditionHashes.Pop();
                    conditionHashes.Push(ComputeConditionHash(
                        scanResult,
                        new PreprocessorExpression
                        {
                            Unary = new PreprocessorExpressionUnary
                            {
                                Type = PreprocessorExpressionTokenType.LogicalNot,
                                Expression = scanResult.Conditions[fromElifExpr].Condition,
                            }
                        }));
                    conditionHashes.Push(ComputeConditionHash(
                        scanResult,
                        PreprocessorExpressionParser.ParseCondition(PreprocessorExpressionLexer.Lex(value))));
                    conditionHashesToPopOnEndIf.Push(conditionHashesToPopOnEndIf.Pop() + 1);
                    return null;
                case "#else":
                    var fromElseExpr = conditionHashes.Pop();
                    conditionHashes.Push(ComputeConditionHash(
                        scanResult,
                        new PreprocessorExpression
                        {
                            Unary = new PreprocessorExpressionUnary
                            {
                                Type = PreprocessorExpressionTokenType.LogicalNot,
                                Expression = scanResult.Conditions[fromElseExpr].Condition,
                            }
                        }));
                    return null;
                case "#endif":
                    var toPop = conditionHashesToPopOnEndIf.Pop();
                    for (int i = 0; i < toPop; i++)
                    {
                        conditionHashes.Pop();
                    }
                    return null;
            }
            return null;
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
            var conditionHashes = new Stack<long>();
            var conditionHashesUsed = new HashSet<long>();
            var conditionHashesToPopOnEndIf = new Stack<int>();
            var enumerator = lines.GetEnumerator();
            var lineNumber = 0;
            var inBlockComment = false;
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
                if (!line.StartsWith('#'))
                {
                    continue;
                }

                // Is it a directive we care about?
                var directive = _directives.FirstOrDefault(x => line.StartsWith(x));
                if (directive == null)
                {
                    continue;
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

                // Split the directive to get the value on the right.
                var components = line.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (directive != components[0])
                {
                    continue;
                }

                // While the value ends in \, grab more lines and stick them on the end.
                string value = string.Empty;
                if (components.Length == 2)
                {
                    value = components[1];
                    while (value[value.Length - 1] == '\\' && (value.Length == 1 || value[value.Length - 2] != '\\'))
                    {
                        enumerator.MoveNext();
                        value = (value.Length > 2 ? value.Substring(0, value.Length - 2) : string.Empty) + "\n" +
                            enumerator.Current;
                    }
                }

                // Handle the directive.
                PreprocessorDirective? processedDirective;
                try
                {
                    processedDirective = ProcessDirective(directive, value, result, conditionHashes, conditionHashesToPopOnEndIf, conditionHashesUsed);
                }
                catch (Exception ex)
                {
                    throw new PreprocessorScannerException(lineNumber, line, ex);
                }
                if (processedDirective != null)
                {
                    result.Directives.Add(processedDirective);
                }
            }
            // Remove any conditions that weren't used by the preprocessor (i.e. these
            // might be #if blocks that only control C/C++ code).
            var unusedConditionHashes = result.Conditions.Keys.ToHashSet();
            unusedConditionHashes.ExceptWith(conditionHashesUsed);
            foreach (var hash in unusedConditionHashes)
            {
                result.Conditions.Remove(hash);
            }
            return result;
        }
    }
}
