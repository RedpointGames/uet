namespace Redpoint.OpenGE.Component.PreprocessorCache.LexerParser
{
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class PreprocessorExpressionLexer
    {
        internal static IEnumerable<PreprocessorExpressionToken> Lex(string expression)
        {
            var buffer = new StringBuilder();
            var inTextMode = false;
            var inUnbreakableTextMode = false;
            var unbreakableTerminator = '\0';
            var isEscaping = false;
            for (int position = 0; position < expression.Length; position++)
            {
                var current = expression[position];
                var lookAhead = position == expression.Length - 1 ? unchecked((char)-1) : expression[position + 1];
                PreprocessorExpressionTokenType? nextToken = null;
                if (!inUnbreakableTextMode)
                {
                    switch (current)
                    {
                        case '(':
                            nextToken = PreprocessorExpressionTokenType.ParenOpen;
                            break;
                        case ')':
                            nextToken = PreprocessorExpressionTokenType.ParenClose;
                            break;
                        case '+':
                            nextToken = PreprocessorExpressionTokenType.Add;
                            break;
                        case '-':
                            nextToken = PreprocessorExpressionTokenType.Subtract;
                            break;
                        case '*':
                            nextToken = PreprocessorExpressionTokenType.Multiply;
                            break;
                        case '/' when lookAhead == '*':
                            // This will the start of a comment block, which we'll
                            // handle in unbreakable text mode.
                            nextToken = null;
                            break;
                        case '/':
                            nextToken = PreprocessorExpressionTokenType.Divide;
                            break;
                        case '%':
                            nextToken = PreprocessorExpressionTokenType.Modulus;
                            break;
                        case '=' when lookAhead == '=':
                            nextToken = PreprocessorExpressionTokenType.Equals;
                            position++;
                            break;
                        case '!' when lookAhead == '=':
                            nextToken = PreprocessorExpressionTokenType.NotEquals;
                            position++;
                            break;
                        case '<' when lookAhead == '=':
                            nextToken = PreprocessorExpressionTokenType.LessEquals;
                            position++;
                            break;
                        case '<' when lookAhead == '<':
                            nextToken = PreprocessorExpressionTokenType.LeftShift;
                            position++;
                            break;
                        case '<':
                            nextToken = PreprocessorExpressionTokenType.LessThan;
                            break;
                        case '>' when lookAhead == '=':
                            nextToken = PreprocessorExpressionTokenType.GreaterEquals;
                            position++;
                            break;
                        case '>' when lookAhead == '>':
                            nextToken = PreprocessorExpressionTokenType.RightShift;
                            position++;
                            break;
                        case '>':
                            nextToken = PreprocessorExpressionTokenType.GreaterThan;
                            break;
                        case '&' when lookAhead == '&':
                            nextToken = PreprocessorExpressionTokenType.LogicalAnd;
                            position++;
                            break;
                        case '&':
                            nextToken = PreprocessorExpressionTokenType.BitwiseAnd;
                            break;
                        case '|' when lookAhead == '|':
                            nextToken = PreprocessorExpressionTokenType.LogicalOr;
                            position++;
                            break;
                        case '|':
                            nextToken = PreprocessorExpressionTokenType.BitwiseOr;
                            break;
                        case '^':
                            nextToken = PreprocessorExpressionTokenType.BitwiseXor;
                            break;
                        case '~':
                            nextToken = PreprocessorExpressionTokenType.BitwiseNot;
                            break;
                        case '!':
                            nextToken = PreprocessorExpressionTokenType.LogicalNot;
                            break;
                        case '#' when lookAhead == '#':
                            nextToken = PreprocessorExpressionTokenType.Join;
                            position++;
                            break;
                        case '#':
                            nextToken = PreprocessorExpressionTokenType.Stringify;
                            break;
                        case ',':
                            nextToken = PreprocessorExpressionTokenType.Comma;
                            break;
                    }
                }
                if (nextToken.HasValue)
                {
                    if (buffer.Length > 0)
                    {
                        // Flush the text buffer.
                        yield return new PreprocessorExpressionToken
                        {
                            Text = buffer.ToString(),
                        };
                        buffer.Clear();
                    }
                    yield return new PreprocessorExpressionToken
                    {
                        Type = nextToken.Value,
                    };
                    inTextMode = false;
                }
                else if (!inTextMode)
                {
                    if (current >= 'A' && current <= 'Z' ||
                        current >= 'a' && current <= 'z' ||
                        current == '_')
                    {
                        // Process word.
                        buffer.Append(current);
                        position++;
                        for (; position < expression.Length; position++)
                        {
                            current = expression[position];
                            if (current >= 'A' && current <= 'Z' ||
                                current >= 'a' && current <= 'z' ||
                                current >= '0' && current <= '9' ||
                                current == '_')
                            {
                                buffer.Append(current);
                            }
                            else
                            {
                                break;
                            }
                        }
                        // Rewind since we've gone too far. The outer
                        // loop will move it forward.
                        position--;
                        yield return new PreprocessorExpressionToken
                        {
                            Identifier = buffer.ToString(),
                        };
                        buffer.Clear();
                    }
                    else if (current == '0' &&
                        lookAhead != 'b' &&
                        lookAhead != 'x' &&
                        !(lookAhead >= '0' && lookAhead <= '9') &&
                        !(lookAhead >= 'a' && lookAhead <= 'f') &&
                        !(lookAhead >= 'A' && lookAhead <= 'F'))
                    {
                        // Process exactly zero.
                        yield return new PreprocessorExpressionToken
                        {
                            Number = 0,
                            NumberOriginal = "0",
                        };
                    }
                    else if (current == '0' && (lookAhead == 'b' || lookAhead == 'x' || lookAhead >= '0' && lookAhead <= '7') ||
                        current >= '1' && current <= '9')
                    {
                        // Process binary number.
                        var @base = lookAhead switch
                        {
                            'b' => 2,
                            'x' => 16,
                            _ when current >= '1' && current <= '9' => 10,
                            _ => 8,
                        };
                        if (@base != 10)
                        {
                            position++;
                            if (@base != 8)
                            {
                                position++;
                            }
                        }
                        var isUnsigned = false;
                        for (; position < expression.Length; position++)
                        {
                            current = expression[position];
                            if (@base == 2 && current >= '0' && current <= '1' ||
                                @base == 8 && current >= '0' && current <= '7' ||
                                @base == 10 && current >= '0' && current <= '9' ||
                                @base == 16 && (current >= '0' && current <= '9' ||
                                    current >= 'a' && current <= 'f' ||
                                    current >= 'A' && current <= 'F'))
                            {
                                buffer.Append(current);
                            }
                            else if (current == 'u' || current == 'U')
                            {
                                // "unsigned long" indicator; we just want it gone.
                                isUnsigned = true;
                                position++;
                            }
                            else if (current == 'l' || current == 'L')
                            {
                                // "long" indicator; we just want it gone.
                                position++;
                            }
                            else
                            {
                                break;
                            }
                        }
                        // Rewind since we've gone too far. The outer
                        // loop will move it forward.
                        position--;
                        long value;
                        try
                        {
                            value = isUnsigned
                                ? unchecked((long)Convert.ToUInt64(buffer.ToString(), @base))
                                : Convert.ToInt64(buffer.ToString(), @base);
                        }
                        catch
                        {
                            // We want to be extremely lenient because all we really
                            // care about for this lexer is determining includes so
                            // we can send them over the network.
                            value = 0;
                        }
                        yield return new PreprocessorExpressionToken
                        {
                            Number = value,
                            NumberOriginal = buffer.ToString(),
                        };
                        buffer.Clear();
                    }
                    else if (current == ' ' || current == '\t' || current == '\n' || current == '\r')
                    {
                        // Return whitespace elements.
                        yield return new PreprocessorExpressionToken
                        {
                            Whitespace = current.ToString(),
                        };
                    }
                    else
                    {
                        // Start processing text.
                        inTextMode = true;
                        buffer.Append(current);

                        if (current == '"')
                        {
                            inUnbreakableTextMode = true;
                            unbreakableTerminator = '"';
                        }
                        else if (current == '\'')
                        {
                            inUnbreakableTextMode = true;
                            unbreakableTerminator = '\'';
                        }
                        else if (current == '/' && lookAhead == '*')
                        {
                            inUnbreakableTextMode = true;
                            unbreakableTerminator = '*';
                        }
                    }
                }
                else if (inUnbreakableTextMode && isEscaping)
                {
                    buffer.Append(current);
                    isEscaping = false;
                }
                else if (inUnbreakableTextMode)
                {
                    buffer.Append(current);

                    if (current == '\\')
                    {
                        // We're escaping the next character.
                        isEscaping = true;
                    }
                    else if (current == unbreakableTerminator)
                    {
                        if (unbreakableTerminator == '*' && lookAhead == '/' ||
                            unbreakableTerminator != '*')
                        {
                            inUnbreakableTextMode = false;
                            inTextMode = false;
                            if (buffer.Length > 0)
                            {
                                // Flush the text buffer.
                                yield return new PreprocessorExpressionToken
                                {
                                    Text = buffer.ToString(),
                                };
                                buffer.Clear();
                            }
                        }
                    }
                }
                else if (inTextMode)
                {
                    if (current == ' ' || current == '\t')
                    {
                        // Terminates text mode.
                        inTextMode = false;
                        if (buffer.Length > 0)
                        {
                            // Flush the text buffer.
                            yield return new PreprocessorExpressionToken
                            {
                                Text = buffer.ToString(),
                            };
                            buffer.Clear();
                        }
                        yield return new PreprocessorExpressionToken
                        {
                            Whitespace = current.ToString(),
                        };
                    }
                    else
                    {
                        // Otherwise append text.
                        buffer.Append(current);

                        if (current == '"')
                        {
                            inUnbreakableTextMode = true;
                            unbreakableTerminator = '"';
                        }
                        else if (current == '\'')
                        {
                            inUnbreakableTextMode = true;
                            unbreakableTerminator = '\'';
                        }
                        else if (current == '/' && lookAhead == '*')
                        {
                            inUnbreakableTextMode = true;
                            unbreakableTerminator = '*';
                        }
                    }
                }
            }

            if (buffer.Length > 0)
            {
                // Flush the text buffer.
                yield return new PreprocessorExpressionToken
                {
                    Text = buffer.ToString(),
                };
                buffer.Clear();
            }
        }
    }
}
