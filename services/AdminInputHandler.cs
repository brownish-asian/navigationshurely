using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace navigationshurely.services
{
    public sealed class AdminInputHandler
    {
        private string InputText = string.Empty;
        private int CursorPosition;
        private bool shipOut = false;
        public Func<string>? WorkingDirectoryProvider { get; set; }

        // History navigation.
        private int historyIndex = -1;
        private string historyDraft = string.Empty;

        // Completion.
        private List<string> completionMatches = new();
        private int completionIndex = -1;
        private int completionStart = -1;
        private string completionLastInput = string.Empty;
        private int completionLastCursor = -1;

        public event Action? PassTheMessageToSession;

        // Supplied by AdminWindow.
        public Func<IReadOnlyList<string>>? HistoryProvider { get; set; }

        public Func<string, int, int, IReadOnlyList<string>>?
            CompletionProvider
        { get; set; }

        private string PromptPrefix()
        {
            string directory =
                WorkingDirectoryProvider?.Invoke() ?? "/";

            return $"{directory} > ";
        }

        public string DisplayString()
        {
            return PromptPrefix() + InputText;
        }

        public int CursorIndex()
        {
            return CursorPosition + PromptPrefix().Length;
        }

        public string CommandString()
        {
            return InputText;
        }

        // ----------------------------------------------------
        // KEY PROCESSING
        // ----------------------------------------------------

        public void ProcessKey(KeyEventArgs e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            ModifierKeys modifiers = Keyboard.Modifiers;

            bool control = (modifiers & ModifierKeys.Control) != 0;
            bool alt = (modifiers & ModifierKeys.Alt) != 0;
            bool shift = (modifiers & ModifierKeys.Shift) != 0;

            // AltGr is used to produce characters on many keyboard layouts.
            bool altGr = control && alt && Keyboard.IsKeyDown(Key.RightAlt);

            if (altGr)
            {
                ProcessCharacterKey(key, e);
                return;
            }

            if (control)
            {
                if (ProcessControlKey(key))
                    e.Handled = true;

                return;
            }

            if (alt)
            {
                if (ProcessAltKey(key))
                    e.Handled = true;

                return;
            }

            if (ProcessOrdinaryKey(key, shift))
            {
                e.Handled = true;

                if (shipOut)
                {
                    shipOut = false;
                    PassTheMessageToSession?.Invoke();
                }

                return;
            }

            ProcessCharacterKey(key, e);
        }

        public void Clear()
        {
            InputText = string.Empty;
            CursorPosition = 0;
            shipOut = false;

            ResetHistoryNavigation();
            InvalidateCompletion();
        }

        // ----------------------------------------------------
        // ORDINARY KEYS
        // ----------------------------------------------------

        private bool ProcessOrdinaryKey(Key key, bool shift)
        {
            switch (key)
            {
                case Key.Enter:
                    shipOut = true;
                    return true;

                case Key.Back:
                    DeletePreviousCharacter();
                    return true;

                case Key.Delete:
                    DeleteRange(CursorPosition, 1);
                    return true;

                case Key.Left:
                    MoveCaret(CursorPosition - 1);
                    return true;

                case Key.Right:
                    MoveCaret(CursorPosition + 1);
                    return true;

                case Key.Home:
                    MoveCaret(0);
                    return true;

                case Key.End:
                    MoveCaret(InputText.Length);
                    return true;

                case Key.Up:
                    NavigateHistory(-1);
                    return true;

                case Key.Down:
                    NavigateHistory(1);
                    return true;

                case Key.Escape:
                    Clear();
                    return true;

                case Key.Tab:
                    CompleteInput();
                    return true;

                case Key.Insert when shift:
                    PasteClipboardText();
                    return true;

                default:
                    return false;
            }
        }

        // ----------------------------------------------------
        // CONTROL KEYS
        // ----------------------------------------------------

        private bool ProcessControlKey(Key key)
        {
            switch (key)
            {
                case Key.A:
                    // Beginning of line.
                    MoveCaret(0);
                    return true;

                case Key.E:
                    // End of line.
                    MoveCaret(InputText.Length);
                    return true;

                case Key.B:
                    MoveCaret(CursorPosition - 1);
                    return true;

                case Key.F:
                    MoveCaret(CursorPosition + 1);
                    return true;

                case Key.P:
                    NavigateHistory(-1);
                    return true;

                case Key.N:
                    NavigateHistory(1);
                    return true;

                case Key.H:
                    DeletePreviousCharacter();
                    return true;

                case Key.D:
                    DeleteRange(CursorPosition, 1);
                    return true;

                case Key.W:
                    DeletePreviousWord();
                    return true;

                case Key.U:
                    DeleteRange(0, CursorPosition);
                    return true;

                case Key.K:
                    DeleteRange(
                        CursorPosition,
                        InputText.Length - CursorPosition);
                    return true;

                case Key.T:
                    TransposeCharacters();
                    return true;

                case Key.C:
                    Clear();
                    return true;

                case Key.V:
                    PasteClipboardText();
                    return true;

                case Key.Left:
                    MoveCaretToPreviousWord();
                    return true;

                case Key.Right:
                    MoveCaretToNextWord();
                    return true;

                case Key.Back:
                    DeletePreviousWord();
                    return true;

                case Key.Delete:
                    DeleteNextWord();
                    return true;

                default:
                    return false;
            }
        }

        // ----------------------------------------------------
        // ALT KEYS
        // ----------------------------------------------------

        private bool ProcessAltKey(Key key)
        {
            switch (key)
            {
                case Key.B:
                    MoveCaretToPreviousWord();
                    return true;

                case Key.F:
                    MoveCaretToNextWord();
                    return true;

                case Key.D:
                    DeleteNextWord();
                    return true;

                case Key.Back:
                    DeletePreviousWord();
                    return true;

                default:
                    return false;
            }
        }

        // ----------------------------------------------------
        // HISTORY NAVIGATION
        // ----------------------------------------------------

        private void NavigateHistory(int direction)
        {
            IReadOnlyList<string>? history = HistoryProvider?.Invoke();

            if (history == null || history.Count == 0)
                return;

            // Start browsing from the newest entry.
            if (historyIndex == -1)
            {
                if (direction > 0)
                    return;

                historyDraft = InputText;
                historyIndex = history.Count;
            }

            if (direction < 0)
            {
                historyIndex = Math.Max(0, historyIndex - 1);
            }
            else
            {
                historyIndex = Math.Min(
                    history.Count,
                    historyIndex + 1);
            }

            // Reaching the bottom restores the user's unfinished input.
            InputText = historyIndex == history.Count
                ? historyDraft
                : history[historyIndex];

            CursorPosition = InputText.Length;

            InvalidateCompletion();
        }

        private void ResetHistoryNavigation()
        {
            historyIndex = -1;
            historyDraft = string.Empty;
        }

        // ----------------------------------------------------
        // TAB COMPLETION
        // ----------------------------------------------------

        private void CompleteInput()
        {
            if (CompletionProvider == null)
                return;

            (int Start, int End) range = FindTokenRange(CursorPosition);

            // Keep completion predictable when editing in the middle
            // of a token. Completion is available at the token's end.
            if (CursorPosition != range.End)
                return;

            // A repeated Tab cycles through the existing suggestions.
            if (completionMatches.Count > 1 &&
                completionStart == range.Start &&
                completionLastInput == InputText &&
                completionLastCursor == CursorPosition)
            {
                completionIndex =
                    (completionIndex + 1) % completionMatches.Count;

                ApplyCompletion(
                    completionMatches[completionIndex],
                    range.Start,
                    range.End,
                    appendSpace: false);

                return;
            }

            InvalidateCompletion();

            IReadOnlyList<string> results;

            try
            {
                results = CompletionProvider(
                    InputText,
                    range.Start,
                    CursorPosition);
            }
            catch
            {
                return;
            }

            if (results == null || results.Count == 0)
                return;

            completionMatches = results
                .Where(candidate => !string.IsNullOrEmpty(candidate))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (completionMatches.Count == 0)
                return;

            completionStart = range.Start;

            // One match: complete it and add a trailing space.
            if (completionMatches.Count == 1)
            {
                ApplyCompletion(
                    completionMatches[0],
                    range.Start,
                    range.End,
                    appendSpace: true);

                InvalidateCompletion();
                return;
            }

            string typedPrefix = InputText.Substring(
                range.Start,
                range.End - range.Start);

            string commonPrefix = GetCommonPrefix(completionMatches);

            // First extend the text as far as the candidates agree.
            if (commonPrefix.Length > typedPrefix.Length &&
                commonPrefix.StartsWith(
                    typedPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                completionIndex = -1;

                ApplyCompletion(
                    commonPrefix,
                    range.Start,
                    range.End,
                    appendSpace: false);
            }
            else
            {
                // No further common characters. Start cycling.
                completionIndex = 0;

                ApplyCompletion(
                    completionMatches[0],
                    range.Start,
                    range.End,
                    appendSpace: false);
            }
        }

        private void ApplyCompletion(
            string candidate,
            int start,
            int end,
            bool appendSpace)
        {
            string oldInput = InputText;

            InputText =
                oldInput[..start] +
                candidate +
                oldInput[end..];

            CursorPosition = start + candidate.Length;

            // Directories end in '/', so the user can immediately
            // continue completing the next path component.
            if (appendSpace &&
                end == oldInput.Length &&
                !IsDirectoryCompletion(candidate))
            {
                InputText += " ";
                CursorPosition++;
            }

            completionLastInput = InputText;
            completionLastCursor = CursorPosition;
        }

        private static bool IsDirectoryCompletion(string candidate)
        {
            if (candidate.Length >= 2 &&
                (candidate[0] == '"' || candidate[0] == '\'') &&
                candidate[^1] == candidate[0])
            {
                candidate = candidate[1..^1];
            }

            return candidate.EndsWith("/", StringComparison.Ordinal);
        }

        private static string GetCommonPrefix(List<string> candidates)
        {
            if (candidates.Count == 0)
                return string.Empty;

            string first = candidates[0];
            int length = first.Length;

            foreach (string candidate in candidates.Skip(1))
            {
                length = Math.Min(length, candidate.Length);

                while (length > 0 &&
                    !string.Equals(
                        first[..length],
                        candidate[..length],
                        StringComparison.OrdinalIgnoreCase))
                {
                    length--;
                }

                if (length == 0)
                    break;
            }

            return first[..length];
        }

        private void InvalidateCompletion()
        {
            completionMatches.Clear();
            completionIndex = -1;
            completionStart = -1;
            completionLastInput = string.Empty;
            completionLastCursor = -1;
        }

        // Finds the token surrounding the cursor.
        // Whitespace, semicolons and redirection symbols separate tokens.
        // Quoted strings and escaped spaces remain part of a token.
        private (int Start, int End) FindTokenRange(int cursor)
        {
            int start = 0;
            int end = InputText.Length;

            char quote = '\0';
            bool escaped = false;

            cursor = Math.Max(0, Math.Min(cursor, InputText.Length));

            for (int i = 0; i < InputText.Length; i++)
            {
                char c = InputText[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (quote != '\0')
                {
                    if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c is '"' or '\'')
                {
                    quote = c;
                    continue;
                }

                bool delimiter =
                    char.IsWhiteSpace(c) || c is ';' or '>';

                if (!delimiter)
                    continue;

                if (i < cursor)
                {
                    start = i + 1;
                }
                else
                {
                    end = i;
                    break;
                }
            }

            return (start, end);
        }

        // ----------------------------------------------------
        // CHARACTER INPUT
        // ----------------------------------------------------

        private void ProcessCharacterKey(Key key, KeyEventArgs e)
        {
            if (TryTranslateKey(key, out string text, out bool deadKey))
            {
                InsertText(text);
                e.Handled = true;
            }
            else if (deadKey)
            {
                e.Handled = true;
            }
        }

        private void InsertText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            InputText = InputText.Insert(CursorPosition, text);
            CursorPosition += text.Length;

            ResetHistoryNavigation();
            InvalidateCompletion();
        }

        // ----------------------------------------------------
        // CURSOR MOVEMENT
        // ----------------------------------------------------

        private void MoveCaret(int newPosition)
        {
            CursorPosition = Math.Max(
                0,
                Math.Min(newPosition, InputText.Length));

            InvalidateCompletion();
        }

        // ----------------------------------------------------
        // DELETION
        // ----------------------------------------------------

        private void DeletePreviousCharacter()
        {
            if (CursorPosition > 0)
                DeleteRange(CursorPosition - 1, 1);
        }

        private void DeleteRange(int start, int length)
        {
            start = Math.Max(0, Math.Min(start, InputText.Length));
            length = Math.Max(0, Math.Min(length, InputText.Length - start));

            if (length == 0)
                return;

            InputText = InputText.Remove(start, length);

            if (start < CursorPosition)
                CursorPosition = Math.Max(start, CursorPosition - length);

            ResetHistoryNavigation();
            InvalidateCompletion();
        }

        private void DeletePreviousWord()
        {
            int start = CursorPosition;

            while (start > 0 && char.IsWhiteSpace(InputText[start - 1]))
                start--;

            while (start > 0 && !char.IsWhiteSpace(InputText[start - 1]))
                start--;

            DeleteRange(start, CursorPosition - start);
        }

        private void DeleteNextWord()
        {
            int end = CursorPosition;

            while (end < InputText.Length &&
                char.IsWhiteSpace(InputText[end]))
            {
                end++;
            }

            while (end < InputText.Length &&
                !char.IsWhiteSpace(InputText[end]))
            {
                end++;
            }

            DeleteRange(CursorPosition, end - CursorPosition);
        }

        // ----------------------------------------------------
        // WORD NAVIGATION
        // ----------------------------------------------------

        private void MoveCaretToPreviousWord()
        {
            int position = CursorPosition;

            while (position > 0 &&
                char.IsWhiteSpace(InputText[position - 1]))
            {
                position--;
            }

            while (position > 0 &&
                !char.IsWhiteSpace(InputText[position - 1]))
            {
                position--;
            }

            MoveCaret(position);
        }

        private void MoveCaretToNextWord()
        {
            int position = CursorPosition;

            while (position < InputText.Length &&
                char.IsWhiteSpace(InputText[position]))
            {
                position++;
            }

            while (position < InputText.Length &&
                !char.IsWhiteSpace(InputText[position]))
            {
                position++;
            }

            MoveCaret(position);
        }

        // ----------------------------------------------------
        // CHARACTER TRANSPOSITION
        // ----------------------------------------------------

        private void TransposeCharacters()
        {
            if (InputText.Length < 2)
                return;

            int right = CursorPosition == InputText.Length
                ? InputText.Length - 1
                : CursorPosition;

            int left = right - 1;

            if (left < 0)
                return;

            char[] characters = InputText.ToCharArray();

            (characters[left], characters[right]) =
                (characters[right], characters[left]);

            InputText = new string(characters);

            if (CursorPosition < InputText.Length)
                CursorPosition++;

            ResetHistoryNavigation();
            InvalidateCompletion();
        }

        // ----------------------------------------------------
        // CLIPBOARD
        // ----------------------------------------------------

        private void PasteClipboardText()
        {
            if (Clipboard.ContainsText())
                InsertText(Clipboard.GetText());
        }

        // ----------------------------------------------------
        // WINDOWS KEYBOARD TRANSLATION
        // ----------------------------------------------------

        private static bool TryTranslateKey(
            Key key,
            out string text,
            out bool deadKey)
        {
            text = string.Empty;
            deadKey = false;

            int virtualKey = KeyInterop.VirtualKeyFromKey(key);

            if (virtualKey == 0)
                return false;

            byte[] keyboardState = new byte[256];

            if (!GetKeyboardState(keyboardState))
                return false;

            IntPtr keyboardLayout = GetKeyboardLayout(0);

            uint scanCode = MapVirtualKeyEx(
                (uint)virtualKey,
                MapVkToVscEx,
                keyboardLayout);

            StringBuilder buffer = new StringBuilder(8);

            int result = ToUnicodeEx(
                (uint)virtualKey,
                scanCode,
                keyboardState,
                buffer,
                buffer.Capacity,
                0,
                keyboardLayout);

            if (result > 0)
            {
                int characterCount = Math.Min(result, buffer.Length);

                text = buffer.ToString(0, characterCount);

                return text.Length > 0;
            }

            if (result < 0)
                deadKey = true;

            return false;
        }

        private const uint MapVkToVscEx = 4;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetKeyboardState(
            [Out] byte[] keyboardState);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint threadId);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKeyEx(
            uint code,
            uint mapType,
            IntPtr keyboardLayout);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ToUnicodeEx(
            uint virtualKey,
            uint scanCode,
            byte[] keyboardState,
            [Out] StringBuilder buffer,
            int bufferSize,
            uint flags,
            IntPtr keyboardLayout);
    }
}