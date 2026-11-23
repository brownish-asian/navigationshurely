using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace navigationshurely.services
{
    /// <summary>
    /// Handles a single-line terminal-style input prompt.
    /// Call ProcessKey from the TextBox's PreviewKeyDown event.
    /// </summary>
    public sealed class AdminInputHandler
    {
        private string InputText = string.Empty;
        private int CursorPosition;
        private bool shipOut = false;

        public event Action? PassTheMessageToSession;

        public string DisplayString()
        {
            return "> " + InputText;
        }

        public int CursorIndex()
        {
            return CursorPosition + 2;
        }

        public string CommandString()
        {
            return InputText;
        }

        /// <summary>
        /// Processes the pressed key and updates the internal command text/caret.
        /// Handled keys are marked as handled so the TextBox does not insert them again.
        /// </summary>
        public void ProcessKey(KeyEventArgs e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));

            // Alt-modified keys are reported as Key.System; SystemKey is the actual key.
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            ModifierKeys modifiers = Keyboard.Modifiers;

            bool control = (modifiers & ModifierKeys.Control) != 0;
            bool alt = (modifiers & ModifierKeys.Alt) != 0;
            bool shift = (modifiers & ModifierKeys.Shift) != 0;

            // Right Alt + Ctrl is AltGr on many keyboard layouts. It produces text,
            // so let the character translator handle it instead of treating it as a shortcut.
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
                    PassTheMessageToSession.Invoke();
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
        }

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

                case Key.Escape:
                    Clear();
                    return true;

                case Key.Tab:
                    // Keep focus in the prompt; tab completion is not implemented here.
                    return true;

                case Key.Insert when shift:
                    PasteClipboardText();
                    return true;

                default:
                    return false;
            }
        }

        private bool ProcessControlKey(Key key)
        {
            switch (key)
            {
                case Key.A:
                    // Ctrl+A: beginning of line (terminal/Readline behaviour).
                    MoveCaret(0);
                    return true;

                case Key.E:
                    // Ctrl+E: end of line.
                    MoveCaret(InputText.Length);
                    return true;

                case Key.B:
                    // Ctrl+B: one character backwards.
                    MoveCaret(CursorPosition - 1);
                    return true;

                case Key.F:
                    // Ctrl+F: one character forwards.
                    MoveCaret(CursorPosition + 1);
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
                    DeleteRange(CursorPosition, InputText.Length - CursorPosition);
                    return true;

                case Key.T:
                    TransposeCharacters();
                    return true;

                case Key.C:
                    // Ctrl+C: cancel the current command line.
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

        private void ProcessCharacterKey(Key key, KeyEventArgs e)
        {
            if (TryTranslateKey(key, out string text, out bool deadKey))
            {
                InsertText(text);
                e.Handled = true;
            }
            else if (deadKey)
            {
                // A dead key (for example, an accent key) has been consumed by ToUnicodeEx.
                // The following key can then produce the composed character.
                e.Handled = true;
            }
        }

        private void InsertText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            InputText = InputText.Insert(CursorPosition, text);
            CursorPosition += text.Length;
        }

        private void MoveCaret(int newPosition)
        {
            CursorPosition = Math.Max(0, Math.Min(newPosition, InputText.Length));
        }

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

            while (end < InputText.Length && char.IsWhiteSpace(InputText[end]))
                end++;

            while (end < InputText.Length && !char.IsWhiteSpace(InputText[end]))
                end++;

            DeleteRange(CursorPosition, end - CursorPosition);
        }

        private void MoveCaretToPreviousWord()
        {
            int position = CursorPosition;

            while (position > 0 && char.IsWhiteSpace(InputText[position - 1]))
                position--;

            while (position > 0 && !char.IsWhiteSpace(InputText[position - 1]))
                position--;

            MoveCaret(position);
        }

        private void MoveCaretToNextWord()
        {
            int position = CursorPosition;

            while (position < InputText.Length && char.IsWhiteSpace(InputText[position]))
                position++;

            while (position < InputText.Length && !char.IsWhiteSpace(InputText[position]))
                position++;

            MoveCaret(position);
        }

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
            (characters[left], characters[right]) = (characters[right], characters[left]);
            InputText = new string(characters);

            if (CursorPosition < InputText.Length)
                CursorPosition++;
        }

        private void PasteClipboardText()
        {
            if (Clipboard.ContainsText())
                InsertText(Clipboard.GetText());
        }

        /// <summary>
        /// Converts a key press into the character(s) produced by the active Windows
        /// keyboard layout, including Shift, Caps Lock, numpad keys and AltGr.
        /// KeyEventArgs alone does not contain the resulting character, so Windows' own
        /// keyboard-layout translation is used here.
        /// </summary>
        private static bool TryTranslateKey(Key key, out string text, out bool deadKey)
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
            uint scanCode = MapVirtualKeyEx((uint)virtualKey, MapVkToVscEx, keyboardLayout);

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
        private static extern bool GetKeyboardState([Out] byte[] keyboardState);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint threadId);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr keyboardLayout);

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
