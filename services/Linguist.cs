using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using BibleApp.Data;
using navigationshurely.models;
using navigationshurely.Services;

namespace navigationshurely.services
{
    public class Linguist
    {
        private State state;
        private ParseState parseState;
        private Command? command;
        private string commandBuffer = string.Empty;
        private string choiceCommandBuffer = string.Empty;
        private int quantifier = 0;
        private EntireCommandStatus validity;
        private State previousStateBeforeSearch;
        private int previousQuantifier = 1;
        private char? activeFilter;
        private string searchQuery = string.Empty;
        private string wholeQueryWithCommas;
        private string lastSubmittedSearch;

        // Set only when a search is actually submitted with Enter. InputHandler
        // consumes this value once; it is deliberately separate from the
        // comma-serialized search configuration stored in Command.SearchText.
        private string? submittedSearchPhrase;
        private string commandString = string.Empty;
        private bool normalChoosingMode = true;
        private bool justANormalStateChange = false;

        // Choice mode is only keyboard-focusable after the current search
        // session has actually produced at least one result.
        private bool searchResultsAvailable;
        private readonly AliasCatalogue _aliasCatalog;

        public event Action<bool>? SearchUiVisibilityChanged;
        public event Action<bool>? ShowMarks;
        public event Action<bool>? PressEnterVisibilityChanged;

        public Linguist(AliasCatalogue aliasCatalog)
        {
            state = State.B;
            parseState = ParseState.Start;
            command = null;
            validity = EntireCommandStatus.Incomplete;
            wholeQueryWithCommas = ",,,,,lt,os,mf";
            lastSubmittedSearch = wholeQueryWithCommas;
            _aliasCatalog = aliasCatalog;
        }

        public string CurrentCommandInfo()
        {
            String result = commandString;
            if (validity == EntireCommandStatus.Incomplete)
            {
                return result;
            }
            commandString = "";
            if (justANormalStateChange && state != State.Query && state != State.Choice)
            {
                return "";
            }
            return result;
        }

        private void AddCharToCommandInfo(char k)
        {
            switch (k)
            {
                case '{':
                    break;
                case '+':
                    commandString += "[Tab]";
                    break;
                case '|':
                    commandString += "Esc.";
                    break;
                case '<':
                    commandString += "Ctrl+D";
                    break;
                case '>':
                    commandString += "Ctrl+U";
                    break;
                case '\u0003':
                    commandString += "Shift+[Tab]";
                    break;
                case '\u0001':
                    commandString += "Ctrl+F";
                    break;
                case '\u0002':
                    commandString += "Ctrl+B";
                    break;
                case '\u0006':
                    commandString += "Ctrl+Backspace";
                    break;
                default:
                    commandString += k;
                    break;
            }
        }

        // Removes the previous whitespace-delimited word, along with the
        // single separator immediately before it. This mirrors the existing
        // query-mode behaviour while also handling tabs and other whitespace.
        private static string DeletePreviousWord(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            int end = value.Length;

            // Ignore whitespace immediately before the caret.
            while (end > 0 && char.IsWhiteSpace(value[end - 1]))
                end--;

            if (end == 0)
                return string.Empty;

            // Find the beginning of the previous word.
            int start = end;
            while (start > 0 && !char.IsWhiteSpace(value[start - 1]))
                start--;

            // Also remove its preceding separator, matching the old query
            // implementation (e.g. "one two" becomes "one").
            if (start > 0)
                start--;

            return value[..start];
        }

        public Color CommandColor(bool classic)
        {
            switch (validity)
            {
                case EntireCommandStatus.Incomplete:
                    return Color.FromRgb(85, 200, 208);
                case EntireCommandStatus.Invalid:
                    return Color.FromRgb(255, 107, 120);
                case EntireCommandStatus.Valid:
                    bool wasNormalChange = justANormalStateChange;
                    justANormalStateChange = false;

                    if (wasNormalChange && !classic)
                    {
                        return Color.FromArgb(0x55, 0x07, 0x13, 0x20);
                    }
                    return Color.FromRgb(142, 214, 165);
                default:
                    return Color.FromRgb(197, 206, 216);
            }
        }


        private static readonly Dictionary<char, int> Quantifiers = new()
        {
            ['z'] = 0,
            ['u'] = 1,
            ['d'] = 2,
            ['t'] = 3,
            ['a'] = 4,
            ['i'] = 5,
            ['s'] = 6,
            ['e'] = 7,
            ['o'] = 8,
            ['n'] = 9
        };


        private static readonly Dictionary<char, int> FastQuantifiers = new()
        {
            ['h'] = 10,
            ['l'] = 11,
            ['w'] = 12,
            ['y'] = 13,
            ['p'] = 14,
            ['m'] = 15,
            ['q'] = 20,
        };


        private static readonly Dictionary<string, int> BCommands = new()
        {
            ["ge"] = 1, //genesis
            ["x"] = 2, //exodo
            ["le"] = 3, //levitico
            ["bi"] = 4, //bilang
            ["de"] = 5, //deuteronomio
            ["su"] = 6, //josue
            ["uk"] = 7, //hukom
            ["ru"] = 8, //ruth
            ["is"] = 9, //I samuel
            ["ws"] = 10, // II samuel
            ["ih"] = 11, //I hari
            ["wh"] = 12, //II hari
            ["icr"] = 13, //I cronica
            ["wcr"] = 14, //II cronica
            ["ez"] = 15, //ezra
            ["ne"] = 16, //nehemias
            ["es"] = 17, //esther
            ["ob"] = 18, //job
            ["aw"] = 19, //awit
            ["wi"] = 20, //kawikaan
            ["ec"] = 21, //eclesiastes
            ["so"] = 22, //awit ng mga awit
            ["sa"] = 23, //isaias
            ["re"] = 24, //jeremias
            ["y"] = 25, //panaghoy
            ["ek"] = 26, //ezekiel
            ["da"] = 27, //daniel
            ["os"] = 28, //oseas
            ["oe"] = 29, //joel
            ["am"] = 30, //amos
            ["ba"] = 31, //obadias
            ["on"] = 32, //jonas
            ["mi"] = 33, //mikas
            ["na"] = 34, //nahum
            ["ha"] = 35, //habacuc
            ["ze"] = 36, //zefanias
            ["ag"] = 37, //hagai
            ["za"] = 38, //zacarias
            ["mal"] = 39, //malakias
            ["mat"] = 40, //mateo
            ["mar"] = 41, //marcos
            ["lu"] = 42, //lucas
            ["ua"] = 43, //juan
            ["gw"] = 44, //gawa
            ["ro"] = 45, //roma
            ["ico"] = 46, //I corinto
            ["wco"] = 47, //II corinto
            ["ga"] = 48, //galacia
            ["ef"] = 49, //efeso
            ["fp"] = 50, //filipos
            ["ls"] = 51, //colosas
            ["its"] = 52, //I tesalonica
            ["wts"] = 53, //II tesalonica
            ["itm"] = 54, //I timoteo
            ["wtm"] = 55, //II timoteo
            ["ti"] = 56, //tito
            ["fm"] = 57, //filemon
            ["he"] = 58, //hebreo
            ["sn"] = 59, //santiago
            ["p"] = 60, //I pedro
            ["wp"] = 61, //II pedro
            ["iua"] = 62, //I juan
            ["wua"] = 63, //II juan
            ["tua"] = 64, //III juan
            ["ud"] = 65, //judas
            ["ap"] = 66 //apocalipsis
        };


        private static readonly HashSet<string> BPrefixes = BuildPrefixes();

        private static HashSet<string> BuildPrefixes()
        {
            var prefixes = new HashSet<string>();

            foreach (var command in BCommands.Keys)
            {
                for (int i = 1; i < command.Length; i++)
                {
                    prefixes.Add(command[..i]);
                }
            }

            return prefixes;
        }

        public string? AliasSuggestionPrefix()
        {
            // Suggestions are only relevant in semicolon mode.
            if (state != State.S)
                return null;

            // Don't show suggestions for invalid input.
            if (validity == EntireCommandStatus.Invalid)
                return null;

            // Only show suggestions when the parser has confirmed
            // that the user is currently typing a possible verse alias.
            if (parseState != ParseState.ReadingSemicolonAlias)
                return null;

            // Safety check: never search using an empty prefix.
            if (string.IsNullOrEmpty(commandBuffer))
                return null;

            return commandBuffer;
        }

        public string? BookAliasSuggestionPrefix()
        {
            // Book suggestions are available whenever BOOK mode is idle.
            if (state != State.B)
                return null;

            // When the parser is waiting for a command, show every book.
            if (parseState == ParseState.Start)
                return string.Empty;

            // Don't display suggestions for invalid unfinished input.
            if (validity == EntireCommandStatus.Invalid)
                return null;

            // Only filter suggestions while typing a possible book alias.
            if (parseState != ParseState.ReadingBookAlias)
                return null;

            if (string.IsNullOrEmpty(commandBuffer) ||
                !BPrefixes.Contains(commandBuffer))
            {
                return null;
            }

            return commandBuffer;
        }

        public List<KeyValuePair<string, int>> GetBookAliasSuggestions(string prefix)
        {
            // An empty prefix intentionally matches every book.
            return BCommands
                .Where(pair => pair.Key.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
                .OrderBy(pair => pair.Value)
                .ToList();
        }

        public List<KeyValuePair<char, int>> GetMovementQuantifierOptions()
        {
            // Zero is excluded because the current movement parser treats
            // a zero quantity as its default movement of one.
            IEnumerable<KeyValuePair<char, int>> options;

            // Fast quantifiers are only recognised when a new quantity
            // is being entered, not halfway through an existing number.
            if (parseState == ParseState.Start)
            {
                options = Quantifiers
                    .Where(pair => pair.Value > 0);
                options = options.Concat(FastQuantifiers);
            }
            else
            {
                options = Quantifiers.ToList();
            }
            return options
                .OrderBy(pair => pair.Value)
                .ToList();
        }

        public string ModeString()
        {
            if (state == State.B) return "BOOK Mode";
            else if (state == State.S) return "SEMI-COLON Mode";
            else if (state == State.C) return "CHAPTER Mode";
            else if (state == State.V) return "VERSE Mode";
            else if (state == State.Filter) return "SEARCH: FILTER mode";
            else if (state == State.Query) return "SEARCH: QUERY mode";
            else if (state == State.Choice)
            {
                if (normalChoosingMode)
                {
                    return "SEARCH: CHOICE mode";
                }
                else
                {
                    return "SEARCH: ABSOLUTE PATH mode";
                }
            }
            else return "Random mode";
        }

        public State getState()
        {
            return state;
        }

        private enum ParseState
        {
            Start,
            ReadingQuantifier,
            ReadingBookAlias,
            ReadingSemicolonAlias,
            SettingMark,
            TeleportingToMark,
            CopyingSomething,
            FilterDeciding,
            FilterBookAlias,
            FilterQuantifier,
        }

        public void ResetCommand()
        {
            parseState = ParseState.Start;
            quantifier = 0;
            this.command = null;
            commandBuffer = "";
        }

        public void AliasSuggestionSelected()
        {
            // Reset the unfinished alias input.
            ResetCommand();

            // Clear the displayed command text.
            commandString = string.Empty;

            // Restore a clean command state.
            validity = EntireCommandStatus.Valid;

            justANormalStateChange = false;
        }

        private EntireCommandStatus NotValid()
        {
            validity = EntireCommandStatus.Invalid;
            ResetCommand();
            activeFilter = null;
            NotifySearchDraftVisibilityChanged();
            return validity;
        }

        public Command? TakeCommand()
        {
            Command? result = command;
            ResetCommand();
            return result;
        }

        // Returns the raw phrase from the most recently submitted search,
        // then clears it so it cannot accidentally be reused by another command.
        public string? TakeSubmittedSearchPhrase()
        {
            string? result = submittedSearchPhrase;
            submittedSearchPhrase = null;
            return result;
        }


        //MAIN INPUT HANDLER
        public EntireCommandStatus Feed(char k)
        {
            AddCharToCommandInfo(k);

            if (state == State.Query || state == State.Filter || state == State.Choice)
            {
                return Search(k);
            }
            //termination keys
            if (parseState == ParseState.TeleportingToMark)
            {
                command = new Command { Mode = State.S, Quantity = -1 * (int)k, Direction = Direct.Absolute };
                ShowMarks?.Invoke(false);
                validity = EntireCommandStatus.Valid;
                return validity;
            }
            else if (k == ' ')
            {
                if (state == State.C || state == State.V)
                {
                    return CompleteMovement(Direct.Absolute);
                }

                ResetCommand();
                validity = EntireCommandStatus.Invalid;
                return validity;
            }
            else if (k == '\'')
            {
                ShowMarks?.Invoke(true);
                parseState = ParseState.TeleportingToMark;
                validity = EntireCommandStatus.Incomplete;
                return validity;
            }
            //book mode
            if (state == State.B)
            {
                // Allow Backspace to edit an unfinished book alias.
                if (parseState == ParseState.ReadingBookAlias)
                {
                    if (k == '{')
                    {
                        return BackspaceBookAlias(false);
                    }
                    else if (k == '\u0006')
                    {
                        return BackspaceBookAlias(true);
                    }
                }

                commandBuffer += k;

                if (BCommands.TryGetValue(commandBuffer, out int position))
                {
                    command = new Command { Mode = State.B, Quantity = position, Direction = Direct.Absolute };
                    commandBuffer = "";
                    parseState = ParseState.Start;
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }

                if (BPrefixes.Contains(commandBuffer))
                {
                    parseState = ParseState.ReadingBookAlias;
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }

                if (parseState == ParseState.Start)
                {
                    //movement
                    if (k == 'j' || k == 'k')
                    {
                        return FeedMovement(k);
                    }

                    //mode switching in book state
                    else if (k == 'c')
                    {
                        state = State.C;
                        justANormalStateChange = true;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    }
                    else if (k == 'v')
                    {
                        state = State.V;
                        justANormalStateChange = true;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    }
                    else if (k == ';')
                    {
                        state = State.S;
                        justANormalStateChange = true;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    }
                    else if (k == '/')
                    {
                        BeginSearchMode();
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    }
                }
                return NotValid();

            }

            //mode switching
            if (parseState == ParseState.Start)
            {
                switch (k)
                {
                    case 'b':
                        state = State.B;
                        justANormalStateChange = true;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    case 'c':
                        state = State.C;
                        justANormalStateChange = true;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    case 'v':
                        state = State.V;
                        justANormalStateChange = true;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    case ';':
                        state = State.S;
                        justANormalStateChange = true;
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    case '/':
                        BeginSearchMode();
                        validity = EntireCommandStatus.Valid;
                        return validity;
                    default:
                        break;
                }
            }

            //semi colon mode
            if (state == State.S)
            {
                // Allow Backspace to edit an unfinished verse alias without
                // treating the backspace character as part of the alias.
                if (parseState == ParseState.ReadingSemicolonAlias)
                {
                    if (k == '{')
                    {
                        return BackspaceSemicolonAlias(false);
                    }
                    else if (k == '\u0006')
                    {
                        return BackspaceSemicolonAlias(true);
                    }
                }

                commandBuffer += k;

                //checks if it is calling mark functions
                if (parseState == ParseState.SettingMark)
                {
                    command = new Command { Mode = state, Quantity = (int)k, Direction = Direct.Absolute };
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                else if (parseState == ParseState.CopyingSomething)
                {
                    switch (k)
                    {
                        case 't':
                            return CopyTheTitle();
                        case 'v':
                            return CopyTheVerse();
                        case 'y':
                            return CopyAll();
                        default:
                            return NotValid();
                    }
                }

                //trying to get S-state aliases
                if (_aliasCatalog.TryResolve(commandBuffer, out int position))
                {
                    command = new Command
                    {
                        Mode = State.S,
                        Quantity = position,
                        Direction = Direct.Absolute
                    };

                    commandBuffer = "";
                    parseState = ParseState.Start;
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }

                if (_aliasCatalog.IsPrefix(commandBuffer))
                {
                    parseState = ParseState.ReadingSemicolonAlias;
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }

                //One letter, sub-state switching commands
                if (parseState == ParseState.Start)
                {
                    switch (k)
                    {
                        case 'r':
                            return HistoryCommands(k);
                        case 'z':
                            return HistoryCommands(k);
                        case 'q':
                            parseState = ParseState.SettingMark;
                            validity = EntireCommandStatus.Incomplete;
                            return validity;
                        case 'x':
                            return TranslationChange();
                        case 'e':
                            return ToggleUI();
                        case 'f':
                            return HelpWasRequested();
                        case 'y':
                            parseState = ParseState.CopyingSomething;
                            validity = EntireCommandStatus.Incomplete;
                            return validity;
                        case '+':
                            return BackgroundChange(Direct.Up);
                        case '\u0003':
                            return BackgroundChange(Direct.Down);
                        case '0':
                            return InterfaceChange();
                        default:
                            break;
                    }
                }

                return NotValid();
            }

            //chapter and verse mode
            if (state == State.C || state == State.V)
            {
                return FeedMovement(k);
            }

            //unexpected error has occurred (eg. a user pressed a random key)
            return NotValid();
        }

        private EntireCommandStatus BackgroundChange(Direct direction)
        {
            if (direction == Direct.Up)
            {
                command = new Command { Mode = state, Direction = Direct.Absolute, Quantity = 700 };
            }
            else
            {
                command = new Command { Mode = state, Direction = Direct.Absolute, Quantity = 800 };
            }
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus BackspaceBookAlias(bool controlHeld)
        {
            if (controlHeld)
            {
                // Ctrl+Backspace clears the entire unfinished alias.
                commandBuffer = string.Empty;
                parseState = ParseState.Start;
                commandString = string.Empty;

                validity = EntireCommandStatus.Incomplete;
                return validity;
            }

            // Remove the last character.
            if (commandBuffer.Length > 0)
                commandBuffer = commandBuffer[..^1];

            // Keep the displayed command synchronized with the input.
            if (commandString.Length > 0)
                commandString = commandString[..^1];

            if (commandBuffer.Length == 0)
            {
                parseState = ParseState.Start;
            }
            else if (BPrefixes.Contains(commandBuffer))
            {
                // The shortened prefix is still a valid book prefix.
                parseState = ParseState.ReadingBookAlias;
            }
            else
            {
                // Defensive recovery.
                commandBuffer = string.Empty;
                parseState = ParseState.Start;
                commandString = string.Empty;
            }

            validity = EntireCommandStatus.Incomplete;
            return validity;
        }

        private EntireCommandStatus BackspaceSemicolonAlias(bool controlHeld)
        {
            if (controlHeld)
            {
                commandBuffer = string.Empty;
                parseState = ParseState.Start;
                commandString = string.Empty;
                validity = EntireCommandStatus.Incomplete;
                return validity;
            }
            if (commandBuffer.Length > 0)
                commandBuffer = commandBuffer[..^1];

            // The command display is built as the user types the alias, so
            // keep it in step with the editable alias buffer.
            if (commandString.Length > 0)
                commandString = commandString[..^1];

            if (commandBuffer.Length == 0)
            {
                parseState = ParseState.Start;
            }
            else if (_aliasCatalog.IsPrefix(commandBuffer))
            {
                // Keep alias-suggestion mode active for the shortened prefix.
                parseState = ParseState.ReadingSemicolonAlias;
            }
            else
            {
                // Defensive recovery if the catalogue/parser ever disagree.
                commandBuffer = string.Empty;
                parseState = ParseState.Start;
                commandString = string.Empty;
            }

            validity = EntireCommandStatus.Incomplete;
            return validity;
        }

        private EntireCommandStatus Search(char k)
        {
            // A new search session is exited independently of parser details.
            if (k == '/')
            {
                ExitSearchMode();
                return validity;
            }

            // Moving focus away from a half-entered filter cancels that
            // unfinished token rather than carrying filter parser state into
            // Query or Results mode. Completed filters remain untouched.
            if ((k == '+' || k == '\u0003') &&
                state == State.Filter &&
                (parseState != ParseState.Start || activeFilter != null))
            {
                parseState = ParseState.Start;
                commandBuffer = string.Empty;
                quantifier = 0;
                activeFilter = null;
                commandString = string.Empty;
                NotifySearchDraftVisibilityChanged();
            }

            // Tab cycles through only useful views. Before a search has
            // returned results, Choice is omitted from the cycle entirely.
            if (k == '+')
            {
                justANormalStateChange = true;
                commandString = string.Empty;
                state = state switch
                {
                    State.Query => State.Filter,
                    State.Filter => HasCurrentSearchResults ? State.Choice : State.Query,
                    State.Choice => State.Query,
                    _ => State.Query
                };

                if (state == State.Query)
                    EnterQueryState();
                else
                    NotifySearchDraftVisibilityChanged();

                validity = EntireCommandStatus.Valid;
                return validity;
            }

            // Shift+Tab follows the same available-view rules in reverse.
            if (k == '\u0003')
            {
                commandString = string.Empty;
                justANormalStateChange = true;
                state = state switch
                {
                    State.Filter => State.Query,
                    State.Choice => State.Filter,
                    State.Query => HasCurrentSearchResults ? State.Choice : State.Filter,
                    _ => State.Query
                };

                if (state == State.Query)
                    EnterQueryState();
                else
                    NotifySearchDraftVisibilityChanged();

                validity = EntireCommandStatus.Valid;
                return validity;
            }

            //query state
            if (state == State.Query)
            {
                commandString = "/" + searchQuery;
                if (k == '_')
                {
                    // Query text is already synchronized with the draft.
                    // Enter submits the draft only when it contains an actual
                    // search target. This prevents an empty initial query from
                    // accidentally asking the database for every verse.
                    if (!HasSearchCriteria())
                    {
                        commandString = string.Empty;
                        validity = EntireCommandStatus.Valid;
                        NotifySearchDraftVisibilityChanged();
                        return validity;
                    }

                    state = State.Filter;
                    commandString = string.Empty;
                    return ShipOutProperSearchCommand();
                }
                else if (k == '{')
                {
                    // Backspace is harmless when the query is already empty.
                    if (searchQuery.Length > 0)
                        searchQuery = searchQuery[..^1];

                    SyncQueryIntoSearchDraft();
                    commandString = "/" + searchQuery;
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }
                else if (k == '\u0006')
                {
                    // Ctrl+Backspace removes the previous word. This is also
                    // safe when the query is empty or contains only spaces.
                    searchQuery = DeletePreviousWord(searchQuery);

                    SyncQueryIntoSearchDraft();
                    commandString = "/" + searchQuery;
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }
                else if (k == '<' || k == '>' || k == '|')
                {
                    validity = EntireCommandStatus.Invalid;
                    return validity;
                }
                else
                {
                    searchQuery += k;
                    SyncQueryIntoSearchDraft();
                    commandString = "/" + searchQuery;
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }
            }

            //filter state
            else if (state == State.Filter)
            {
                if (k == '_')
                {
                    // Do not submit while a filter value is half-entered.
                    if (parseState != ParseState.Start || activeFilter != null)
                    {
                        validity = EntireCommandStatus.Incomplete;
                        return validity;
                    }

                    if (!HasSearchCriteria())
                    {
                        validity = EntireCommandStatus.Valid;
                        NotifySearchDraftVisibilityChanged();
                        return validity;
                    }

                    return ShipOutProperSearchCommand();
                }
                //choosing which filter the user wants to change
                if (parseState == ParseState.Start && activeFilter == null)
                {
                    switch (k)
                    {
                        case 'b':
                            parseState = ParseState.FilterBookAlias;
                            activeFilter = k;
                            break;
                        case 'c':
                            parseState = ParseState.FilterQuantifier;
                            activeFilter = k;
                            break;
                        case 'v':
                            parseState = ParseState.FilterQuantifier;
                            activeFilter = k;
                            break;
                        case 't':
                            parseState = ParseState.FilterDeciding;
                            activeFilter = k;
                            break;
                        case 'l':
                            parseState = ParseState.FilterDeciding;
                            activeFilter = k;
                            break;
                        case 'o':
                            parseState = ParseState.FilterDeciding;
                            activeFilter = k;
                            break;
                        case 'm':
                            parseState = ParseState.FilterDeciding;
                            activeFilter = k;
                            break;
                        default:
                            return NotValid();
                    }
                    validity = EntireCommandStatus.Incomplete;
                    NotifySearchDraftVisibilityChanged();
                    return validity;
                }

                //actually editing the filter now
                else if (activeFilter != null)
                {
                    if (parseState == ParseState.FilterBookAlias)
                    {
                        if (k == ' ')
                        {
                            AddFilter("b", " ", true);
                            validity = EntireCommandStatus.Valid;
                            return validity;
                        }
                        commandBuffer += k;

                        if (BCommands.TryGetValue(commandBuffer, out int position))
                        {
                            //adding and/or replacing book filter
                            string filterOption = position.ToString();
                            AddFilter("b", filterOption, false);

                            commandBuffer = "";
                            parseState = ParseState.Start;
                            validity = EntireCommandStatus.Valid;
                            return validity;
                        }

                        if (BPrefixes.Contains(commandBuffer))
                        {
                            validity = EntireCommandStatus.Incomplete;
                            return validity;
                        }
                        activeFilter = null;
                        return NotValid();
                    }
                    else if (parseState == ParseState.FilterQuantifier)
                    {
                        return FinishFilterQuantifier(k, activeFilter.ToString());
                    }
                    else if (parseState == ParseState.FilterDeciding)
                    {
                        switch (activeFilter)
                        {
                            case 't':
                                if (k == 'o' || k == 'n')
                                {
                                    AddFilter(activeFilter.ToString(), k.ToString(), false);
                                    validity = EntireCommandStatus.Valid;
                                    return validity;
                                }
                                else if (k == ' ')
                                {
                                    AddFilter(activeFilter.ToString(), k.ToString(), true);
                                    validity = EntireCommandStatus.Valid;
                                    return validity;
                                }
                                else
                                {
                                    activeFilter = null;
                                    return NotValid();
                                }
                            case 'l':
                                if (k == 'e' || k == 't')
                                {
                                    AddFilter(activeFilter.ToString(), k.ToString(), false);
                                    validity = EntireCommandStatus.Valid;
                                    return validity;
                                }
                                else
                                {
                                    activeFilter = null;
                                    return NotValid();
                                }
                            case 'o':
                                if (k == 'a' || k == 's')
                                {
                                    AddFilter(activeFilter.ToString(), k.ToString(), false);
                                    validity = EntireCommandStatus.Valid;
                                    return validity;
                                }
                                else
                                {
                                    activeFilter = null;
                                    return NotValid();
                                }
                            case 'm':
                                if (k == 'f' || k == 't')
                                {
                                    AddFilter(activeFilter.ToString(), k.ToString(), false);
                                    validity = EntireCommandStatus.Valid;
                                    return validity;
                                }
                                else
                                {
                                    activeFilter = null;
                                    return NotValid();
                                }
                        }
                    }
                    else
                    {
                        activeFilter = null;
                        validity = EntireCommandStatus.Invalid;
                        return validity;
                    }
                }
                activeFilter = null;
                return NotValid();
            }

            //choice mode
            else if (state == State.Choice)
            {
                //commandString = "*" + choiceCommandBuffer;

                if (normalChoosingMode)
                {
                    if (parseState != ParseState.Start && parseState != ParseState.ReadingQuantifier)
                    {
                        if (k == 'g')
                        {
                            command = new Command { Mode = state, Direction = Direct.Up, Quantity = 0 };
                            validity = EntireCommandStatus.Valid;
                            return validity;
                        }
                        return NotValid();
                    }

                    switch (k)
                    {
                        case '_':
                            // Defensive guard: never manufacture a selection
                            // when the current search has no result rows.
                            if (HasCurrentSearchResults)
                                FoundTheSearchedVerse();

                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case '|':
                            validity = EntireCommandStatus.Incomplete;
                            return validity;

                        case '<':
                            command = new Command { Mode = state, Direction = Direct.Down, Quantity = 20 };
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case '>':
                            command = new Command { Mode = state, Direction = Direct.Up, Quantity = 20 };
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case '\u0001':
                            command = new Command { Mode = state, Direction = Direct.Down, Quantity = 40 };
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case '\u0002':
                            command = new Command { Mode = state, Direction = Direct.Up, Quantity = 40 };
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case 'g':
                            parseState = ParseState.FilterBookAlias;
                            commandString += k;
                            validity = EntireCommandStatus.Incomplete;
                            return validity;

                        case 'G':
                            command = new Command { Mode = state, Direction = Direct.Down, Quantity = 0 };
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case 'i':
                            normalChoosingMode = false;
                            commandString = "*" + choiceCommandBuffer;
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        case 'c':
                            normalChoosingMode = false;
                            choiceCommandBuffer = "";
                            commandString = "*";
                            validity = EntireCommandStatus.Valid;
                            return validity;

                        default:
                            return FeedMovement(k);
                    }
                }
                else
                {

                    //filtering out choices mode
                    switch (k)
                    {
                        case '|':
                            normalChoosingMode = true;
                            validity = EntireCommandStatus.Valid;
                            return validity;
                        case '<':
                        case '>':
                            break;
                        case '{':
                            if (choiceCommandBuffer.Length == 0)
                            {
                                // Do not submit an unnecessary empty command.
                                commandString = "*";
                                validity = EntireCommandStatus.Incomplete;
                                return validity;
                            }

                            choiceCommandBuffer = choiceCommandBuffer[..^1];
                            break;

                        case '\u0006':
                            if (choiceCommandBuffer.Length == 0)
                            {
                                // Ctrl+Backspace on an empty absolute path is
                                // a no-op, not an empty search submission.
                                commandString = "*";
                                validity = EntireCommandStatus.Incomplete;
                                return validity;
                            }

                            choiceCommandBuffer = DeletePreviousWord(choiceCommandBuffer);
                            break;

                        default:
                            choiceCommandBuffer += k;
                            break;
                    }
                    return ShipOutChoiceCommand(choiceCommandBuffer);
                }
            }
            else
            {
                activeFilter = null;
                return NotValid();
            }
        }

        private EntireCommandStatus FinishFilterQuantifier(char k, string whichFilter)
        {
            {
                if (quantifier == 0 && k == ' ')
                {
                    AddFilter(activeFilter.ToString(), " ", true);
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                //user (may have) typed an alias to a number
                if (Quantifiers.TryGetValue(k, out int digit))
                {
                    quantifier = (quantifier) * 10 + digit;
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }

                //user typed an actual number
                if (char.IsDigit(k))
                {
                    quantifier = (quantifier) * 10 + (int)char.GetNumericValue(k);
                    validity = EntireCommandStatus.Incomplete;
                    return validity;
                }

                //must be a movement-terminated command
                //if (quantifier == 0) { quantifier = 1; }

                if (k == 'j' || k == 'k' || k == ' ')
                {
                    string quantifierString = quantifier.ToString();
                    string optionString = quantifierString + k.ToString();

                    AddFilter(whichFilter, optionString, false);
                    validity = EntireCommandStatus.Valid;
                    return validity;
                }
                else
                {
                    activeFilter = null;
                    return NotValid();
                }
            }
        }

        private void AddFilter(string whichFilter, string option, bool weAreDeleting)
        {
            string[] filterParts = wholeQueryWithCommas.Split(',');
            string newFilter = whichFilter + option;
            int filterIndex;
            switch (whichFilter)
            {
                case "b":
                    filterIndex = 1;
                    break;
                case "c":
                    filterIndex = 2;
                    break;
                case "v":
                    filterIndex = 3;
                    break;
                case "t":
                    filterIndex = 4;
                    break;
                case "l":
                    filterIndex = 5;
                    break;
                case "o":
                    filterIndex = 6;
                    break;
                case "m":
                    filterIndex = 7;
                    break;
                default:
                    filterIndex = 0;
                    break;
            }
            if (weAreDeleting)
            {
                filterParts[filterIndex] = "";
            }
            else
            {
                filterParts[filterIndex] = newFilter;
            }
            activeFilter = null;
            parseState = ParseState.Start;
            commandBuffer = string.Empty;
            quantifier = 0;
            wholeQueryWithCommas = string.Join(',', filterParts);
            NotifySearchDraftVisibilityChanged();
            AFilterWasChanged();
        }

        private EntireCommandStatus HistoryCommands(char k)
        {
            if (k == 'r')
            {
                command = new Command { Mode = state, Quantity = 0, Direction = Direct.Up };
                validity = EntireCommandStatus.Valid;
                return validity;
            }
            if (k == 'z')
            {
                command = new Command { Mode = state, Quantity = 0, Direction = Direct.Down };
                validity = EntireCommandStatus.Valid;
                return validity;
            }
            validity = EntireCommandStatus.Invalid;
            return validity;
        }

        private EntireCommandStatus FeedMovement(char k)
        {
            if (k == '.' && parseState != ParseState.ReadingQuantifier)
            {
                quantifier = previousQuantifier;
                return CompleteMovement(Direct.Absolute);
            }

            if (parseState == ParseState.Start && FastQuantifiers.TryGetValue(k, out int num))
            {
                quantifier = num;
                previousQuantifier = num;
                return CompleteMovement(Direct.Absolute);
            }

            //user (may have) typed an alias to a number
            if (Quantifiers.TryGetValue(k, out int digit))
            {
                quantifier = (quantifier) * 10 + digit;
                if (quantifier > 1000)
                {
                    return NotValid();
                }
                previousQuantifier = quantifier;
                parseState = ParseState.ReadingQuantifier;
                validity = EntireCommandStatus.Incomplete;
                return validity;
            }

            //user typed an actual number
            if (char.IsDigit(k))
            {
                quantifier = (quantifier) * 10 + (int)char.GetNumericValue(k);
                if (quantifier > 1000)
                {
                    return NotValid();
                }
                previousQuantifier = quantifier;
                parseState = ParseState.ReadingQuantifier;
                validity = EntireCommandStatus.Incomplete;
                return validity;
            }

            //must be a movement-terminated command
            if (quantifier == 0) { quantifier = 1; }

            if (k == 'j')
            {
                return CompleteMovement(Direct.Down);
            }
            if (k == 'k')
            {
                return CompleteMovement(Direct.Up);
            }

            //shouldn't reach here
            return NotValid();
        }

        private EntireCommandStatus CompleteMovement(Direct direction)
        {
            if (quantifier != 0 && quantifier <= 200)
            {
                command = new Command
                {
                    Mode = state,
                    Quantity = quantifier,
                    Direction = direction
                };

                validity = EntireCommandStatus.Valid;
                return validity;
            }

            return NotValid();
        }

        private EntireCommandStatus TranslationChange()
        {
            command = new Command
            {
                Mode = state,
                Quantity = 200,
                Direction = Direct.Absolute
            };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus ToggleUI()
        {
            command = new Command { Mode = state, Quantity = 300, Direction = Direct.Absolute };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus HelpWasRequested()
        {
            command = new Command { Mode = state, Quantity = 400, Direction = Direct.Absolute };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus CopyTheVerse()
        {
            command = new Command { Mode = state, Quantity = 500, Direction = Direct.Absolute };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus CopyTheTitle()
        {
            command = new Command { Mode = state, Quantity = 600, Direction = Direct.Absolute };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus CopyAll()
        {
            command = new Command { Mode = state, Quantity = 900, Direction = Direct.Absolute };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private EntireCommandStatus InterfaceChange()
        {
            command = new Command { Mode = state, Quantity = 1000, Direction = Direct.Absolute };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private void AFilterWasChanged()
        {
            // Quantity 1 refreshes the filter model, not the search results.
            command = new Command { Mode = State.Query, Direction = Direct.Absolute, Quantity = 1, SearchText = wholeQueryWithCommas };
        }

        private EntireCommandStatus ShipOutProperSearchCommand()
        {
            // Capture the source phrase before the structured search draft is
            // handed to the database. MainWindow must never parse SearchText
            // to reconstruct the user's original input.
            submittedSearchPhrase = searchQuery;
            command = new Command { Mode = State.Query, Quantity = 0, Direction = Direct.Absolute, SearchText = wholeQueryWithCommas };
            lastSubmittedSearch = wholeQueryWithCommas;
            parseState = ParseState.Start;
            commandBuffer = string.Empty;
            quantifier = 0;
            activeFilter = null;
            NotifySearchDraftVisibilityChanged();
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        private void SyncQueryIntoSearchDraft()
        {
            string[] filterParts = wholeQueryWithCommas.Split(',');
            if (filterParts.Length != 8)
            {
                throw new InvalidOperationException(
                    "The search draft must contain exactly eight fields.");
            }

            filterParts[0] = searchQuery;
            wholeQueryWithCommas = string.Join(',', filterParts);
            NotifySearchDraftVisibilityChanged();
        }

        private void NotifySearchDraftVisibilityChanged()
        {
            bool canSubmitFromCurrentState =
                (state == State.Query || state == State.Filter) &&
                parseState == ParseState.Start &&
                activeFilter == null;

            bool hasUnsubmittedChanges = !string.Equals(
                wholeQueryWithCommas,
                lastSubmittedSearch,
                StringComparison.Ordinal);

            PressEnterVisibilityChanged?.Invoke(
                canSubmitFromCurrentState && hasUnsubmittedChanges);
        }

        // Leaves Query / Filter / Choice mode through the same cleanup path
        // used by pressing '/'. MainWindow uses this when it consumes a special
        // submitted search so the parser, rather than WPF, restores its prior mode.
        public void ExitSearchMode()
        {
            if (state != State.Query && state != State.Filter && state != State.Choice)
                return;

            justANormalStateChange = true;
            state = previousStateBeforeSearch;
            ResetCommand();
            choiceCommandBuffer = string.Empty;
            normalChoosingMode = true;
            activeFilter = null;
            commandString = string.Empty;
            searchResultsAvailable = false;
            submittedSearchPhrase = null;
            NotifySearchDraftVisibilityChanged();
            SearchUiVisibilityChanged?.Invoke(false);
            validity = EntireCommandStatus.Valid;
        }

        private void BeginSearchMode()
        {
            previousStateBeforeSearch = state;
            state = State.Query;
            justANormalStateChange = true;

            // Results belong to the previous mode/session and must not be
            // navigable until this session submits a search of its own.
            searchResultsAvailable = false;
            choiceCommandBuffer = string.Empty;
            normalChoosingMode = true;

            EnterQueryState();
            SearchUiVisibilityChanged?.Invoke(true);
        }

        private void EnterQueryState()
        {
            // Query mode starts with no unfinished filter token. This method
            // is also used when tabbing back to Query, so it must not reopen
            // or reset the current search session's result availability.
            parseState = ParseState.Start;
            commandBuffer = string.Empty;
            quantifier = 0;
            activeFilter = null;
            commandString = "/" + searchQuery;
            SyncQueryIntoSearchDraft();
        }

        public void SetSearchResultsAvailable(bool available)
        {
            searchResultsAvailable = available;
        }

        // A result list is selectable only while it still corresponds to the
        // exact draft that produced it. Editing and then reverting a draft
        // makes the previous result set current again without a new search.
        private bool HasCurrentSearchResults =>
            searchResultsAvailable &&
            string.Equals(
                wholeQueryWithCommas,
                lastSubmittedSearch,
                StringComparison.Ordinal);

        public bool SearchResultsAvailable => HasCurrentSearchResults;

        private bool HasSearchCriteria()
        {
            string[] parts = wholeQueryWithCommas.Split(',');

            // Phrase plus book/chapter/verse/testament are the fields that
            // actually identify content. Language/order/case alone do not.
            return parts.Take(5).Any(part => !string.IsNullOrWhiteSpace(part));
        }

        private void FoundTheSearchedVerse()
        {
            command = new Command { Mode = State.Choice, Direction = Direct.Absolute, Quantity = 0 };
        }

        private EntireCommandStatus ShipOutChoiceCommand(string searchText)
        {
            commandString = "*" + searchText;
            command = new Command { Mode = state, Direction = Direct.Absolute, Quantity = 1, SearchText = searchText };
            validity = EntireCommandStatus.Valid;
            return validity;
        }

        public void SelectNavigationMode(State target)
        {
            if (target != State.B && target != State.C &&
                target != State.V && target != State.S)
            {
                throw new ArgumentOutOfRangeException(nameof(target));
            }

            // Re-selecting semicolon mode is a no-op so clicking its label
            // cannot reset an unfinished semicolon command. Preserve the
            // original B/C/V mode-click behavior.
            if (state == State.S && target == State.S)
                return;

            bool leavingSearch =
                state == State.Query ||
                state == State.Filter ||
                state == State.Choice;

            bool leavingMarkSelection =
                parseState == ParseState.TeleportingToMark;

            if (leavingMarkSelection)
                ShowMarks?.Invoke(false);

            state = target;
            previousStateBeforeSearch = target;

            // Cancel unfinished input.
            ResetCommand();
            choiceCommandBuffer = string.Empty;
            activeFilter = null;
            commandString = string.Empty;

            if (leavingSearch)
            {
                NotifySearchDraftVisibilityChanged();
                SearchUiVisibilityChanged?.Invoke(false);
            }

            validity = EntireCommandStatus.Valid;
            justANormalStateChange = true;
        }
    }
}
