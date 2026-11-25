using navigationshurely.models;
using System;
using System.Collections.Generic;
using System.Text;

namespace navigationshurely.services
{
    public class Tokeniser
    {
        public List<Token> ParseToken(string input)
        {
            ArgumentNullException.ThrowIfNull(input);

            var tokens = new List<Token>();
            int i = 0;

            while (i < input.Length)
            {
                char current = input[i];

                if (char.IsWhiteSpace(current))
                {
                    i++;
                    continue;
                }

                // Comments begin at the start of a token.
                if (current == '#')
                {
                    while (i < input.Length && input[i] != '\n')
                        i++;

                    continue;
                }

                if (TryReadSymbol(input, ref i, out Token symbol))
                {
                    tokens.Add(symbol);
                    continue;
                }

                if (current is '"' or '\'')
                {
                    tokens.Add(ReadString(input, ref i));
                    continue;
                }

                tokens.Add(ReadWord(input, ref i));
            }

            tokens.Add(new Token
            {
                Type = TokenType.EndOfInput,
                Value = string.Empty,
                Position = input.Length,
                Length = 0
            });

            return tokens;
        }

        private static bool IsDelimiter(char c)
        {
            return char.IsWhiteSpace(c) || c is ';' or '>';
        }

        private static bool TryReadSymbol(
            string input,
            ref int i,
            out Token token)
        {
            int start = i;
            char current = input[i];

            TokenType? type = current switch
            {
                ';' => TokenType.Semicolon,
                '>' => TokenType.OutputRedirect,
                _ => null
            };

            if (!type.HasValue)
            {
                token = default!;
                return false;
            }

            if (current == '>' &&
                i + 1 < input.Length &&
                input[i + 1] == '>')
            {
                type = TokenType.AppendRedirect;
                i += 2;
            }
            else
            {
                i++;
            }

            token = new Token
            {
                Type = type.Value,
                Value = input.Substring(start, i - start),
                Position = start,
                Length = i - start
            };

            return true;
        }

        private static Token ReadWord(string input, ref int i)
        {
            int start = i;
            var builder = new StringBuilder();

            while (i < input.Length)
            {
                char current = input[i];

                if (IsDelimiter(current))
                    break;

                if (current is '"' or '\'')
                {
                    throw new FormatException(
                        $"Unexpected quote at position {i}.");
                }

                if (current == '\\')
                {
                    i++;

                    if (i >= input.Length)
                        throw new FormatException("Incomplete escape sequence.");

                    builder.Append(input[i]);
                    i++;
                    continue;
                }

                builder.Append(current);
                i++;
            }

            if (builder.Length == 0)
                throw new FormatException(
                    $"Could not parse input at position {i}.");

            return new Token
            {
                Type = TokenType.Word,
                Value = builder.ToString(),
                Position = start,
                Length = i - start
            };
        }

        private static Token ReadString(string input, ref int i)
        {
            int start = i;
            char quote = input[i++];

            var builder = new StringBuilder();
            bool closed = false;

            while (i < input.Length)
            {
                char current = input[i];

                if (current == quote)
                {
                    i++;
                    closed = true;
                    break;
                }

                if (current == '\\')
                {
                    i++;

                    if (i >= input.Length)
                        throw new FormatException(
                            $"Incomplete escape sequence at position {i - 1}.");

                    char escaped = input[i++];

                    switch (escaped)
                    {
                        case 'n':
                            builder.Append('\n');
                            break;

                        case 'r':
                            builder.Append('\r');
                            break;

                        case 't':
                            builder.Append('\t');
                            break;

                        default:
                            builder.Append(escaped);
                            break;
                    }

                    continue;
                }

                builder.Append(current);
                i++;
            }

            if (!closed)
                throw new FormatException(
                    $"Unterminated string beginning at position {start}.");

            if (i < input.Length && !IsDelimiter(input[i]))
            {
                throw new FormatException(
                    $"Unexpected character after quoted string at position {i}.");
            }

            return new Token
            {
                Type = TokenType.String,
                Value = builder.ToString(),
                Position = start,
                Length = i - start
            };
        }
    }
}