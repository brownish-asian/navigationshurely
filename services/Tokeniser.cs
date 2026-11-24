using navigationshurely.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

                // Ignore whitespace.
                if (char.IsWhiteSpace(current))
                {
                    i++;
                    continue;
                }

                // Single-character structural tokens.
                if (TryReadSymbol(current, i, out Token symbolToken))
                {
                    tokens.Add(symbolToken);
                    i++;
                    continue;
                }

                // Quoted string.
                if (current == '"' || current == '\'')
                {
                    tokens.Add(ReadString(input, ref i));
                    continue;
                }

                // Ordinary word.
                tokens.Add(ReadWord(input, ref i));
            }

            tokens.Add(new Token
            {
                Type = TokenType.EndOfInput,
                Value = "",
                Position = input.Length,
                Length = 0
            });

            return tokens;
        }

        private static bool IsDelimiter(char c)
        {
            return char.IsWhiteSpace(c) || c is ';' or '{' or '}' or '=' or '(' or ')' or '=' or ',' or '.' or '[' or ']' or '-' or '+' or ':';
        }

        private static bool TryReadSymbol(char c, int position, out Token token)
        {
            TokenType? type = c switch
            {
                ';' => TokenType.Semicolon,
                '{' => TokenType.LeftBrace,
                '}' => TokenType.RightBrace,
                '(' => TokenType.LeftBracket,
                ')' => TokenType.RightBracket,
                '=' => TokenType.Equals,
                ',' => TokenType.Comma,
                '.' => TokenType.FullStop,
                '[' => TokenType.LeftSquareBracket,
                ']' => TokenType.RightSquareBracket,
                '-' => TokenType.Hyphen,
                '+' => TokenType.Plus,
                ':' => TokenType.Colon,
                _ => null
            };

            if (type.HasValue)
            {
                token = new Token
                {
                    Type = type.Value,
                    Value = c.ToString(),
                    Position = position,
                    Length = 1
                };
                return true;
            }

            token = default!;
            return false;
        }

        private static Token ReadWord(string input, ref int i)
        {
            int start = i;

            while (i < input.Length)
            {
                char c = input[i];

                if (IsDelimiter(c))
                {
                    break;
                }

                if (c == '"' || c == '\'')
                {
                    throw new FormatException(
                        $"Unexpected quote at position {i}. " +
                        "Quoted arguments must be separate tokens."
                    );
                }

                i++;
            }

            return new Token
            {
                Type = TokenType.Word,
                Value = input.Substring(start, i - start),
                Position = start,
                Length = i - start
            };
        }

        private static Token ReadString(string input, ref int i)
        {
            int start = i;
            char quote = input[i];
            i++;

            var builder = new StringBuilder();
            bool closed = false;

            while (i < input.Length)
            {
                char c = input[i];

                if (c == quote)
                {
                    i++;
                    closed = true;
                    break;
                }

                if (c == '\\')
                {
                    i++;
                    if (i >= input.Length)
                    {
                        throw new FormatException(
                            $"Incomplete escape sequence at position {i - 1}."
                        );
                    }

                    char escaped = input[i++];
                    switch (escaped)
                    {
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        default: builder.Append(escaped); break;
                    }
                    continue;
                }

                builder.Append(c);
                i++;
            }

            if (!closed)
            {
                throw new FormatException(
                    $"Unterminated string beginning at position {start}."
                );
            }

            if (i < input.Length && !IsDelimiter(input[i]))
            {
                throw new FormatException(
                    $"Unexpected character after quoted string at position {i}."
                );
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
