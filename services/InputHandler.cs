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

        private Linguist linguist = new();

        public InputHandler()
        {
            linguist.SearchUiVisibilityChanged += visible =>
            {
                SearchUiVisibilityChanged?.Invoke(visible);
            };
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
            return this.linguist.ModeString();
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

                    default:
                        character = '\0';
                        return false;
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