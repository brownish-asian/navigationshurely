using navigationshurely.models;
using navigationshurely.terminal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace navigationshurely.services
{
    public class TerminalCompleter
    {
        private readonly Librarian _librarian;
        private readonly Tokeniser _tokeniser = new();

        private static readonly string[] Commands =
        {
            "exit",
            "quit",
            "help",
            "man",
            "clear",
            "cls",
            "history",
            "whoami",

            "pwd",
            "ls",
            "cd",
            "tree",
            "find",

            "mkdir",
            "rmdir",
            "touch",
            "vim",
            "cat",
            "rm",
            "cp",
            "mv",
            "stat",

            "echo",
            "grep",
            "head",
            "tail",
            "wc",

            "basename",
            "dirname",

            "profiles",
            "mkprofile",
            "useprofile"
        };

        private static readonly Dictionary<string, string[]> Options = new()
        {
            ["ls"] = new[]
            {
                "-a", "-l", "--all", "--long"
            },

            ["mkdir"] = new[]
            {
                "-p", "--parents"
            },

            ["rm"] = new[]
            {
                "-r", "-R", "-f", "--recursive", "--force"
            },

            ["cp"] = new[]
            {
                "-r", "-R", "--recursive"
            },

            ["find"] = new[]
            {
                "-name", "-iname"
            },

            ["grep"] = new[]
            {
                "-i", "-n"
            },

            ["head"] = new[]
            {
                "-n", "--lines"
            },

            ["tail"] = new[]
            {
                "-n", "--lines"
            },

            ["wc"] = new[]
            {
                "-l", "-w", "-c", "-m"
            }
        };

        public TerminalCompleter(Librarian librarian)
        {
            _librarian = librarian ??
                throw new ArgumentNullException(nameof(librarian));
        }

        // ----------------------------------------------------
        // MAIN COMPLETION ENTRY POINT
        // ----------------------------------------------------

        public IReadOnlyList<string> GetCompletions(
            string input,
            int tokenStart,
            int cursorPosition)
        {
            if (string.IsNullOrEmpty(input))
                input = string.Empty;

            tokenStart = Math.Max(
                0,
                Math.Min(tokenStart, input.Length));

            cursorPosition = Math.Max(
                tokenStart,
                Math.Min(cursorPosition, input.Length));

            // Only parse the command text before the current token.
            // This also allows completion inside an unfinished quote.
            List<Token> previousTokens;

            try
            {
                previousTokens = _tokeniser.ParseToken(
                    input[..tokenStart]);

                previousTokens = previousTokens
                    .Where(token =>
                        token.Type != TokenType.EndOfInput)
                    .ToList();
            }
            catch (FormatException)
            {
                return Array.Empty<string>();
            }

            // Commands separated by semicolons are completed independently.
            int lastSemicolon = previousTokens.FindLastIndex(
                token => token.Type == TokenType.Semicolon);

            List<Token> currentCommand = previousTokens
                .Skip(lastSemicolon + 1)
                .ToList();

            string rawPrefix = input.Substring(
                tokenStart,
                cursorPosition - tokenStart);

            (string prefix, char quote) = ExtractPrefix(rawPrefix);

            // Empty command position, or immediately after a semicolon:
            // complete command names.
            if (currentCommand.Count == 0)
            {
                return Commands
                    .Where(command =>
                        command.StartsWith(
                            prefix,
                            StringComparison.OrdinalIgnoreCase))
                    .Select(command => FormatCandidate(command, quote))
                    .ToList();
            }

            // If we are completing the destination of > or >>,
            // always offer filesystem paths.
            Token lastToken = currentCommand[^1];

            if (lastToken.Type is
                TokenType.OutputRedirect or
                TokenType.AppendRedirect)
            {
                return CompletePaths(prefix, quote);
            }

            if (currentCommand[0].Type != TokenType.Word)
                return Array.Empty<string>();

            string commandName =
                currentCommand[0].Value.ToLowerInvariant();

            // Complete profile names after useprofile.
            if (commandName == "useprofile" &&
                currentCommand.Count == 1)
            {
                return _librarian
                    .CompleteProfiles(prefix)
                    .Select(profile => FormatCandidate(profile, quote))
                    .ToList();
            }

            // Complete command options when the current word starts with '-'.
            if (prefix.StartsWith("-", StringComparison.Ordinal) &&
                Options.TryGetValue(commandName, out string[]? options))
            {
                return options
                    .Where(option =>
                        option.StartsWith(
                            prefix,
                            StringComparison.OrdinalIgnoreCase))
                    .Select(option => FormatCandidate(option, quote))
                    .ToList();
            }

            // Otherwise, complete virtual filesystem paths.
            return CompletePaths(prefix, quote);
        }

        // ----------------------------------------------------
        // PATH COMPLETION
        // ----------------------------------------------------

        private IReadOnlyList<string> CompletePaths(
            string prefix,
            char quote)
        {
            return _librarian
                .CompletePaths(prefix)
                .Select(path => FormatCandidate(path, quote))
                .ToList();
        }

        // ----------------------------------------------------
        // QUOTED INPUT
        // ----------------------------------------------------

        private static (string Prefix, char Quote) ExtractPrefix(
            string rawPrefix)
        {
            char quote = '\0';

            if (rawPrefix.Length > 0 &&
                rawPrefix[0] is '"' or '\'')
            {
                quote = rawPrefix[0];
                rawPrefix = rawPrefix[1..];

                // Remove a closing quote, if one has already been typed.
                if (rawPrefix.Length > 0 &&
                    rawPrefix[^1] == quote)
                {
                    rawPrefix = rawPrefix[..^1];
                }
            }

            // Undo the basic escapes used by the tokeniser.
            rawPrefix = rawPrefix
                .Replace("\\\\", "\\")
                .Replace("\\ ", " ");

            return (rawPrefix, quote);
        }

        private static string FormatCandidate(
            string value,
            char quote)
        {
            // Preserve an existing quote style.
            if (quote != '\0')
            {
                return quote +
                    EscapeForQuote(value, quote) +
                    quote;
            }

            // Automatically quote names containing spaces or shell
            // structural characters.
            if (value.Any(char.IsWhiteSpace) ||
                value.Contains(';') ||
                value.Contains('>') ||
                value.StartsWith("#", StringComparison.Ordinal))
            {
                return "\"" + EscapeForQuote(value, '"') + "\"";
            }

            return value;
        }

        private static string EscapeForQuote(
            string value,
            char quote)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace(
                    quote.ToString(),
                    "\\" + quote);
        }
    }
}