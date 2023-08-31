namespace Redpoint.OpenGE.Component.PreprocessorCache.LexerParser
{
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Collections.Generic;

    internal class PreprocessorExpressionParser
    {
        internal static Dictionary<PreprocessorExpressionTokenType, string> _literalMappings = new Dictionary<PreprocessorExpressionTokenType, string>
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
            { PreprocessorExpressionTokenType.BitwiseOr, "|" },
            { PreprocessorExpressionTokenType.BitwiseXor, "^" },
            { PreprocessorExpressionTokenType.BitwiseNot, "~" },
            { PreprocessorExpressionTokenType.LeftShift, "<<" },
            { PreprocessorExpressionTokenType.RightShift, ">>" },
            { PreprocessorExpressionTokenType.LogicalOr, "||" },
            { PreprocessorExpressionTokenType.LogicalAnd, "&&" },
            { PreprocessorExpressionTokenType.LogicalNot, "!" },
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

        // This is expected to parse one of:
        //
        // INVOKE(...)
        // VARIABLE
        // 5 <numeric value>
        // ~[terminal]
        // ![terminal]
        // -[terminal]
        //
        private static PreprocessorExpression ParseTerminal(
            PreprocessorExpressionToken[] tokens,
            ref int position)
        {
            // Skip all leading whitespace.
            while (position < tokens.Length &&
                tokens[position].DataCase == PreprocessorExpressionToken.DataOneofCase.Whitespace)
            {
                position++;
            }
            EnsureNotOutOfTokens(tokens, position);
            PreprocessorExpression returnedExpression;
            switch (tokens[position].DataCase)
            {
                case PreprocessorExpressionToken.DataOneofCase.Identifier:
                    // This is a variable or function at the start of the parse, so
                    // treat it as such.
                    var identifier = tokens[position];
                    position++;
                    // Skip whitespace that might be between an identifier and a ParenOpen.
                    while (position < tokens.Length &&
                        tokens[position].DataCase == PreprocessorExpressionToken.DataOneofCase.Whitespace)
                    {
                        position++;
                    }
                    // @hack: MSVC allows "defined X" to mean "defined(X)".
                    if (position < tokens.Length &&
                        identifier.Identifier == "defined" &&
                        tokens[position].DataCase == PreprocessorExpressionToken.DataOneofCase.Identifier)
                    {
                        returnedExpression = new PreprocessorExpression
                        {
                            Defined = tokens[position].Identifier,
                        };
                        position++;
                        break;
                    }
                    // Invocation of __has_include(), which can contain non-expression
                    // contents in it's only argument.
                    else if (position < tokens.Length &&
                        identifier.Identifier == "__has_include" &&
                        tokens[position].DataCase == PreprocessorExpressionToken.DataOneofCase.Type &&
                        tokens[position].Type == PreprocessorExpressionTokenType.ParenOpen)
                    {
                        position++; // Skip the opening parenthesis.
                        // Skip whitespace that might be between the ParenOpen and the start of the include.
                        while (position < tokens.Length &&
                            tokens[position].DataCase == PreprocessorExpressionToken.DataOneofCase.Whitespace)
                        {
                            position++;
                        }
                        EnsureNotOutOfTokens(tokens, position);
                        if (tokens[position].DataCase == PreprocessorExpressionToken.DataOneofCase.Type &&
                             tokens[position].Type == PreprocessorExpressionTokenType.LessThan ||
                            tokens[position].DataCase == PreprocessorExpressionToken.DataOneofCase.Text &&
                             tokens[position].Text.StartsWith('"'))
                        {
                            // Fixed include expression.
                            var includeBuffer = string.Empty;
                            while (position < tokens.Length &&
                                (tokens[position].DataCase != PreprocessorExpressionToken.DataOneofCase.Type ||
                                 tokens[position].Type != PreprocessorExpressionTokenType.ParenClose))
                            {
                                switch (tokens[position].DataCase)
                                {
                                    case PreprocessorExpressionToken.DataOneofCase.Type:
                                        includeBuffer += _literalMappings[tokens[position].Type];
                                        break;
                                    case PreprocessorExpressionToken.DataOneofCase.Identifier:
                                        includeBuffer += tokens[position].Identifier;
                                        break;
                                    case PreprocessorExpressionToken.DataOneofCase.Number:
                                        includeBuffer += tokens[position].NumberOriginal;
                                        break;
                                    case PreprocessorExpressionToken.DataOneofCase.Text:
                                        includeBuffer += tokens[position].Text;
                                        break;
                                    case PreprocessorExpressionToken.DataOneofCase.Whitespace:
                                        includeBuffer += tokens[position].Whitespace;
                                        break;
                                    default:
                                        throw new NotSupportedException("DataCase in __has_include");
                                }
                                position++;
                            }
                            returnedExpression = new PreprocessorExpression
                            {
                                HasInclude = includeBuffer,
                            };
                            position++;
                            break;
                        }
                        else
                        {
                            throw new Exception("Dynamic include expressions for __has_include are not yet supported!");
                        }
                    }
                    // Normal function invocation.
                    else if (position < tokens.Length &&
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
                        if (identifier.Identifier == "defined" && arguments.Count == 1 &&
                            arguments[0].ExprCase == PreprocessorExpression.ExprOneofCase.Token &&
                            arguments[0].Token.DataCase == PreprocessorExpressionToken.DataOneofCase.Identifier)
                        {
                            returnedExpression = new PreprocessorExpression
                            {
                                Defined = arguments[0].Token.Identifier,
                            };
                        }
                        else
                        {
                            var invocation = new PreprocessorExpressionInvoke
                            {
                                Identifier = identifier.Identifier,
                            };
                            invocation.Arguments.AddRange(arguments);
                            returnedExpression = new PreprocessorExpression
                            {
                                Invoke = invocation,
                            };
                        }
                        break;
                    }
                    else
                    {
                        returnedExpression = new PreprocessorExpression
                        {
                            Token = identifier,
                        };
                        break;
                    }
                case PreprocessorExpressionToken.DataOneofCase.Number:
                    // This is a constant numeric value.
                    returnedExpression = new PreprocessorExpression
                    {
                        Token = tokens[position++],
                    };
                    break;
                case PreprocessorExpressionToken.DataOneofCase.Text:
                    // This is some kind of text fragment (like ".what"),
                    // which isn't parseable at this point.
                    throw new PreprocessorSyntaxException(tokens, position);
                case PreprocessorExpressionToken.DataOneofCase.Type when tokens[position].Type == PreprocessorExpressionTokenType.ParenOpen:
                    // We're opening a nested expression, which could again be
                    // another condition or unary value.
                    position++; // Consume the opening parenthesis.
                    var nestedExpression = ParseExpression(
                        tokens,
                        ref position,
                        new[]
                        {
                            PreprocessorExpressionTokenType.ParenClose,
                        });
                    // ParseComparisonOrUnaryCondition consumes the ParenClose,
                    // so we don't need to move position.
                    returnedExpression = nestedExpression;
                    break;
                case PreprocessorExpressionToken.DataOneofCase.Type when
                    tokens[position].Type == PreprocessorExpressionTokenType.BitwiseNot ||
                    tokens[position].Type == PreprocessorExpressionTokenType.LogicalNot:
                    // This is a bitwise not (~VAL) or boolean not (!VAL),
                    // which need to be handled as potential terminals.
                    position++; // Consume the token.
                    returnedExpression = new PreprocessorExpression
                    {
                        Unary = new PreprocessorExpressionUnary
                        {
                            Type = tokens[position - 1].Type,
                            Expression = ParseTerminal(tokens, ref position),
                        }
                    };
                    break;
                case PreprocessorExpressionToken.DataOneofCase.Type when
                    tokens[position].Type == PreprocessorExpressionTokenType.Subtract:
                    // This is a unary subtract (-VAL), which need to be handled as a
                    // potential terminal.
                    position++; // Consume the token.
                    returnedExpression = new PreprocessorExpression
                    {
                        Binary = new PreprocessorExpressionBinary
                        {
                            Type = PreprocessorExpressionTokenType.Subtract,
                            Left = new PreprocessorExpression
                            {
                                Token = new PreprocessorExpressionToken
                                {
                                    Number = 0,
                                    NumberOriginal = string.Empty,
                                }
                            },
                            Right = ParseTerminal(tokens, ref position),
                        }
                    };
                    break;
                default:
                    // This is some kind of token we weren't expected for a terminal.
                    throw new PreprocessorSyntaxException(tokens, position);
            }
            // Skip all trailing whitespace.
            while (position < tokens.Length &&
                tokens[position].DataCase == PreprocessorExpressionToken.DataOneofCase.Whitespace)
            {
                position++;
            }
            return returnedExpression;
        }

        // All operators we care about are left-to-right associativity, so we don't
        // need to embed that information in this table.
        private readonly static PreprocessorExpressionTokenType[][] _precedenceTable = new PreprocessorExpressionTokenType[][]
        {
            // Precedence 10: Multiplication, division and remainder
            new[]
            {
                PreprocessorExpressionTokenType.Multiply,
                PreprocessorExpressionTokenType.Divide,
                PreprocessorExpressionTokenType.Modulus,
            },
            // Precedence 9: Addition and subtraction
            new[]
            {
                PreprocessorExpressionTokenType.Add,
                PreprocessorExpressionTokenType.Subtract,
            },
            // Precedence 8: Bitwise left shift and right shift
            new[]
            {
                PreprocessorExpressionTokenType.LeftShift,
                PreprocessorExpressionTokenType.RightShift,
            },
            // Precedence 7: Relational comparison
            new[]
            {
                PreprocessorExpressionTokenType.LessThan,
                PreprocessorExpressionTokenType.LessEquals,
                PreprocessorExpressionTokenType.GreaterThan,
                PreprocessorExpressionTokenType.GreaterEquals,
            },
            // Precedence 6: Direct comparison
            new[]
            {
                PreprocessorExpressionTokenType.Equals,
                PreprocessorExpressionTokenType.NotEquals,
            },
            // Precedence 5: Bitwise AND
            new[]
            {
                PreprocessorExpressionTokenType.BitwiseAnd,
            },
            // Precedence 4: Bitwise XOR
            new[]
            {
                PreprocessorExpressionTokenType.BitwiseXor,
            },
            // Precedence 3: Bitwise OR
            new[]
            {
                PreprocessorExpressionTokenType.BitwiseOr,
            },
            // Precedence 2: Logical AND
            new[]
            {
                PreprocessorExpressionTokenType.LogicalAnd,
            },
            // Precedence 1: Logical OR
            new[]
            {
                PreprocessorExpressionTokenType.LogicalOr,
            },
        };

        private static int GetPrecedenceLevel(PreprocessorExpressionTokenType @operator)
        {
            for (int i = 0; i < _precedenceTable.Length; i++)
            {
                if (_precedenceTable[i].Contains(@operator))
                {
                    return _precedenceTable.Length - i;
                }
            }
            return -1;
        }

        private static PreprocessorExpression ParseExpression(
            PreprocessorExpressionToken[] tokens,
            ref int position,
            PreprocessorExpressionTokenType[] terminators)
        {
            // Get the first terminal.
            var lhs = ParseTerminal(tokens, ref position);

            // Check if we're terminating the expression at this point.
            if (position >= tokens.Length ||
                terminators.Contains(tokens[position].Type))
            {
                if (position < tokens.Length)
                {
                    position++;
                }
                return lhs;
            }

            // See what the first operator is.
            if (tokens[position].DataCase != PreprocessorExpressionToken.DataOneofCase.Type)
            {
                // We expect some kind of operator at this point.
                throw new PreprocessorSyntaxException(tokens, position);
            }
            var firstOperator = tokens[position].Type;
            var firstOperatorPrecedence = GetPrecedenceLevel(firstOperator);
            if (firstOperatorPrecedence == -1)
            {
                // This was an unexpected operator.
                throw new PreprocessorSyntaxException(tokens, position);
            }
            position++;

            while (true)
            {
                // Get the second terminal.
                var rewindPosition = position;
                var rhs = ParseTerminal(tokens, ref position);

                // Check if we're out of tokens or are forcibly terminating.
                if (position >= tokens.Length ||
                    terminators.Contains(tokens[position].Type))
                {
                    if (position < tokens.Length)
                    {
                        position++;
                    }
                    return new PreprocessorExpression
                    {
                        Binary = new PreprocessorExpressionBinary
                        {
                            Left = lhs,
                            Right = rhs,
                            Type = firstOperator,
                        }
                    };
                }

                // See what the second operator is. This allows us to figure out
                // precedence to determine how things are binding together.
                if (tokens[position].DataCase != PreprocessorExpressionToken.DataOneofCase.Type)
                {
                    // We expect some kind of operator at this point.
                    throw new PreprocessorSyntaxException(tokens, position);
                }
                var secondOperator = tokens[position].Type;
                if (secondOperator == PreprocessorExpressionTokenType.ParenClose)
                {
                    // This is terminating a parenthesised expression.
                    position++;
                    return new PreprocessorExpression
                    {
                        Binary = new PreprocessorExpressionBinary
                        {
                            Left = lhs,
                            Right = rhs,
                            Type = firstOperator,
                        }
                    };
                }
                var secondOperatorPrecedence = GetPrecedenceLevel(secondOperator);
                if (secondOperatorPrecedence == -1)
                {
                    // This was an unexpected operator.
                    throw new PreprocessorSyntaxException(tokens, position);
                }

                // If the second operator has a higher precedence value than our first
                // operator, then we rewind to before our second terminal and allow the
                // RHS to become an expression.
                if (secondOperatorPrecedence > firstOperatorPrecedence)
                {
                    position = rewindPosition;
                    rhs = ParseExpression(
                        tokens,
                        ref position,
                        // Any token type that is the same or higher precedence value (
                        // lower binding priority) than our current operator should
                        // terminate it.
                        _precedenceTable.Where((tokens, idx) => (_precedenceTable.Length - idx) <= firstOperatorPrecedence)
                            .SelectMany(x => x)
                            .ToArray());

                    // ParseExpression will have consumed our terminator, but we actually
                    // want to look at it. Check if we're out of tokens or are
                    // forcibly terminating after consuming the RHS as an expression.
                    if (position >= tokens.Length ||
                        terminators.Contains(tokens[position - 1].Type))
                    {
                        return new PreprocessorExpression
                        {
                            Binary = new PreprocessorExpressionBinary
                            {
                                Left = lhs,
                                Right = rhs,
                                Type = firstOperator,
                            }
                        };
                    }
                    position--;

                    // See what the second operator is. This allows us to figure out
                    // precedence to determine how things are binding together.
                    if (tokens[position].DataCase != PreprocessorExpressionToken.DataOneofCase.Type)
                    {
                        // We expect some kind of operator at this point.
                        throw new PreprocessorSyntaxException(tokens, position);
                    }
                    secondOperator = tokens[position].Type;
                    secondOperatorPrecedence = GetPrecedenceLevel(secondOperator);
                    if (secondOperatorPrecedence == -1)
                    {
                        // This was an unexpected operator.
                        throw new PreprocessorSyntaxException(tokens, position);
                    }

                    // e.g. a + b * c [+]
                    // LHS = a
                    // OP1 = +
                    // RHS = (b * c)
                    // OP2 = +
                }
                else
                {
                    // e.g. a * b [*]
                    // LHS = a
                    // OP1 = *
                    // RHS = b
                    // OP2 = *

                    // e.g. a * b [+]
                    // LHS = a
                    // OP1 = *
                    // RHS = b
                    // OP2 = +
                }

                // Move past the operator.
                position++;

                // Combine the LHS and RHS and store as the LHS so that we can loop again
                // to check for more RHS.
                lhs = new PreprocessorExpression
                {
                    Binary = new PreprocessorExpressionBinary
                    {
                        Left = lhs,
                        Right = rhs,
                        Type = firstOperator,
                    }
                };

                // The second operator now counts as the first operator, because we've
                // move our whole expression so far into the LHS.
                firstOperator = secondOperator;
                firstOperatorPrecedence = secondOperatorPrecedence;
            }
        }

        internal static PreprocessorExpression ParseCondition(IEnumerable<PreprocessorExpressionToken> tokens)
        {
            // @todo: We could optimize this by not pulling tokens we don't need yet.
            var allTokens = tokens.ToArray();
            if (allTokens.Length == 0)
            {
                throw new PreprocessorSyntaxException(allTokens, 0);
            }
            int position = 0;
            return ParseExpression(allTokens, ref position, Array.Empty<PreprocessorExpressionTokenType>());
        }
    }
}
