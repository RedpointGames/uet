namespace Redpoint.OpenGE.PreprocessorCache.LexerParser
{
    using PreprocessorCacheApi;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    internal class PreprocessorScanner
    {
        private static string[] _directives = new[]
        {
            "#include",
            "#define",
            "#undef",
#if FALSE
            "#if",
            "#ifdef",
            "#ifndef",
            "#elif",
            "#else",
            "#endif",
#endif
        };

        private static Regex _functionDefine = new Regex("^([A-Za-z_][A-Za-z0-9_]*)\\(([a-zA-Z0-9,_\\s]*)\\)\\s");
        private static Regex _variableDefine = new Regex("^([A-Za-z_][A-Za-z0-9_]*)(\\s|$)");

        private static PreprocessorDirective MakeDirective(Stack<PreprocessorExpression> conditions, PreprocessorDirective directive)
        {
            directive.Conditions.AddRange(conditions);
            return directive;
        }

        private static T ProcessWithContext<T>(string line, Func<T> callback)
        {
            try
            {
                return callback();
            }
            catch (PreprocessorSyntaxException ex)
            {
                throw new PreprocessorScannerException(line, ex);
            }
        }

        internal static IEnumerable<PreprocessorDirective> Scan(IEnumerable<string> lines)
        {
            var conditions = new Stack<PreprocessorExpression>();
            var conditionsToPopOnEndIf = new Stack<int>();
            var enumerator = lines.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var line = enumerator.Current.TrimStart();
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
                switch (directive)
                {
                    case "#include":
                        var include = value.Trim();
                        var includeQuote = include.StartsWith('"');
                        var includeAngle = include.StartsWith('<');
                        if (includeQuote || includeAngle)
                        {
                            // @note: Figure out the behaviour when an expression-based include
                            // has a comment at the end? Probably also should be trimmed.
                            if (include.IndexOf("//") != -1)
                            {
                                include = include.Substring(0, include.IndexOf("//")).TrimEnd();
                            }
                            if (include.IndexOf("/*") != -1)
                            {
                                include = include.Substring(0, include.IndexOf("/*")).TrimEnd();
                            }
                        }
                        if (includeQuote)
                        {
                            yield return MakeDirective(conditions, new PreprocessorDirective
                            {
                                Include = new PreprocessorDirectiveInclude
                                {
                                    Normal = include.Trim('"'),
                                }
                            });
                        }
                        else if (includeAngle)
                        {
                            yield return MakeDirective(conditions, new PreprocessorDirective
                            {
                                Include = new PreprocessorDirectiveInclude
                                {
                                    System = include.TrimStart('<').TrimEnd('>'),
                                }
                            });
                        }
                        else
                        {
                            yield return MakeDirective(conditions, new PreprocessorDirective
                            {
                                Include = new PreprocessorDirectiveInclude
                                {
                                    Expansion = ProcessWithContext(
                                        line, 
                                        () => PreprocessorExpressionParser.ParseExpansion(PreprocessorExpressionLexer.Lex(include))),
                                }
                            });
                        }
                        break;
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
                                    : ProcessWithContext(
                                        line,
                                        () => PreprocessorExpressionParser.ParseExpansion(PreprocessorExpressionLexer.Lex(exprText))),
                            };
                            funcDefine.Parameters.AddRange(
                                functionMatch.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                            yield return MakeDirective(conditions, new PreprocessorDirective
                            {
                                Define = funcDefine,
                            });
                        }
                        else if (variableMatch.Success)
                        {
                            var exprText = value.Length > variableMatch.Length ? value.Substring(variableMatch.Length) : string.Empty;
                            yield return MakeDirective(conditions, new PreprocessorDirective
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
                                        : ProcessWithContext(
                                            line,
                                            () => PreprocessorExpressionParser.ParseExpansion(PreprocessorExpressionLexer.Lex(exprText))),
                                },
                            });
                        }
                        else
                        {
                            // Lenience.
                        }
                        break;
                    case "#undef":
                        yield return MakeDirective(conditions, new PreprocessorDirective
                        {
                            Undefine = new PreprocessorDirectiveUndefine
                            {
                                Identifier = value.Trim(),
                            }
                        });
                        break;
#if FALSE
                    case "#if":
                        throw new NotImplementedException();
                    /*
                conditions.Push(PreprocessorExpressionParser.Parse(PreprocessorExpressionLexer.Lex(value)));
                conditionsToPopOnEndIf.Push(1);
                    break;
                    */
                    case "#ifdef":
                        conditions.Push(new PreprocessorExpression
                        {
                            Defined = value.Trim(),
                        });
                        conditionsToPopOnEndIf.Push(1);
                        break;
                    case "#ifndef":
                        conditions.Push(new PreprocessorExpression
                        {
                            NotDefined = value.Trim(),
                        });
                        conditionsToPopOnEndIf.Push(1);
                        break;
                    case "#elif":
                        var fromElifExpr = conditions.Pop();
                        conditions.Push(new PreprocessorExpression
                        {
                            Unary = new PreprocessorExpressionUnary
                            {
                                Type = PreprocessorExpressionTokenType.BooleanNot,
                                Expression = fromElifExpr,
                            }
                        });
                        throw new NotImplementedException();
                    /*
                conditions.Push(PreprocessorExpressionParser.Parse(PreprocessorExpressionLexer.Lex(value)));
                conditionsToPopOnEndIf.Push(conditionsToPopOnEndIf.Pop() + 1);
                break;
                    */
                    case "#else":
                        var fromElseExpr = conditions.Pop();
                        conditions.Push(new PreprocessorExpression
                        {
                            Unary = new PreprocessorExpressionUnary
                            {
                                Type = PreprocessorExpressionTokenType.BooleanNot,
                                Expression = fromElseExpr,
                            }
                        });
                        break;
                    case "#endif":
                        var toPop = conditionsToPopOnEndIf.Pop();
                        for (int i = 0; i < toPop; i++)
                        {
                            conditions.Pop();
                        }
                        break;
#endif
                }
            }
        }
    }
}
