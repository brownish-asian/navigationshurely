using navigationshurely.services;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Input;
using navigationshurely.models;

namespace navigationshurely.Services
{
    public class InputHandler
    {
        public event Action<bool>? SearchUiVisibilityChanged;
        public static event Action? ExitApplication;
        public event Action<bool>? ShowMarks;
        public event Action? HideTheEnterBox;

        private Linguist linguist = new();

        public InputHandler()
        {
            linguist.SearchUiVisibilityChanged += visible => { SearchUiVisibilityChanged?.Invoke(visible); };
            linguist.ShowMarks += visible => { ShowMarks?.Invoke(visible); };
            linguist.HideTheEnterBox += PassingOnTheMessage;
        }

        private void PassingOnTheMessage()
        {
            HideTheEnterBox?.Invoke();
        }
        public string AliasForThisVerse(int encodedBookNumber)
        {
            return linguist.AliasForThisVerse(encodedBookNumber);
        }

        public EntireCommandStatus ProcessKey(Key key)
        {
            if (TryConvertKey(key, out char character))
            {
                return linguist.Feed(character);
            }
            return EntireCommandStatus.Invalid;
        }

        public Color CommandColor() { return linguist.CommandColor(); }

        public string CommandInfo()
        {
            return linguist.CurrentCommandInfo();
        }

        public Command? GetCommand()
        {
            return linguist.TakeCommand();
        }

        public string GetStateText()
        {
            return linguist.ModeString();
        }

        private static bool TryConvertKey(Key key, out char character)
        {
            // Check special Ctrl combinations FIRST.
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                switch (key)
                {
                    case Key.D:
                        character = '<';
                        return true;

                    case Key.U:
                        character = '>';
                        return true;

                    case Key.F:
                        character = '\u0001'; // Ctrl+f
                        return true;

                    case Key.Back:
                        character = '\u0006'; // Ctrl+Backspace
                        return true;

                    case Key.Q:
                        character = '\u0005';
                        ExitApplication.Invoke();
                        return true;

                    case Key.B:
                        character = '\u0002'; // Ctrl+b
                        return true;

                    default:
                        character = '\0';
                        return false;
                }
            }

            // Shift+Tab must be checked before ordinary Tab.
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                if (key == Key.Tab)
                {
                    character = '\u0003'; // Shift+Tab
                    return true;
                }
            }

            // Letters: distinguish lowercase from uppercase.
            if (key >= Key.A && key <= Key.Z)
            {
                bool shiftPressed =
                    (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

                char letter = (char)('a' + (key - Key.A));

                character = shiftPressed
                    ? char.ToUpperInvariant(letter)
                    : letter;

                return true;
            }

            // Ordinary digits.
            if (key >= Key.D0 && key <= Key.D9)
            {
                character = (char)('0' + (key - Key.D0));
                return true;
            }

            switch (key)
            {
                case Key.OemPeriod:
                    character = '.'; // Full stop (.)
                    return true;

                case Key.OemMinus:
                    character = '-'; // Normal hyphen (-)
                    return true;

                case Key.OemQuotes:
                    character = '\'';
                    return true;

                case Key.Escape:
                    character = '|';
                    return true;

                case Key.Space:
                    character = ' ';
                    return true;

                case Key.Oem1:
                    character = ';';
                    return true;

                case Key.Oem2:
                    character = '/';
                    return true;

                case Key.Enter:
                    character = '_';
                    return true;

                case Key.Back:
                    character = '{';
                    return true;

                case Key.Tab:
                    character = '+';
                    return true;

                default:
                    character = '\0';
                    return false;
            }
        }
    }
}