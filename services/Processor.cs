using navigationshurely.models;
using navigationshurely.terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;

namespace navigationshurely.services
{
    public class Processor
    {
        private string _response = string.Empty;

        private readonly Librarian _librarian;
        private readonly Func<string> _historyProvider;
        private readonly ShiftManager _shiftManager;

        public event Action? ExitTerminal;
        public event Action? ClearTerminal;
        public event Action<string, string>? VimRequested;
        public event Action? FreeMoveRequested;

        private bool exitRequested;

        public Processor(
            Librarian librarian,
            Func<string>? historyProvider = null)
        {
            _librarian = librarian ??
                throw new ArgumentNullException(nameof(librarian));

            _historyProvider = historyProvider ?? (() => string.Empty);

            _shiftManager = new ShiftManager(_librarian);
        }

        public string Response()
        {
            string result = _response;
            _response = string.Empty;
            return result;
        }

        // ----------------------------------------------------
        // COMMAND PROCESSING
        // ----------------------------------------------------

        public void ProcessCommand(List<Token> pieceWiseCommand)
        {
            ArgumentNullException.ThrowIfNull(pieceWiseCommand);

            _response = string.Empty;
            exitRequested = false;

            var statement = new List<Token>();

            foreach (Token token in pieceWiseCommand)
            {
                if (token.Type == TokenType.EndOfInput)
                    break;

                if (token.Type == TokenType.Semicolon)
                {
                    ProcessStatement(statement);
                    statement.Clear();

                    if (exitRequested)
                        break;

                    continue;
                }

                statement.Add(token);
            }

            if (!exitRequested)
                ProcessStatement(statement);
        }

        private void ProcessStatement(List<Token> tokens)
        {
            if (tokens.Count == 0 || exitRequested)
                return;

            int redirectIndex = tokens.FindIndex(
                token => token.Type is
                    TokenType.OutputRedirect or
                    TokenType.AppendRedirect);

            if (redirectIndex >= 0)
            {
                if (redirectIndex == 0 ||
                    redirectIndex != tokens.Count - 2)
                {
                    AddResponse(
                        "Syntax error: redirection requires one destination filename.");
                    return;
                }

                Token destination = tokens[^1];

                if (destination.Type is not (TokenType.Word or TokenType.String))
                {
                    AddResponse("Syntax error: invalid redirection destination.");
                    return;
                }

                bool append =
                    tokens[redirectIndex].Type == TokenType.AppendRedirect;

                var commandTokens = tokens
                    .Take(redirectIndex)
                    .ToList();

                string output = ExecuteSimple(commandTokens);

                if (exitRequested)
                    return;

                string error = _librarian.WriteFile(
                    destination.Value,
                    output,
                    append);

                AddResponse(error);
                return;
            }

            AddResponse(ExecuteSimple(tokens));
        }

        private string ExecuteSimple(List<Token> tokens)
        {
            if (tokens.Count == 0)
                return string.Empty;

            if (tokens[0].Type != TokenType.Word)
                return "Invalid command.";

            foreach (Token token in tokens)
            {
                if (token.Type is not (TokenType.Word or TokenType.String))
                    return "Invalid command syntax.";
            }

            string command = tokens[0].Value;

            List<string> args = tokens
                .Skip(1)
                .Select(token => token.Value)
                .ToList();

            switch (command)
            {
                // ------------------------------------------------
                // TERMINAL CONTROL
                // ------------------------------------------------

                case "exit":
                case "quit":
                    exitRequested = true;
                    ExitTerminal?.Invoke();
                    return string.Empty;

                case "clear":
                case "cls":
                    ClearTerminal?.Invoke();
                    return string.Empty;

                case "help":
                case "--help":
                    return GetHelp(args);

                case "man":
                    return GetManual(args);

                case "history":
                    return _historyProvider();

                case "whoami":
                    return args.Count == 0
                        ? "admin"
                        : "whoami: too many arguments.";

                case "freemove":
                    if (args.Count != 0)
                        return "Usage: freemove";

                    FreeMoveRequested?.Invoke();
                    return "Entering free-move layout editor...";

                // ------------------------------------------------
                // PROFILE MANAGEMENT
                // ------------------------------------------------

                case "profiles":
                    return args.Count == 0
                        ? _librarian.ListProfiles()
                        : "Usage: profiles";

                case "mkprofile":
                    return args.Count == 1
                        ? _librarian.CreateProfile(args[0])
                        : "Usage: mkprofile [profile_name]";

                case "useprofile":
                    return args.Count == 1
                        ? _librarian.SelectProfile(args[0])
                        : "Usage: useprofile [profile_name]";

                case "pstn":
                    return ExecutePosition(args, relative: false);

                case "shift":
                    return ExecutePosition(args, relative: true);


                // ------------------------------------------------
                // DIRECTORY NAVIGATION
                // ------------------------------------------------

                case "pwd":
                    return args.Count == 0
                        ? _librarian.WorkingDirectory()
                        : "Usage: pwd";

                case "cd":
                    if (args.Count > 1)
                        return "Usage: cd [directory]";

                    return _librarian.ChangeDirectory(
                        args.Count == 0 ? null : args[0]);

                // ------------------------------------------------
                // DIRECTORY LISTING
                // ------------------------------------------------

                case "ls":
                    return ExecuteLs(args);

                case "tree":
                    return args.Count <= 1
                        ? _librarian.Tree(
                            args.Count == 0 ? "." : args[0])
                        : "Usage: tree [directory]";

                case "find":
                    return ExecuteFind(args);

                // ------------------------------------------------
                // DIRECTORY CREATION AND REMOVAL
                // ------------------------------------------------

                case "mkdir":
                    return ExecuteMkdir(args);

                case "rmdir":
                    if (args.Count == 0)
                        return "Usage: rmdir [directory]";

                    return ExecuteMultiple(
                        args,
                        path => _librarian.RemoveDirectory(path));

                // ------------------------------------------------
                // FILE CREATION AND REMOVAL
                // ------------------------------------------------

                case "touch":
                    if (args.Count == 0)
                        return "Usage: touch [filename]";

                    return ExecuteMultiple(
                        args,
                        path => _librarian.Touch(path));

                case "rm":
                    return ExecuteRm(args);

                // ------------------------------------------------
                // READING AND WRITING
                // ------------------------------------------------

                case "cat":
                    return ExecuteCat(args);
                case "vim":
                    return ExecuteVim(args);

                case "echo":
                    return string.Join(" ", args);

                // ------------------------------------------------
                // COPYING AND MOVING
                // ------------------------------------------------

                case "cp":
                    return ExecuteCp(args);

                case "mv":
                    return args.Count == 2
                        ? _librarian.Move(args[0], args[1])
                        : "Usage: mv [source] [destination]";

                // ------------------------------------------------
                // FILE INFORMATION
                // ------------------------------------------------

                case "stat":
                    return args.Count == 1
                        ? _librarian.Stat(args[0])
                        : "Usage: stat [path]";

                case "basename":
                    return ExecuteBasename(args);

                case "dirname":
                    return ExecuteDirname(args);

                // ------------------------------------------------
                // TEXT UTILITIES
                // ------------------------------------------------

                case "grep":
                    return ExecuteGrep(args);

                case "head":
                    return ExecuteHeadTail(args, false);

                case "tail":
                    return ExecuteHeadTail(args, true);

                case "wc":
                    return ExecuteWc(args);

                default:
                    return $"{command}: command not found.";
            }
        }

        // ----------------------------------------------------
        // LS
        // ----------------------------------------------------

        // ----------------------------------------------------
        // CONTROL POSITIONING
        // ----------------------------------------------------

        private string ExecutePosition(
            List<string> args,
            bool relative)
        {
            string commandName = relative ? "shift" : "pstn";

            string usage = relative
                ? "Usage: shift <control> [--dx <deltaX>] [--dy <deltaY>]"
                : "Usage: pstn <control> [--x <left>] [--y <top>]";

            if (args.Count < 3)
                return usage;

            if (!TryParseControl(args[0], out Controls control))
            {
                return
                    $"{commandName}: '{args[0]}' is not a recognised control.";
            }

            string horizontalOption = relative ? "--dx" : "--x";
            string verticalOption = relative ? "--dy" : "--y";

            double? horizontal = null;
            double? vertical = null;

            // Parse and validate all arguments before making any changes.
            for (int i = 1; i < args.Count; i++)
            {
                string option = args[i];

                bool isHorizontal = string.Equals(
                    option,
                    horizontalOption,
                    StringComparison.Ordinal);

                bool isVertical = string.Equals(
                    option,
                    verticalOption,
                    StringComparison.Ordinal);

                if (!isHorizontal && !isVertical)
                {
                    return
                        $"{commandName}: unknown option '{option}'.";
                }

                if (isHorizontal && horizontal.HasValue)
                {
                    return
                        $"{commandName}: option '{option}' was supplied more than once.";
                }

                if (isVertical && vertical.HasValue)
                {
                    return
                        $"{commandName}: option '{option}' was supplied more than once.";
                }

                if (i + 1 >= args.Count ||
                    args[i + 1].StartsWith(
                        "--",
                        StringComparison.Ordinal))
                {
                    return
                        $"{commandName}: missing value for '{option}'.";
                }

                string rawValue = args[++i];

                if (!TryParseFiniteDouble(rawValue, out double value))
                {
                    return
                        $"{commandName}: '{rawValue}' is not a valid finite coordinate.";
                }

                if (isHorizontal)
                    horizontal = value;
                else
                    vertical = value;
            }

            // At least one coordinate must have been provided.
            if (!horizontal.HasValue && !vertical.HasValue)
                return usage;

            // No configuration has been modified before this point.
            try
            {
                if (relative)
                {
                    _shiftManager.Shift(
                        control,
                        horizontal,
                        vertical);
                }
                else
                {
                    _shiftManager.SetPosition(
                        control,
                        horizontal,
                        vertical);
                }
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is InvalidOperationException)
            {
                return $"{commandName}: {ex.Message}";
            }

            return string.Empty;
        }

        private static bool TryParseControl(
            string value,
            out Controls control)
        {
            // Match actual enum names rather than allowing numeric enum values.
            string? matchingName = Enum.GetNames(typeof(Controls))
                .FirstOrDefault(name =>
                    string.Equals(
                        name,
                        value,
                        StringComparison.OrdinalIgnoreCase));

            if (matchingName == null)
            {
                control = default;
                return false;
            }

            control = (Controls)Enum.Parse(
                typeof(Controls),
                matchingName);

            return true;
        }

        private static bool TryParseFiniteDouble(
            string value,
            out double result)
        {
            return double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result) &&
                !double.IsNaN(result) &&
                !double.IsInfinity(result);
        }

        private string ExecuteLs(List<string> args)
        {
            bool showHidden = false;
            bool longFormat = false;

            var paths = new List<string>();
            bool optionsEnded = false;

            foreach (string arg in args)
            {
                if (!optionsEnded && arg == "--")
                {
                    optionsEnded = true;
                    continue;
                }

                if (!optionsEnded && arg.StartsWith("--"))
                {
                    switch (arg)
                    {
                        case "--all":
                            showHidden = true;
                            break;

                        case "--long":
                            longFormat = true;
                            break;

                        default:
                            return $"ls: unrecognised option '{arg}'.";
                    }

                    continue;
                }

                if (!optionsEnded && arg.StartsWith("-") && arg.Length > 1)
                {
                    foreach (char flag in arg[1..])
                    {
                        switch (flag)
                        {
                            case 'a':
                                showHidden = true;
                                break;

                            case 'l':
                                longFormat = true;
                                break;

                            default:
                                return $"ls: invalid option '-{flag}'.";
                        }
                    }

                    continue;
                }

                paths.Add(arg);
            }

            if (paths.Count == 0)
                return _librarian.List(".", showHidden, longFormat);

            var output = new List<string>();

            foreach (string path in paths)
            {
                string listing = _librarian.List(
                    path,
                    showHidden,
                    longFormat);

                if (paths.Count > 1)
                    output.Add($"{path}:\n{listing}");
                else
                    output.Add(listing);
            }

            return string.Join(Environment.NewLine, output);
        }

        // ----------------------------------------------------
        // MKDIR
        // ----------------------------------------------------

        private string ExecuteMkdir(List<string> args)
        {
            bool parents = false;
            var paths = new List<string>();

            foreach (string arg in args)
            {
                if (arg == "-p" || arg == "--parents")
                    parents = true;
                else
                    paths.Add(arg);
            }

            if (paths.Count == 0)
                return "Usage: mkdir [-p] [directory]";

            return ExecuteMultiple(
                paths,
                path => _librarian.CreateDirectory(path, parents));
        }

        // ----------------------------------------------------
        // RM
        // ----------------------------------------------------

        private string ExecuteRm(List<string> args)
        {
            bool recursive = false;
            bool force = false;

            var paths = new List<string>();

            foreach (string arg in args)
            {
                if (arg == "--recursive")
                {
                    recursive = true;
                    continue;
                }

                if (arg == "--force")
                {
                    force = true;
                    continue;
                }

                if (arg.StartsWith("-") && arg.Length > 1)
                {
                    foreach (char flag in arg[1..])
                    {
                        switch (flag)
                        {
                            case 'r':
                            case 'R':
                                recursive = true;
                                break;

                            case 'f':
                                force = true;
                                break;

                            default:
                                return $"rm: invalid option '-{flag}'.";
                        }
                    }

                    continue;
                }

                paths.Add(arg);
            }

            if (paths.Count == 0)
                return "Usage: rm [-rf] [path]";

            return ExecuteMultiple(
                paths,
                path => _librarian.Remove(path, recursive, force));
        }

        // ----------------------------------------------------
        // CP
        // ----------------------------------------------------

        private string ExecuteCp(List<string> args)
        {
            bool recursive = false;

            var paths = new List<string>();

            foreach (string arg in args)
            {
                if (arg is "-r" or "-R" or "--recursive")
                {
                    recursive = true;
                    continue;
                }

                paths.Add(arg);
            }

            if (paths.Count != 2)
                return "Usage: cp [-r] [source] [destination]";

            return _librarian.Copy(
                paths[0],
                paths[1],
                recursive);
        }

        // ----------------------------------------------------
        // CAT
        // ----------------------------------------------------

        private string ExecuteCat(List<string> args)
        {
            if (args.Count == 0)
                return "Usage: cat [file]";

            var output = new List<string>();

            foreach (string path in args)
            {
                if (!_librarian.TryReadFile(
                    path,
                    out string content,
                    out string error))
                {
                    output.Add(error);
                    continue;
                }

                output.Add(content);
            }

            return string.Join(Environment.NewLine, output);
        }


        private string ExecuteVim(List<string> args)
        {
            if (args.Count != 1)
                return "Usage: vim [filename]";

            string path = args[0];

            if (!_librarian.TryOpenForEditing(
                path,
                out string content,
                out string error))
            {
                return error;
            }

            if (VimRequested == null)
                return "vim: editor interface is unavailable.";

            VimRequested.Invoke(path, content);

            return string.Empty;
        }

        // ----------------------------------------------------
        // FIND
        // ----------------------------------------------------

        private string ExecuteFind(List<string> args)
        {
            string path = ".";
            string pattern = "*";

            bool pathProvided = false;

            for (int i = 0; i < args.Count; i++)
            {
                if (args[i] is "-name" or "-iname")
                {
                    if (i + 1 >= args.Count)
                        return "find: missing search pattern.";

                    pattern = args[++i];
                }
                else if (args[i].StartsWith("-"))
                {
                    return $"find: unsupported option '{args[i]}'.";
                }
                else if (!pathProvided)
                {
                    path = args[i];
                    pathProvided = true;
                }
                else
                {
                    return "Usage: find [path] [-name pattern]";
                }
            }

            return _librarian.Find(path, pattern);
        }

        // ----------------------------------------------------
        // BASENAME
        // ----------------------------------------------------

        private string ExecuteBasename(List<string> args)
        {
            if (args.Count != 1)
                return "Usage: basename [path]";

            string normalized = args[0].Replace('\\', '/').TrimEnd('/');

            if (normalized.Length == 0)
                return "/";

            return normalized.Split('/').Last();
        }

        // ----------------------------------------------------
        // DIRNAME
        // ----------------------------------------------------

        private string ExecuteDirname(List<string> args)
        {
            if (args.Count != 1)
                return "Usage: dirname [path]";

            string normalized = args[0].Replace('\\', '/').TrimEnd('/');

            if (normalized.Length == 0)
                return "/";

            int separator = normalized.LastIndexOf('/');

            if (separator < 0)
                return ".";

            if (separator == 0)
                return "/";

            return normalized[..separator];
        }

        // ----------------------------------------------------
        // GREP
        // ----------------------------------------------------

        private string ExecuteGrep(List<string> args)
        {
            bool ignoreCase = false;
            bool showLineNumbers = false;

            int index = 0;

            while (index < args.Count && args[index].StartsWith("-"))
            {
                string option = args[index++];

                if (option == "--")
                    break;

                foreach (char flag in option[1..])
                {
                    switch (flag)
                    {
                        case 'i':
                            ignoreCase = true;
                            break;

                        case 'n':
                            showLineNumbers = true;
                            break;

                        default:
                            return $"grep: invalid option '-{flag}'.";
                    }
                }
            }

            if (args.Count - index < 2)
                return "Usage: grep [-in] [pattern] [file]";

            string pattern = args[index++];

            var output = new List<string>();

            foreach (string path in args.Skip(index))
            {
                if (!_librarian.TryReadFile(
                    path,
                    out string content,
                    out string error))
                {
                    output.Add(error);
                    continue;
                }

                string[] lines = content.Split('\n');

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].TrimEnd('\r');

                    StringComparison comparison = ignoreCase
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal;

                    if (line.Contains(pattern, comparison))
                    {
                        string prefix = showLineNumbers
                            ? $"{i + 1}:"
                            : string.Empty;

                        output.Add(prefix + line);
                    }
                }
            }

            return string.Join(Environment.NewLine, output);
        }

        // ----------------------------------------------------
        // HEAD AND TAIL
        // ----------------------------------------------------

        private string ExecuteHeadTail(
            List<string> args,
            bool takeLast)
        {
            int count = 10;
            string? path = null;

            for (int i = 0; i < args.Count; i++)
            {
                if (args[i] is "-n" or "--lines")
                {
                    if (++i >= args.Count ||
                        !int.TryParse(args[i], out count) ||
                        count < 0)
                    {
                        return "Invalid line count.";
                    }
                }
                else if (args[i].StartsWith("-") &&
                         int.TryParse(args[i][1..], out int shortCount))
                {
                    if (shortCount < 0)
                        return "Invalid line count.";

                    count = shortCount;
                }
                else if (path == null)
                {
                    path = args[i];
                }
                else
                {
                    return "Only one filename is supported.";
                }
            }

            if (path == null)
                return takeLast
                    ? "Usage: tail [-n number] [file]"
                    : "Usage: head [-n number] [file]";

            if (!_librarian.TryReadFile(
                path,
                out string content,
                out string error))
            {
                return error;
            }

            string[] lines = content.Split('\n');

            if (lines.Length > 0 && lines[^1].Length == 0)
                lines = lines[..^1];

            IEnumerable<string> selected = takeLast
                ? lines.TakeLast(count)
                : lines.Take(count);

            return string.Join(Environment.NewLine, selected)
                .Replace("\r", "");
        }

        // ----------------------------------------------------
        // WC
        // ----------------------------------------------------

        private string ExecuteWc(List<string> args)
        {
            bool showLines = false;
            bool showWords = false;
            bool showCharacters = false;

            var paths = new List<string>();

            foreach (string arg in args)
            {
                if (arg.StartsWith("-") && arg.Length > 1)
                {
                    foreach (char flag in arg[1..])
                    {
                        switch (flag)
                        {
                            case 'l':
                                showLines = true;
                                break;

                            case 'w':
                                showWords = true;
                                break;

                            case 'c':
                            case 'm':
                                showCharacters = true;
                                break;

                            default:
                                return $"wc: invalid option '-{flag}'.";
                        }
                    }
                }
                else
                {
                    paths.Add(arg);
                }
            }

            if (paths.Count == 0)
                return "Usage: wc [-lwm] [file]";

            if (!showLines && !showWords && !showCharacters)
            {
                showLines = true;
                showWords = true;
                showCharacters = true;
            }

            var output = new List<string>();

            foreach (string path in paths)
            {
                if (!_librarian.TryReadFile(
                    path,
                    out string content,
                    out string error))
                {
                    output.Add(error);
                    continue;
                }

                int lines = content.Count(c => c == '\n');

                if (content.Length > 0 && !content.EndsWith("\n"))
                    lines++;

                int words = Regex.Matches(content, @"\S+").Count;
                int characters = content.Length;

                var values = new List<string>();

                if (showLines)
                    values.Add(lines.ToString());

                if (showWords)
                    values.Add(words.ToString());

                if (showCharacters)
                    values.Add(characters.ToString());

                values.Add(path);

                output.Add(string.Join("\t", values));
            }

            return string.Join(Environment.NewLine, output);
        }

        // ----------------------------------------------------
        // MULTIPLE PATHS
        // ----------------------------------------------------

        private static string ExecuteMultiple(
            IEnumerable<string> paths,
            Func<string, string> operation)
        {
            var errors = new List<string>();

            foreach (string path in paths)
            {
                string response = operation(path);

                if (!string.IsNullOrWhiteSpace(response))
                    errors.Add(response);
            }

            return string.Join(Environment.NewLine, errors);
        }

        // ----------------------------------------------------
        // HELP AND MANUAL
        // ----------------------------------------------------

        private string GetHelp(List<string> args)
        {
            if (args.Count == 1)
                return GetManual(args);

            return
                "NavigationShurely Terminal\n\n" +

                "Navigation:\n" +
                "  pwd                 Print working directory\n" +
                "  ls [-la] [path]     List directory contents\n" +
                "  cd [path]           Change directory\n" +
                "  tree [path]         Display directory tree\n" +
                "  find [path] -name X Search for an item\n\n" +

                "Files and directories:\n" +
                "  mkdir [-p] path     Create directory\n" +
                "  rmdir path          Remove empty directory\n" +
                "  touch file          Create file\n" +
                "  cat file            Display file contents\n" +
                "  rm [-rf] path       Remove file/directory\n" +
                "  cp [-r] src dest    Copy file/directory\n" +
                "  mv src dest         Move or rename item\n" +
                "  stat path           Display item information\n\n" +

                "Text utilities:\n" +
                "  echo text           Print text\n" +
                "  grep [-in] p file   Search file contents\n" +
                "  head [-n N] file    Display first lines\n" +
                "  tail [-n N] file    Display last lines\n" +
                "  wc [-lwm] file      Count lines, words, characters\n\n" +
                "  vim file            Edit file using Vim-style commands\n" +

                "Profiles:\n" +
                "  profiles            List profiles\n" +
                "  mkprofile name      Create profile\n" +
                "  useprofile name     Select active profile\n\n" +

                "Control positioning:\n" +
"  pstn control [--x L] [--y T] Set absolute position\n" +
"  shift control [--dx X] [--dy Y] Move relative to current position\n\n" +

                "Elevated layout editing:\n" +
"  freemove            Enter interactive layout editing\n\n" +

                "Other:\n" +
                "  history             Display command history\n" +
                "  whoami              Display current user\n" +
                "  clear               Clear terminal\n" +
                "  help                Display this manual\n" +
                "  man command         Display command information\n" +
                "  exit                Exit terminal\n\n" +

                "Special syntax:\n" +
                "  ~                   Active profile directory\n" +
                "  /                   Filesystem root\n" +
                "  .                   Current directory\n" +
                "  ..                  Parent directory\n" +
                "  ;                   Separate commands\n" +
                "  >                   Write command output to file\n" +
                "  >>                  Append command output to file\n";
        }

        private string GetManual(List<string> args)
        {
            if (args.Count != 1)
                return GetHelp(new List<string>());

            string command = args[0];

            return command switch
            {
                "cd" => "cd [directory]\nChanges the current working directory. Without an argument, changes to the active profile.",

                "ls" => "ls [-la] [path]\nLists directory contents. -a includes hidden items. -l uses long format.",

                "mkdir" => "mkdir [-p] [directory]\nCreates a directory. -p creates missing parent directories.",

                "rm" => "rm [-rf] [path]\nRemoves files. -r allows recursive directory removal. -f ignores missing paths.",

                "cp" => "cp [-r] [source] [destination]\nCopies files. -r enables directory copying.",

                "mv" => "mv [source] [destination]\nMoves or renames files and directories.",

                "touch" => "touch [file]\nCreates an empty text file.",

                "vim" =>
    "vim [filename]\n" +
    "Opens an existing file or a new temporary buffer.\n\n" +
    "Modes:\n" +
    "  Normal       Navigate and manipulate text.\n" +
    "  Insert       Insert or remove text.\n" +
    "  Command      Execute editor commands.\n\n" +
    "Normal mode:\n" +
    "  h j k l      Move left, down, up and right.\n" +
    "  w b e        Word motions.\n" +
    "  0 ^ $        Line motions.\n" +
    "  gg G         First and last line.\n" +
    "  d c y        Delete, change and yank.\n" +
    "  iw aw        Inner and around word.\n" +
    "  i I a A      Insert and append.\n" +
    "  o O          Open a line below or above.\n" +
    "  x p P        Delete and paste.\n" +
    "  u Ctrl+R     Undo and redo.\n\n" +
    "Colon commands:\n" +
    "  :w           Save.\n" +
    "  :wq          Save and quit.\n" +
    "  :q           Quit if there are no unsaved changes.\n" +
    "  :q!          Quit without saving.",


                "pstn" =>
    "pstn <control> [--x <left>] [--y <top>]\n" +
    "Sets the absolute position of a control in the active profile.\n" +
    "Use --x to set Left and --y to set Top.\n" +
    "At least one coordinate is required. Unspecified coordinates remain unchanged.",

                "shift" =>
                    "shift <control> [--dx <deltaX>] [--dy <deltaY>]\n" +
                    "Moves a control relative to its current position in the active profile.\n" +
                    "Positive dx moves right. Positive dy moves down.\n" +
                    "At least one shift value is required. Unspecified axes remain unchanged.",

                "freemove" =>
                    "freemove\n" +
                    "Enters the interactive layout editor. This elevated command is available only in the admin terminal.\n" +
                    "The admin terminal is temporarily hidden while the main interface is edited. Press Esc to leave the editor and restore the terminal.",

                "cat" => "cat [file]\nDisplays file contents.",

                "pwd" => "pwd\nDisplays the absolute current directory.",

                "tree" => "tree [path]\nDisplays a recursive directory tree.",

                "find" => "find [path] [-name pattern]\nFinds directory entries using * and ? wildcards.",

                "grep" => "grep [-in] [pattern] [file]\nSearches for text in a file. -i ignores case. -n displays line numbers.",

                "head" => "head [-n number] [file]\nDisplays the first lines of a file.",

                "tail" => "tail [-n number] [file]\nDisplays the last lines of a file.",

                "wc" => "wc [-lwm] [file]\nCounts lines, words, and characters.",

                "profiles" => "profiles\nLists all available profiles and identifies the active profile.",

                "mkprofile" => "mkprofile [name]\nCreates a profile with a configuration for every Controls enum member.",

                "useprofile" => "useprofile [name]\nSelects a profile and changes the working directory to it.",

                "history" => "history\nDisplays previously entered commands.",

                "clear" => "clear\nClears the terminal transcript without deleting command history.",

                _ => $"No manual entry for '{command}'."
            };
        }

        private void AddResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return;

            if (!string.IsNullOrEmpty(_response))
                _response += Environment.NewLine;

            _response += response;
        }
    }
}