
using System;
using System.Collections.Generic;

namespace navigationshurely.terminal
{
    public enum VimMode
    {
        Normal,
        Insert,
        CommandLine
    }

    public sealed class VimEditorSession
    {
        private sealed class Snapshot
        {
            public string Text { get; }
            public int Cursor { get; }

            public Snapshot(string text, int cursor)
            {
                Text = text;
                Cursor = cursor;
            }
        }

        private readonly Func<string, string> _writeFile;

        private readonly Stack<Snapshot> _undo = new();
        private readonly Stack<Snapshot> _redo = new();

        private Snapshot? _insertOrigin;

        private string _savedText;
        private string _register = string.Empty;

        private bool _registerLinewise;

        private int _count;
        private int _operatorCount = 1;

        private char _pendingOperator;
        private char _pendingTextObject;
        private bool _pendingG;

        public string FilePath { get; }

        public string Text { get; private set; }

        public int CursorPosition { get; private set; }

        public VimMode Mode { get; private set; }

        public string StatusMessage { get; private set; } = string.Empty;

        public bool IsModified =>
            !string.Equals(Text, _savedText, StringComparison.Ordinal);

        public int LineNumber =>
            GetLineIndex(CursorPosition) + 1;

        public int ColumnNumber =>
            CursorPosition - GetLineStart(CursorPosition) + 1;

        public VimEditorSession(
            string filePath,
            string content,
            Func<string, string> writeFile)
        {
            FilePath = filePath ??
                throw new ArgumentNullException(nameof(filePath));

            _writeFile = writeFile ??
                throw new ArgumentNullException(nameof(writeFile));

            Text = Normalize(content ?? string.Empty);

            _savedText = Text;

            CursorPosition = 0;

            Mode = VimMode.Normal;

            ClampNormalCursor();
        }

        // =====================================================
        // NORMAL MODE
        // =====================================================

        public bool HandleNormalCharacter(char key)
        {
            if (Mode != VimMode.Normal)
                return false;

            // -------------------------------------------------
            // TEXT OBJECT COMPLETION
            // -------------------------------------------------

            if (_pendingTextObject != '\0')
            {
                char prefix = _pendingTextObject;
                char operation = _pendingOperator;

                ClearPending();

                if (TryGetTextObjectRange(
                    prefix,
                    key,
                    out int start,
                    out int end))
                {
                    ApplyRangeOperator(operation, start, end);
                }
                else
                {
                    StatusMessage = "Invalid text object.";
                }

                return true;
            }

            // -------------------------------------------------
            // GG COMPLETION
            // -------------------------------------------------

            if (_pendingG)
            {
                _pendingG = false;

                if (key != 'g')
                {
                    ClearPending();
                    StatusMessage = "Expected another g.";
                    return true;
                }

                bool hasCount = _count > 0;
                int requestedLine = _count;

                _count = 0;

                int targetLine = hasCount
                    ? requestedLine - 1
                    : 0;

                if (_pendingOperator != '\0')
                {
                    char operation = _pendingOperator;

                    int currentLine = GetLineIndex(CursorPosition);

                    ClearPending();

                    ApplyLineOperator(
                        operation,
                        currentLine,
                        targetLine);
                }
                else
                {
                    ClearPending();
                    MoveToLine(targetLine);
                }

                return true;
            }

            // -------------------------------------------------
            // OPERATOR PENDING
            // -------------------------------------------------

            if (_pendingOperator != '\0')
            {
                if (IsDigit(key) &&
                    (key != '0' || _count > 0))
                {
                    AddCountDigit(key);
                    return true;
                }

                char operation = _pendingOperator;

                // dd, cc, yy
                if (key == operation)
                {
                    int suffixCount = TakeCount(out _);

                    int total = Math.Max(
                        1,
                        _operatorCount * suffixCount);

                    int currentLine = GetLineIndex(CursorPosition);

                    ClearPending();

                    ApplyLineOperator(
                        operation,
                        currentLine,
                        currentLine + total - 1);

                    return true;
                }

                // iw, aw, i(, a(, i", etc.
                if (key is 'i' or 'a')
                {
                    _pendingTextObject = key;
                    return true;
                }

                // dgg, cgg, ygg
                if (key == 'g')
                {
                    _pendingG = true;
                    return true;
                }

                // dG, cG, yG
                if (key == 'G')
                {
                    bool hasCount = _count > 0;
                    int requestedLine = _count;

                    int targetLine = hasCount
                        ? requestedLine - 1
                        : GetLastLineIndex();

                    int currentLine = GetLineIndex(CursorPosition);

                    ClearPending();

                    ApplyLineOperator(
                        operation,
                        currentLine,
                        targetLine);

                    return true;
                }

                int suffix = TakeCount(out _);

                int totalCount = Math.Max(
                    1,
                    _operatorCount * suffix);

                int current = GetLineIndex(CursorPosition);

                ClearPending();

                // Vertical operator motions are linewise.
                if (key is 'j' or 'k')
                {
                    int target = key == 'j'
                        ? current + totalCount
                        : current - totalCount;

                    ApplyLineOperator(
                        operation,
                        current,
                        target);

                    return true;
                }

                if (TryGetMotionRange(
                    key,
                    totalCount,
                    out int rangeStart,
                    out int rangeEnd))
                {
                    ApplyRangeOperator(
                        operation,
                        rangeStart,
                        rangeEnd);
                }
                else
                {
                    StatusMessage = "Invalid operator motion.";
                }

                return true;
            }

            // -------------------------------------------------
            // NUMERIC COUNTS
            // -------------------------------------------------

            if (IsDigit(key) && (key != '0' || _count > 0))
            {
                AddCountDigit(key);
                return true;
            }

            int count = TakeCount(out bool hasExplicitCount);

            // -------------------------------------------------
            // MOTIONS AND COMMANDS
            // -------------------------------------------------

            switch (key)
            {
                case 'h':
                    MoveLeft(count);
                    break;

                case 'j':
                    MoveVertical(count);
                    break;

                case 'k':
                    MoveVertical(-count);
                    break;

                case 'l':
                    MoveRight(count);
                    break;

                case 'w':
                    CursorPosition = MoveWordForward(
                        CursorPosition,
                        count);

                    ClampNormalCursor();
                    break;

                case 'b':
                    CursorPosition = MoveWordBackward(
                        CursorPosition,
                        count);

                    ClampNormalCursor();
                    break;

                case 'e':
                    CursorPosition = MoveWordEnd(
                        CursorPosition,
                        count);

                    ClampNormalCursor();
                    break;

                case '0':
                    CursorPosition = GetLineStart(CursorPosition);
                    break;

                case '^':
                    CursorPosition = GetFirstNonWhitespace(CursorPosition);
                    ClampNormalCursor();
                    break;

                case '$':
                    MoveToLineEnd();
                    break;

                case 'g':
                    _count = hasExplicitCount ? count : 0;
                    _pendingG = true;
                    break;

                case 'G':
                    MoveToLine(
                        hasExplicitCount
                            ? count - 1
                            : GetLastLineIndex());
                    break;

                // ---------------------------------------------
                // OPERATORS
                // ---------------------------------------------

                case 'd':
                case 'c':
                case 'y':
                    _pendingOperator = key;
                    _operatorCount = count;
                    break;

                // ---------------------------------------------
                // INSERTION
                // ---------------------------------------------

                case 'i':
                    StartInsert(CursorPosition);
                    break;

                case 'I':
                    StartInsert(
                        GetFirstNonWhitespace(CursorPosition));
                    break;

                case 'a':
                    {
                        int lineEnd = GetLineEnd(CursorPosition);

                        int position = lineEnd == GetLineStart(CursorPosition)
                            ? CursorPosition
                            : Math.Min(CursorPosition + 1, lineEnd);

                        StartInsert(position);
                        break;
                    }

                case 'A':
                    StartInsert(GetLineEnd(CursorPosition));
                    break;

                case 'o':
                    OpenLine(below: true);
                    break;

                case 'O':
                    OpenLine(below: false);
                    break;

                // ---------------------------------------------
                // DELETION
                // ---------------------------------------------

                case 'x':
                    DeleteUnderCursor(count);
                    break;

                // ---------------------------------------------
                // PASTING
                // ---------------------------------------------

                case 'p':
                    Paste(afterCursor: true, count);
                    break;

                case 'P':
                    Paste(afterCursor: false, count);
                    break;

                // ---------------------------------------------
                // UNDO / REDO
                // ---------------------------------------------

                case 'u':
                    for (int i = 0; i < count; i++)
                        Undo();

                    break;

                // ---------------------------------------------
                // COMMAND LINE
                // ---------------------------------------------

                case ':':
                    BeginCommandLine();
                    break;
            }

            return true;
        }

        // =====================================================
        // INSERT MODE
        // =====================================================

        private void StartInsert(int position)
        {
            StartInsert(position, Capture());
        }

        private void StartInsert(
            int position,
            Snapshot original)
        {
            _insertOrigin = original;

            CursorPosition = Math.Clamp(
                position,
                0,
                Text.Length);

            Mode = VimMode.Insert;

            ClearPending();

            StatusMessage = string.Empty;
        }

        public void InsertText(string value)
        {
            if (Mode != VimMode.Insert ||
                string.IsNullOrEmpty(value))
            {
                return;
            }

            value = Normalize(value);

            Text = Text.Insert(CursorPosition, value);

            CursorPosition += value.Length;

            StatusMessage = string.Empty;
        }

        public void InsertNewLine()
        {
            InsertText("\n");
        }

        public void InsertTab()
        {
            InsertText("\t");
        }

        public void InsertBackspace()
        {
            if (Mode != VimMode.Insert ||
                CursorPosition <= 0)
            {
                return;
            }

            Text = Text.Remove(CursorPosition - 1, 1);

            CursorPosition--;

            StatusMessage = string.Empty;
        }

        public void InsertDelete()
        {
            if (Mode != VimMode.Insert ||
                CursorPosition >= Text.Length)
            {
                return;
            }

            Text = Text.Remove(CursorPosition, 1);

            StatusMessage = string.Empty;
        }

        public void MoveInsertHorizontal(int direction)
        {
            if (Mode != VimMode.Insert)
                return;

            CursorPosition = Math.Clamp(
                CursorPosition + direction,
                0,
                Text.Length);
        }

        public void MoveInsertVertical(int direction)
        {
            if (Mode != VimMode.Insert)
                return;

            int currentLine = GetLineIndex(CursorPosition);
            int column = CursorPosition -
                GetLineStart(CursorPosition);

            int targetLine = Math.Clamp(
                currentLine + direction,
                0,
                GetLastLineIndex());

            int start = GetLineStartByNumber(targetLine);
            int end = GetLineEnd(start);

            CursorPosition = Math.Min(start + column, end);
        }

        public void MoveInsertToLineStart()
        {
            if (Mode == VimMode.Insert)
                CursorPosition = GetLineStart(CursorPosition);
        }

        public void MoveInsertToLineEnd()
        {
            if (Mode == VimMode.Insert)
                CursorPosition = GetLineEnd(CursorPosition);
        }

        private void EndInsert()
        {
            if (_insertOrigin != null &&
                Text != _insertOrigin.Text)
            {
                _undo.Push(_insertOrigin);
                _redo.Clear();

                // Normal mode points at the last inserted character.
                if (CursorPosition > 0 &&
                    Text[CursorPosition - 1] != '\n')
                {
                    CursorPosition--;
                }
            }

            _insertOrigin = null;

            Mode = VimMode.Normal;

            ClampNormalCursor();
        }

        private void OpenLine(bool below)
        {
            Snapshot before = Capture();

            if (below)
            {
                int end = GetLineEnd(CursorPosition);

                Text = Text.Insert(end, "\n");

                StartInsert(end + 1, before);
            }
            else
            {
                int start = GetLineStart(CursorPosition);

                Text = Text.Insert(start, "\n");

                StartInsert(start, before);
            }
        }

        // =====================================================
        // OPERATORS
        // =====================================================

        private void ApplyMotionOperator(
            char operation,
            char motion,
            int count)
        {
            int currentLine = GetLineIndex(CursorPosition);

            if (motion == 'j' || motion == 'k')
            {
                int target = motion == 'j'
                    ? currentLine + count
                    : currentLine - count;

                ApplyLineOperator(
                    operation,
                    currentLine,
                    target);

                return;
            }

            if (!TryGetMotionRange(
                motion,
                count,
                out int start,
                out int end))
            {
                StatusMessage = "Invalid operator motion.";
                return;
            }

            ApplyRangeOperator(operation, start, end);
        }

        private void ApplyRangeOperator(
            char operation,
            int start,
            int end)
        {
            start = Math.Clamp(start, 0, Text.Length);
            end = Math.Clamp(end, 0, Text.Length);

            if (end < start)
                (start, end) = (end, start);

            if (start == end)
            {
                StatusMessage = "Nothing to operate on.";
                return;
            }

            string selected = Text.Substring(
                start,
                end - start);

            if (operation == 'y')
            {
                _register = selected;
                _registerLinewise = false;

                StatusMessage = "Yanked.";
                return;
            }

            Snapshot before = Capture();

            Text = Text.Remove(start, end - start);

            CursorPosition = start;

            if (operation == 'c')
            {
                StartInsert(start, before);
                return;
            }

            FinishNormalEdit(before);
        }

        private void ApplyLineOperator(
            char operation,
            int firstLine,
            int lastLine)
        {
            firstLine = Math.Clamp(
                firstLine,
                0,
                GetLastLineIndex());

            lastLine = Math.Clamp(
                lastLine,
                0,
                GetLastLineIndex());

            int first = Math.Min(firstLine, lastLine);
            int last = Math.Max(firstLine, lastLine);

            int start = GetLineStartByNumber(first);

            int contentEnd = GetLineEnd(
                GetLineStartByNumber(last));

            int nextLineStart = last < GetLastLineIndex()
                ? GetLineStartByNumber(last + 1)
                : Text.Length;

            if (operation == 'y')
            {
                _register = Text.Substring(
                    start,
                    Math.Max(0, nextLineStart - start));

                if (!_register.EndsWith("\n", StringComparison.Ordinal))
                    _register += "\n";

                _registerLinewise = true;

                StatusMessage = "Lines yanked.";

                return;
            }

            Snapshot before = Capture();

            if (operation == 'c')
            {
                // Preserve the newline after the final selected line.
                Text = Text.Remove(
                    start,
                    Math.Max(0, contentEnd - start));

                CursorPosition = start;

                StartInsert(start, before);

                return;
            }

            int removeStart = start;
            int removeEnd = nextLineStart;

            // When deleting the final line, remove its preceding
            // newline so that an unnecessary empty line is not left.
            if (removeEnd == Text.Length && removeStart > 0)
                removeStart--;

            if (removeEnd > removeStart)
            {
                Text = Text.Remove(
                    removeStart,
                    removeEnd - removeStart);
            }

            CursorPosition = removeStart;

            FinishNormalEdit(before);
        }

        // =====================================================
        // MOTION RANGES
        // =====================================================

        private bool TryGetMotionRange(
            char motion,
            int count,
            out int start,
            out int end)
        {
            start = CursorPosition;
            end = CursorPosition;

            switch (motion)
            {
                case 'h':
                    start = MoveLeftPosition(count);
                    end = CursorPosition;
                    return start != end;

                case 'l':
                    start = CursorPosition;
                    end = MoveRightPosition(count);
                    return start != end;

                case 'w':
                    start = CursorPosition;
                    end = MoveWordForward(CursorPosition, count);
                    return start != end;

                case 'b':
                    start = MoveWordBackward(CursorPosition, count);
                    end = CursorPosition;
                    return start != end;

                case 'e':
                    start = CursorPosition;
                    end = Math.Min(
                        Text.Length,
                        MoveWordEnd(CursorPosition, count) + 1);

                    return start != end;

                case '$':
                    start = CursorPosition;
                    end = GetLineEnd(CursorPosition);
                    return start != end;

                case '0':
                    start = GetLineStart(CursorPosition);
                    end = CursorPosition;
                    return start != end;

                case '^':
                    start = Math.Min(
                        CursorPosition,
                        GetFirstNonWhitespace(CursorPosition));

                    end = Math.Max(
                        CursorPosition,
                        GetFirstNonWhitespace(CursorPosition));

                    return start != end;

                default:
                    return false;
            }
        }

        private int MoveLeftPosition(int count)
        {
            return Math.Max(
                GetLineStart(CursorPosition),
                CursorPosition - count);
        }

        private int MoveRightPosition(int count)
        {
            int start = GetLineStart(CursorPosition);
            int end = GetLineEnd(CursorPosition);

            int maximum = end > start
                ? end - 1
                : end;

            return Math.Min(
                CursorPosition + count,
                maximum);
        }

        private void MoveLeft(int count)
        {
            CursorPosition = MoveLeftPosition(count);
        }

        private void MoveRight(int count)
        {
            CursorPosition = MoveRightPosition(count);
        }

        private void MoveVertical(int direction)
        {
            int currentLine = GetLineIndex(CursorPosition);

            int column = CursorPosition -
                GetLineStart(CursorPosition);

            int targetLine = Math.Clamp(
                currentLine + direction,
                0,
                GetLastLineIndex());

            int start = GetLineStartByNumber(targetLine);
            int end = GetLineEnd(start);

            CursorPosition = end > start
                ? Math.Min(start + column, end - 1)
                : start;
        }

        private void MoveToLine(int line)
        {
            int target = Math.Clamp(
                line,
                0,
                GetLastLineIndex());

            CursorPosition = GetLineStartByNumber(target);

            ClampNormalCursor();
        }

        private void MoveToLineEnd()
        {
            int start = GetLineStart(CursorPosition);
            int end = GetLineEnd(CursorPosition);

            CursorPosition = end > start
                ? end - 1
                : start;
        }

        // =====================================================
        // WORD MOTIONS
        // =====================================================

        private int MoveWordForward(int position, int count)
        {
            position = Math.Clamp(position, 0, Text.Length);

            for (int n = 0; n < count; n++)
            {
                // If already on whitespace, move to the next
                // non-whitespace character without skipping its word.
                if (position < Text.Length &&
                    WordClass(Text[position]) == 0)
                {
                    while (position < Text.Length &&
                        WordClass(Text[position]) == 0)
                    {
                        position++;
                    }

                    continue;
                }

                if (position >= Text.Length)
                    return Text.Length;

                int type = WordClass(Text[position]);

                while (position < Text.Length &&
                    WordClass(Text[position]) == type)
                {
                    position++;
                }

                while (position < Text.Length &&
                    WordClass(Text[position]) == 0)
                {
                    position++;
                }
            }

            return position;
        }

        private int MoveWordBackward(int position, int count)
        {
            position = Math.Clamp(position, 0, Text.Length);

            for (int n = 0; n < count; n++)
            {
                if (position <= 0)
                    break;

                position--;

                while (position > 0 &&
                    WordClass(Text[position]) == 0)
                {
                    position--;
                }

                int type = WordClass(Text[position]);

                while (position > 0 &&
                    WordClass(Text[position - 1]) == type)
                {
                    position--;
                }
            }

            return position;
        }

        private int MoveWordEnd(int position, int count)
        {
            if (Text.Length == 0)
                return 0;

            position = Math.Clamp(
                position,
                0,
                Text.Length - 1);

            for (int n = 0; n < count; n++)
            {
                if (n > 0)
                    position++;

                while (position < Text.Length &&
                    WordClass(Text[position]) == 0)
                {
                    position++;
                }

                if (position >= Text.Length)
                    return Text.Length - 1;

                int type = WordClass(Text[position]);

                while (position + 1 < Text.Length &&
                    WordClass(Text[position + 1]) == type)
                {
                    position++;
                }
            }

            return position;
        }

        private static int WordClass(char value)
        {
            if (char.IsWhiteSpace(value))
                return 0;

            if (char.IsLetterOrDigit(value) || value == '_')
                return 1;

            return 2;
        }

        // =====================================================
        // TEXT OBJECTS
        // =====================================================

        private bool TryGetTextObjectRange(
            char prefix,
            char objectCharacter,
            out int start,
            out int end)
        {
            start = 0;
            end = 0;

            bool around = prefix == 'a';

            if (objectCharacter == 'w')
            {
                return TryGetWordObject(
                    around,
                    out start,
                    out end);
            }

            char open;
            char close;

            switch (objectCharacter)
            {
                case '(':
                case ')':
                    open = '(';
                    close = ')';
                    break;

                case '[':
                case ']':
                    open = '[';
                    close = ']';
                    break;

                case '{':
                case '}':
                    open = '{';
                    close = '}';
                    break;

                case '"':
                case '\'':
                case '`':
                    return TryGetQuoteObject(
                        objectCharacter,
                        around,
                        out start,
                        out end);

                default:
                    return false;
            }

            if (!TryGetDelimitedPair(
                open,
                close,
                out int left,
                out int right))
            {
                return false;
            }

            start = around ? left : left + 1;
            end = around ? right + 1 : right;

            return end >= start;
        }

        private bool TryGetWordObject(
            bool around,
            out int start,
            out int end)
        {
            start = 0;
            end = 0;

            int lineStart = GetLineStart(CursorPosition);
            int lineEnd = GetLineEnd(CursorPosition);

            if (lineStart == lineEnd)
                return false;

            int position = Math.Min(
                CursorPosition,
                Math.Max(lineStart, lineEnd - 1));

            if (WordClass(Text[position]) == 0)
            {
                int next = position;

                while (next < lineEnd &&
                    WordClass(Text[next]) == 0)
                {
                    next++;
                }

                if (next < lineEnd)
                {
                    position = next;
                }
                else
                {
                    position = CursorPosition;

                    while (position > lineStart &&
                        WordClass(Text[position - 1]) == 0)
                    {
                        position--;
                    }

                    if (position <= lineStart)
                        return false;

                    position--;
                }
            }

            int type = WordClass(Text[position]);

            start = position;
            end = position + 1;

            while (start > lineStart &&
                WordClass(Text[start - 1]) == type)
            {
                start--;
            }

            while (end < lineEnd &&
                WordClass(Text[end]) == type)
            {
                end++;
            }

            if (around)
            {
                int trailing = end;

                while (trailing < lineEnd &&
                    char.IsWhiteSpace(Text[trailing]))
                {
                    trailing++;
                }

                if (trailing > end)
                {
                    end = trailing;
                }
                else
                {
                    while (start > lineStart &&
                        char.IsWhiteSpace(Text[start - 1]))
                    {
                        start--;
                    }
                }
            }

            return end > start;
        }

        private bool TryGetQuoteObject(
            char quote,
            bool around,
            out int start,
            out int end)
        {
            start = 0;
            end = 0;

            int lineStart = GetLineStart(CursorPosition);
            int lineEnd = GetLineEnd(CursorPosition);

            int i = lineStart;

            while (i < lineEnd)
            {
                if (Text[i] != quote || IsEscaped(i))
                {
                    i++;
                    continue;
                }

                int closing = i + 1;

                while (closing < lineEnd &&
                    (Text[closing] != quote || IsEscaped(closing)))
                {
                    closing++;
                }

                if (closing >= lineEnd)
                    return false;

                if (i <= CursorPosition &&
                    CursorPosition <= closing)
                {
                    start = around ? i : i + 1;
                    end = around ? closing + 1 : closing;

                    return end >= start;
                }

                i = closing + 1;
            }

            return false;
        }

        private bool TryGetDelimitedPair(
            char open,
            char close,
            out int left,
            out int right)
        {
            left = -1;
            right = -1;

            int lineStart = GetLineStart(CursorPosition);
            int lineEnd = GetLineEnd(CursorPosition);

            var openings = new Stack<int>();

            int bestLength = int.MaxValue;

            for (int i = lineStart; i < lineEnd; i++)
            {
                if (Text[i] == open)
                {
                    openings.Push(i);
                }
                else if (Text[i] == close && openings.Count > 0)
                {
                    int candidate = openings.Pop();

                    if (candidate <= CursorPosition &&
                        CursorPosition <= i)
                    {
                        int length = i - candidate;

                        if (length < bestLength)
                        {
                            bestLength = length;
                            left = candidate;
                            right = i;
                        }
                    }
                }
            }

            return left >= 0 && right >= 0;
        }

        private bool IsEscaped(int position)
        {
            int backslashes = 0;

            for (int i = position - 1; i >= 0 &&
                Text[i] == '\\'; i--)
            {
                backslashes++;
            }

            return backslashes % 2 != 0;
        }

        // =====================================================
        // CHARACTER DELETION
        // =====================================================

        private void DeleteUnderCursor(int count)
        {
            int lineEnd = GetLineEnd(CursorPosition);

            int end = Math.Min(
                CursorPosition + count,
                lineEnd);

            if (end <= CursorPosition)
                return;

            Snapshot before = Capture();

            Text = Text.Remove(
                CursorPosition,
                end - CursorPosition);

            FinishNormalEdit(before);
        }

        // =====================================================
        // PASTING
        // =====================================================

        private void Paste(bool afterCursor, int count)
        {
            if (string.IsNullOrEmpty(_register))
            {
                StatusMessage = "Register is empty.";
                return;
            }

            Snapshot before = Capture();

            if (_registerLinewise)
            {
                string value = _register.EndsWith("\n", StringComparison.Ordinal)
                    ? _register
                    : _register + "\n";

                int insertionPoint;
                string separator = string.Empty;

                if (afterCursor)
                {
                    int lineEnd = GetLineEnd(CursorPosition);

                    if (lineEnd < Text.Length)
                    {
                        insertionPoint = lineEnd + 1;
                    }
                    else
                    {
                        insertionPoint = Text.Length;
                        separator = "\n";
                    }
                }
                else
                {
                    insertionPoint = GetLineStart(CursorPosition);
                }

                Text = Text.Insert(insertionPoint, separator);

                insertionPoint += separator.Length;

                var builder = new System.Text.StringBuilder();

                for (int i = 0; i < count; i++)
                    builder.Append(value);

                Text = Text.Insert(
                    insertionPoint,
                    builder.ToString());

                CursorPosition = insertionPoint;
            }
            else
            {
                int insertionPoint = afterCursor
                    ? Math.Min(CursorPosition + 1, Text.Length)
                    : CursorPosition;

                var builder = new System.Text.StringBuilder();

                for (int i = 0; i < count; i++)
                    builder.Append(_register);

                string value = builder.ToString();

                Text = Text.Insert(insertionPoint, value);

                CursorPosition = insertionPoint;
            }

            FinishNormalEdit(before);
        }

        // =====================================================
        // UNDO / REDO
        // =====================================================

        public void Undo()
        {
            if (Mode != VimMode.Normal)
                return;

            if (_undo.Count == 0)
            {
                StatusMessage = "Nothing to undo.";
                return;
            }

            _redo.Push(Capture());

            Restore(_undo.Pop());

            StatusMessage = "Undo.";
        }

        public void Redo()
        {
            if (Mode != VimMode.Normal)
                return;

            if (_redo.Count == 0)
            {
                StatusMessage = "Nothing to redo.";
                return;
            }

            _undo.Push(Capture());

            Restore(_redo.Pop());

            StatusMessage = "Redo.";
        }

        private Snapshot Capture()
        {
            return new Snapshot(Text, CursorPosition);
        }

        private void Restore(Snapshot snapshot)
        {
            Text = snapshot.Text;

            CursorPosition = snapshot.Cursor;

            ClampNormalCursor();
        }

        private void FinishNormalEdit(Snapshot before)
        {
            if (Text != before.Text)
            {
                _undo.Push(before);
                _redo.Clear();
            }

            Mode = VimMode.Normal;

            ClampNormalCursor();

            StatusMessage = string.Empty;
        }

        // =====================================================
        // COMMAND LINE MODE
        // =====================================================

        private void BeginCommandLine()
        {
            ClearPending();

            Mode = VimMode.CommandLine;

            StatusMessage = string.Empty;
        }

        public void CancelCommandLine()
        {
            if (Mode != VimMode.CommandLine)
                return;

            Mode = VimMode.Normal;

            ClearPending();
        }

        // Returns true only when the editor should close.
        public bool ExecuteColonCommand(string command)
        {
            if (Mode != VimMode.CommandLine)
                return false;

            command = (command ?? string.Empty).Trim();

            if (command.StartsWith(":", StringComparison.Ordinal))
                command = command[1..];

            Mode = VimMode.Normal;

            ClearPending();

            switch (command)
            {
                case "w":
                    TrySave();
                    return false;

                case "wq":
                    return TrySave();

                case "q":
                    if (IsModified)
                    {
                        StatusMessage =
                            "Unsaved changes. Use :q! to discard.";

                        return false;
                    }

                    return true;

                case "q!":
                    return true;

                default:
                    StatusMessage =
                        $"Not an editor command: :{command}";

                    return false;
            }
        }

        private bool TrySave()
        {
            try
            {
                string error = _writeFile(Text);

                if (!string.IsNullOrWhiteSpace(error))
                {
                    StatusMessage = error;
                    return false;
                }

                _savedText = Text;

                StatusMessage = $"\"{FilePath}\" written.";

                return true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"write: {ex.Message}";
                return false;
            }
        }

        // =====================================================
        // ESCAPE
        // =====================================================

        public void Escape()
        {
            if (Mode == VimMode.Insert)
            {
                EndInsert();
                return;
            }

            if (Mode == VimMode.CommandLine)
            {
                CancelCommandLine();
                return;
            }

            ClearPending();
        }

        private void ClearPending()
        {
            _count = 0;
            _operatorCount = 1;

            _pendingOperator = '\0';
            _pendingTextObject = '\0';

            _pendingG = false;
        }

        // =====================================================
        // COUNTS
        // =====================================================

        private void AddCountDigit(char digit)
        {
            _count = Math.Min(
                9999,
                (_count * 10) + (digit - '0'));
        }

        private int TakeCount(out bool explicitCount)
        {
            explicitCount = _count > 0;

            int result = explicitCount
                ? _count
                : 1;

            _count = 0;

            return result;
        }

        private static bool IsDigit(char value)
        {
            return value >= '0' && value <= '9';
        }

        // =====================================================
        // LINE INFORMATION
        // =====================================================

        private int GetLineStart(int position)
        {
            position = Math.Clamp(position, 0, Text.Length);

            int searchPosition = position - 1;

            if (searchPosition < 0)
                return 0;

            int newline = Text.LastIndexOf(
                '\n',
                searchPosition);

            return newline + 1;
        }

        private int GetLineEnd(int position)
        {
            position = Math.Clamp(position, 0, Text.Length);

            int newline = Text.IndexOf('\n', position);

            return newline < 0
                ? Text.Length
                : newline;
        }

        private int GetLineIndex(int position)
        {
            position = Math.Clamp(position, 0, Text.Length);

            int line = 0;

            for (int i = 0; i < position; i++)
            {
                if (Text[i] == '\n')
                    line++;
            }

            return line;
        }

        private int GetLineCount()
        {
            int count = 1;

            foreach (char character in Text)
            {
                if (character == '\n')
                    count++;
            }

            return count;
        }

        private int GetLastLineIndex()
        {
            return GetLineCount() - 1;
        }

        private int GetLineStartByNumber(int line)
        {
            line = Math.Max(0, line);

            int currentLine = 0;

            for (int i = 0; i < Text.Length; i++)
            {
                if (currentLine == line)
                    return i;

                if (Text[i] == '\n')
                    currentLine++;
            }

            return Text.Length;
        }

        private int GetFirstNonWhitespace(int position)
        {
            int start = GetLineStart(position);
            int end = GetLineEnd(position);

            for (int i = start; i < end; i++)
            {
                if (!char.IsWhiteSpace(Text[i]))
                    return i;
            }

            return end;
        }

        private void ClampNormalCursor()
        {
            CursorPosition = Math.Clamp(
                CursorPosition,
                0,
                Text.Length);

            int start = GetLineStart(CursorPosition);
            int end = GetLineEnd(CursorPosition);

            if (end > start && CursorPosition >= end)
            {
                CursorPosition = end - 1;
            }
            else if (CursorPosition < start)
            {
                CursorPosition = start;
            }
        }

        private static string Normalize(string content)
        {
            return content
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
        }
    }
}
