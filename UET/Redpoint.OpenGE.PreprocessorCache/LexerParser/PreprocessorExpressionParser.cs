namespace Redpoint.OpenGE.PreprocessorCache.LexerParser
{
    using PreprocessorCacheApi;
    using System;
    using System.Collections.Generic;

    internal class PreprocessorExpressionParser
    {
        private static Dictionary<PreprocessorExpressionTokenType, string> _literalMappings = new Dictionary<PreprocessorExpressionTokenType, string>
        {
            { PreprocessorExpressionTokenType.ParenOpen, "(" },
            { PreprocessorExpressionTokenType.ParenClose, ")" },
            { PreprocessorExpressionTokenType.Add, "+" },
            { PreprocessorExpressionTokenType.Subtract, "-" },
            { PreprocessorExpressionTokenType.Multiply, "*" },
            { PreprocessorExpressionTokenType.Divide, "/" },
            { PreprocessorExpressionTokenType.Modulus, "%" },
            { PreprocessorExpressionTokenType.Equals, "==" },
            { PreprocessorExpressionTokenType.NotEquals, "!=" },
            { PreprocessorExpressionTokenType.LessThan, "<" },
            { PreprocessorExpressionTokenType.LessEquals, "<=" },
            { PreprocessorExpressionTokenType.GreaterThan, ">" },
            { PreprocessorExpressionTokenType.GreaterEquals, ">=" },
            { PreprocessorExpressionTokenType.BitwiseAnd, "&" },
            { PreprocessorExpressionTokenType.BitwiseBor, "|" },
            { PreprocessorExpressionTokenType.BitwiseXor, "^" },
            { PreprocessorExpressionTokenType.BitwiseNot, "~" },
            { PreprocessorExpressionTokenType.LeftShift, "<<" },
            { PreprocessorExpressionTokenType.RightShift, ">>" },
            { PreprocessorExpressionTokenType.BooleanOr, "||" },
            { PreprocessorExpressionTokenType.BooleanAnd, "&&" },
            { PreprocessorExpressionTokenType.BooleanXor, "^^" },
            { PreprocessorExpressionTokenType.BooleanNot, "!" },
            { PreprocessorExpressionTokenType.Stringify, "#" },
            { PreprocessorExpressionTokenType.Join, "##" },
            { PreprocessorExpressionTokenType.Comma, "," },
        };

        private static void EnsureNotOutOfTokens(PreprocessorExpressionToken[] tokens, int position)
        {
            if (position >= tokens.Length)
            {
                throw new PreprocessorSyntaxException(tokens, position);
            }
        }

        private static PreprocessorExpression ParseExpansion(
            PreprocessorExpressionToken[] tokens, 
            ref int position,
            PreprocessorExpressionTokenType[] terminators)
        {
            var chain = new List<PreprocessorExpression>();
            var terminating = false;
            while (position < tokens.Length && !terminating)
            {
                switch (tokens[position].DataCase)
                {
                    case PreprocessorExpressionToken.DataOneofCase.Whitespace:
                        var whitespaceBuffer = new List<string>();
                        while (position < tokens.Length && tokens[position].DataCase == PreprocessorExpressionToken.DataOneofCase.Whitespace)
                        {
                            whitespaceBuffer.Add(tokens[position].Whitespace);
                            position++;
                        }
                        chain.Add(new PreprocessorExpression
                        {
                            Whitespace = string.Join(string.Empty, whitespaceBuffer),
                        });
                        break;
                    case PreprocessorExpressionToken.DataOneofCase.Identifier:
                        var identifier = tokens[position];
                        position++;
                        if (position < tokens.Length &&
                            tokens[position].DataCase == PreprocessorExpressionToken.DataOneofCase.Type &&
                            tokens[position].Type == PreprocessorExpressionTokenType.ParenOpen)
                        {
                            // This is an invocation, jump over the opening parenthesis.
                            position++;
                            var arguments = new List<PreprocessorExpression>();
                            EnsureNotOutOfTokens(tokens, position);
                            if (tokens[position].DataCase != PreprocessorExpressionToken.DataOneofCase.Type ||
                                tokens[position].Type != PreprocessorExpressionTokenType.ParenClose)
                            {
                                // We have arguments.
                                do
                                {
                                    arguments.Add(ParseExpansion(
                                        tokens, 
                                        ref position, 
                                        new[] { PreprocessorExpressionTokenType.Comma, PreprocessorExpressionTokenType.ParenClose }));
                                    if (tokens[position - 1].Type == PreprocessorExpressionTokenType.Comma)
                                    {
                                        // We're processing another argument.
                                        continue;
                                    }
                                    else
                                    {
                                        // We've ended the argument list.
                                        break;
                                    }
                                }
                                while (true);
                            }
                            else
                            {
                                // We need to consume the ParenClose, because we don't have a ParseExpansion
                                // to do it for us.
                                position++;
                            }
                            var invocation = new PreprocessorExpressionInvoke
                            {
                                Identifier = identifier.Identifier,
                            };
                            invocation.Arguments.AddRange(arguments);
                            chain.Add(new PreprocessorExpression
                            {
                                Invoke = invocation,
                            });
                        }
                        else
                        {
                            chain.Add(new PreprocessorExpression
                            {
                                Token = identifier,
                            });
                        }
                        break;
                    case PreprocessorExpressionToken.DataOneofCase.Text:
                    case PreprocessorExpressionToken.DataOneofCase.Number:
                        chain.Add(new PreprocessorExpression
                        {
                            Token = tokens[position],
                        });
                        position++;
                        break;
                    case PreprocessorExpressionToken.DataOneofCase.Type when terminators.Contains(tokens[position].Type):
                        // We're terminating a subchain (i.e. argument list).
                        position++; // Consume the terminating token.
                        terminating = true;
                        break;
                    case PreprocessorExpressionToken.DataOneofCase.Type when tokens[position].Type == PreprocessorExpressionTokenType.Join:
                        // We're joining the last chain with the next chain. This means removing trailing
                        // whitespace entries from the chain, preserving the join (for recursive evaluation),
                        // and then consuming whitespace in the chain until we get to a non-whitespace token.
                        while (chain.Count > 0 && chain[chain.Count - 1].ExprCase == PreprocessorExpression.ExprOneofCase.Whitespace)
                        {
                            chain.RemoveAt(chain.Count - 1);
                        }
                        chain.Add(new PreprocessorExpression
                        {
                            // Capture the join token and move past it.
                            Token = tokens[position++],
                        });
                        while (position < tokens.Length && tokens[position].DataCase == PreprocessorExpressionToken.DataOneofCase.Whitespace)
                        {
                            // Consume whitespace.
                            position++;
                        }
                        break;
                    case PreprocessorExpressionToken.DataOneofCase.Type when tokens[position].Type == PreprocessorExpressionTokenType.Stringify
                        && position + 1 < tokens.Length
                        && tokens[position + 1].DataCase == PreprocessorExpressionToken.DataOneofCase.Identifier:
                        // We're inserting a "stringify" operation into the chain.
                        position++; // Consume the # token.
                        chain.Add(new PreprocessorExpression
                        {
                            Unary = new PreprocessorExpressionUnary
                            {
                                Type = PreprocessorExpressionTokenType.Stringify,
                                Expression = new PreprocessorExpression
                                {
                                    // Capture the identifier.
                                    Token = tokens[position],
                                }
                            }
                        });
                        position++; // Consume the identifier.
                        break;
                    case PreprocessorExpressionToken.DataOneofCase.Type when tokens[position].Type == PreprocessorExpressionTokenType.ParenOpen:
                        // We're opening a (...) content section, but the parenthesis will be literal in
                        // the C++ code, so we need to insert those as text and process the content as
                        // a subchain.
                        position++; // Consume the opening parenthesis.
                        chain.Add(new PreprocessorExpression
                        {
                            Token = new PreprocessorExpressionToken
                            {
                                Text = _literalMappings[PreprocessorExpressionTokenType.ParenOpen]
                            }
                        });
                        var subchain = ParseExpansion(
                            tokens,
                            ref position,
                            new[] { PreprocessorExpressionTokenType.ParenClose });
                        if (subchain.ExprCase == PreprocessorExpression.ExprOneofCase.Chain)
                        {
                            chain.AddRange(subchain.Chain.Expressions);
                        }
                        else
                        {
                            chain.Add(subchain);
                        }
                        // ParseExpansion consumes the ParenClose and moves us past it.
                        chain.Add(new PreprocessorExpression
                        {
                            Token = new PreprocessorExpressionToken
                            {
                                Text = _literalMappings[PreprocessorExpressionTokenType.ParenClose]
                            }
                        });
                        break;
                    case PreprocessorExpressionToken.DataOneofCase.Type:
                        // Although this is a recognised token, it's actually just a literal value
                        // because we're expanding text, not evaluating an #if expression.
                        chain.Add(new PreprocessorExpression
                        {
                            Token = new PreprocessorExpressionToken
                            {
                                Text = _literalMappings[tokens[position].Type]
                            }
                        });
                        position++; // Consume the token.
                        break;
                }
            }
            if (chain.Count == 1)
            {
                return chain[0];
            }
            else
            {
                var chainExpr = new PreprocessorExpressionChain();
                chainExpr.Expressions.AddRange(chain);
                return new PreprocessorExpression
                {
                    Chain = chainExpr,
                };
            }
        }

        internal static PreprocessorExpression ParseExpansion(IEnumerable<PreprocessorExpressionToken> tokens)
        {
            // @todo: We could optimize this by not pulling tokens we don't need yet.
            var allTokens = tokens.ToArray();
            if (allTokens.Length == 0)
            {
                throw new PreprocessorSyntaxException(allTokens, 0);
            }
            int position = 0;
            return ParseExpansion(allTokens, ref position, Array.Empty<PreprocessorExpressionTokenType>());
        }

#if FALSE

        private static PreprocessorExpression ParseExpansionExpr(PreprocessorExpressionToken[] tokens, ref int position)
        {
            var token = tokens[position];
            switch (token.DataCase)
            {
                case PreprocessorExpressionToken.DataOneofCase.Whitespace:
                    position++;
                    return ParseExpr(tokens, ref position);
                case PreprocessorExpressionToken.DataOneofCase.Identifier:
                    var identifier = tokens[position];
                    position++;
                    if (position >= tokens.Length ||
                        tokens[position].DataCase == PreprocessorExpressionToken.DataOneofCase.Identifier ||
                        tokens[position].DataCase == PreprocessorExpressionToken.DataOneofCase.Whitespace ||
                        tokens[position].DataCase == PreprocessorExpressionToken.DataOneofCase.Text ||
                        tokens[position].DataCase == PreprocessorExpressionToken.DataOneofCase.Number)
                    {
                        return new PreprocessorExpression
                        {
                            Token = identifier,
                        };
                    }
                    switch (tokens[position].Type)
                    {
                        case PreprocessorExpressionTokenType.ParenOpen:
                            // This is an invocation.
                            position++;
                            var arguments = new List<PreprocessorExpression>();
                            EnsureNotOutOfTokens(tokens, position);
                            if (tokens[position].DataCase != PreprocessorExpressionToken.DataOneofCase.Type)
                            {
                                throw new PreprocessorSyntaxException(tokens, position);
                            }
                            if (tokens[position].Type != PreprocessorExpressionTokenType.ParenClose)
                            {
                                // We have arguments.
                                do
                                {
                                    arguments.Add(ParseExpansionSubchain(tokens, ref position, new[] { PreprocessorExpressionTokenType.Comma, PreprocessorExpressionTokenType.ParenClose }));
                                    if (tokens[position - 1].Type == PreprocessorExpressionTokenType.Comma)
                                    {
                                        // We're processing another argument.
                                        continue;
                                    }
                                    else
                                    {
                                        // We've ended the argument list.
                                        break;
                                    }
                                }
                                while (true);
                            }
                            var invocation = new PreprocessorExpressionInvoke
                            {
                                Identifier = identifier.Identifier,
                            };
                            invocation.Arguments.AddRange(arguments);
                            return new PreprocessorExpression
                            {
                                Invoke = invocation,
                            };
                        default:
                            return ParsePotentialBinaryOperator(identifier, tokens, ref position);
                    }
                case PreprocessorExpressionToken.DataOneofCase.Text:
                case PreprocessorExpressionToken.DataOneofCase.Number:
                    position++;
                    return ParsePotentialBinaryOperator(tokens[position - 1], tokens, ref position);
                case PreprocessorExpressionToken.DataOneofCase.Type:
                    switch (tokens[position].Type)
                    {
                        case PreprocessorExpressionTokenType.ParenOpen:
                            position++;
                            // ParsePotentialChain will consume the ParenClose.
                            return ParseExpansionSubchain(tokens, ref position, new[]
                            {
                                PreprocessorExpressionTokenType.ParenClose
                            });
                        case PreprocessorExpressionTokenType.Subtract:
                            position++;
                            return new PreprocessorExpression
                            {
                                Binary = new PreprocessorExpressionBinary
                                {
                                    Left = new PreprocessorExpression
                                    {
                                        Token = new PreprocessorExpressionToken
                                        {
                                            Number = 0,
                                        }
                                    },
                                    Right = ParseExpr(tokens, ref position),
                                    Type = PreprocessorExpressionTokenType.Subtract,
                                }
                            };
                        case PreprocessorExpressionTokenType.BooleanNot:
                        case PreprocessorExpressionTokenType.BitwiseNot:
                            position++;
                            return new PreprocessorExpression
                            {
                                Unary = new PreprocessorExpressionUnary
                                {
                                    Type = tokens[position].Type,
                                    Expression = ParseExpr(tokens, ref position),
                                }
                            };
                        default:
                            throw new PreprocessorSyntaxException(tokens, position);
                    }
                default:
                    throw new PreprocessorSyntaxException(tokens, position);
            }
        }

        internal static PreprocessorExpression Parse(IEnumerable<PreprocessorExpressionToken> tokens)
        {
            // @todo: We could optimize this by not pulling tokens we don't need yet.
            var allTokens = tokens.ToArray();
            if (allTokens.Length == 0)
            {
                throw new InvalidOperationException();
            }
            int position = 0;
            return ParseExpr(allTokens, ref position);
        }

#endif
    }
}
