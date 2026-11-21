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
        public event Action? ExitApplication;
        public event Action<bool>? ShowMarks;
        public event Action<bool>? PressEnterVisibilityChanged;
        public event Action? ToggleOptions;

        private readonly Linguist _linguist;

        public InputHandler(AliasCatalogue aliasCatalog)
        {
            _linguist = new Linguist(aliasCatalog);

            _linguist.SearchUiVisibilityChanged +=
                visible => SearchUiVisibilityChanged?.Invoke(visible);

            _linguist.ShowMarks +=
                visible => ShowMarks?.Invoke(visible);

            _linguist.PressEnterVisibilityChanged +=
                visible => PressEnterVisibilityChanged?.Invoke(visible);
        }

        public bool SearchResultsAvailable => _linguist.SearchResultsAvailable;

        public void SetSearchResultsAvailable(bool available)
        {
            _linguist.SetSearchResultsAvailable(available);
        }

        public string? AliasSuggestionPrefix()
        {
            return _linguist.AliasSuggestionPrefix();
        }

        public string? BookAliasSuggestionPrefix()
        {
            return _linguist.BookAliasSuggestionPrefix();
        }

        public List<KeyValuePair<string, int>> GetBookAliasSuggestions(string prefix)
        {
            return _linguist.GetBookAliasSuggestions(prefix);
        }

        public List<KeyValuePair<char, int>> GetMovementQuantifierOptions()
        {
            return _linguist.GetMovementQuantifierOptions();
        }

        public EntireCommandStatus ProcessCharacter(char character)
        {
            return _linguist.Feed(character);
        }

        public void BookAliasSuggestionSelected()
        {
            // Uses the same parser-reset logic as semicolon suggestions.
            _linguist.AliasSuggestionSelected();
        }

        public void AliasSuggestionSelected()
        {
            _linguist.AliasSuggestionSelected();
        }

        public void SelectNavigationMode(State target)
        {
            _linguist.SelectNavigationMode(target);
        }

        public EntireCommandStatus ProcessKey(Key key)
        {
            if (TryConvertKey(key, out char character))
            {
                return _linguist.Feed(character);
            }
            return EntireCommandStatus.Invalid;
        }

        public Color CommandColor(bool classic) { return _linguist.CommandColor(classic); }

        public string CommandInfo()
        {
            return _linguist.CurrentCommandInfo();
        }

        public Command? GetCommand()
        {
            return _linguist.TakeCommand();
        }

        // One-shot access to the raw phrase captured at search submission.
        public string? TakeSubmittedSearchPhrase()
        {
            return _linguist.TakeSubmittedSearchPhrase();
        }

        // Restore the navigation mode that preceded the current search session.
        public void ExitSearchMode()
        {
            _linguist.ExitSearchMode();
        }

        public State getState()
        {
            return _linguist.getState();
        }

        public string GetStateText()
        {
            return _linguist.ModeString();
        }

        private bool TryConvertKey(Key key, out char character)
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
                        ExitApplication?.Invoke();
                        return false;

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
                if (key == Key.OemQuestion)
                {
                    character = '?'; // Shift+/
                    ToggleOptions?.Invoke();
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
