using navigationshurely.models;
using BibleApp.Data;
using navigationshurely.services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Windows.Media;
using System.DirectoryServices;


namespace navigationshurely.Services;

public class BibleNavigator
{
    private readonly BibleDatabase _database;
    private List<Verse> History;
    private int indexVisited;
    private int maxUndos = 100;
    private List<Verse> locatedVerses;
    private List<Verse> filteringListedVerses;
    private int choiceVerseIndex = 0;
    private SearchRequest? userRequest;
    private bool isGlobalUiVisible;
    private string searchQuery;
    private string searchPortion;

    public event Action? HelpRequested;
    public event Action? CopyVerseText;
    public event Action? CopyVerseName;
    public event Action? DisplaySearchResults;
    public event Action? FiltersHaveChanged;
    public event Action<bool>? SearchUiVisibilityChanged;
    public event Action<bool>? PressEnterVisibilityChange;
    public event Action? UserScrolledSomewhere;
    public event Action? ChangeBackgroundPlease;

    private EntireCommandStatus validityCheck;
    public Verse CurrentVerse { get; private set; }

    private Dictionary<int, Verse> markedVerses;
    public BibleNavigator(BibleDatabase database)
    {
        _database = database;

        markedVerses = new Dictionary<int, Verse>();
        CurrentVerse = _database.GetVerse(1, 1, 1)
            ?? throw new InvalidOperationException(
                "Could not load Genesis 1:1.");
        History = [CurrentVerse];
        validityCheck = EntireCommandStatus.Valid;
        isGlobalUiVisible = true;
        searchQuery = string.Empty;
    }

    //==========================================================================================================================
    //                                                 ACTUAL NAVIGATION METHODS!
    //==========================================================================================================================


    // =========================================================
    // COMMAND PROCESSING
    // =========================================================

    public void ProcessCommand(Command command)
    {
        validityCheck = EntireCommandStatus.Valid;
        switch (command.Mode)
        {
            case State.V:
                ProcessVerseCommand(command);
                break;

            case State.C:
                ProcessChapterCommand(command);
                break;

            case State.B:
                ProcessBookCommand(command);
                break;

            case State.S:
                ProcessSemicolonCommand(command);
                break;

            case State.Choice:
                ProcessChoiceCommand(command);
                break;

            default:
                ProcessSearchCommand(command);
                break;
        }
    }

    private void ProcessVerseCommand(Command command)
    {
        switch (command.Direction)
        {
            case Direct.Down:
                NextVerse(command.Quantity);
                break;

            case Direct.Up:
                PreviousVerse(command.Quantity);
                break;

            case Direct.Absolute:
                GoTo(CurrentVerse.BookId, CurrentVerse.Chapter, command.Quantity);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(command.Direction));
        }
    }

    private void ProcessChapterCommand(Command command)
    {
        switch (command.Direction)
        {
            case Direct.Down:
                NextChapter(command.Quantity);
                break;

            case Direct.Up:
                PreviousChapter(command.Quantity);
                break;

            case Direct.Absolute:
                GoTo(CurrentVerse.BookId, command.Quantity, CurrentVerse.VerseNumber);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(command.Direction));
        }
    }

    private void ProcessBookCommand(Command command)
    {
        switch (command.Direction)
        {
            case Direct.Down:
                NextBook(command.Quantity);
                break;

            case Direct.Up:
                PreviousBook(command.Quantity);
                break;

            case Direct.Absolute:
                GoTo(command.Quantity, CurrentVerse.Chapter, CurrentVerse.VerseNumber);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(command.Direction));
        }
    }

    private void ProcessSearchCommand(Command command)
    {
        string search = command.SearchText;
        if (string.IsNullOrWhiteSpace(search))
            return;

        userRequest = ParseSearch(search);
        FiltersHaveChanged?.Invoke();

        if (command.Quantity == 0)
        {
            AbsoluteSearch(search);
        }
        else
        {
            PressEnterVisibilityChange?.Invoke(true);
        }
    }

    private void ProcessSemicolonCommand(Command command)
    {
        switch (command.Direction)
        {
            case Direct.Down:
                if (command.Quantity == 0)
                    BackInTime();
                break;

            case Direct.Up:
                if (command.Quantity == 0)
                    ForwardInTime();
                break;

            case Direct.Absolute:
                switch (command.Quantity)
                {
                    case 200:
                        TranslationChange();
                        break;

                    case 300:
                        SearchUiVisibilityChanged?.Invoke(isGlobalUiVisible);
                        isGlobalUiVisible = !isGlobalUiVisible;
                        break;

                    case 400:
                        // KeepCurrentVerse();
                        HelpRequested?.Invoke();
                        break;

                    case 500:
                        CopyVerseText?.Invoke();
                        break;

                    case 600:
                        CopyVerseName?.Invoke();
                        break;

                    case 700:
                        ChangeBackgroundPlease?.Invoke();
                        break;

                    default:
                        if (command.Quantity > 127)
                            AliasPosition(command.Quantity);
                        else if (command.Quantity > 0)
                            SetMarkVerse(command.Quantity);
                        else if (command.Quantity < 0)
                            TeleportToMarkVerse(command.Quantity);
                        break;
                }
                break;

            default:
                break;
        }
    }

    private void ProcessChoiceCommand(Command command)
    {
        switch (command.Direction)
        {
            case Direct.Absolute:
                if (command.Quantity == 0)
                {
                    ChoiceMade();
                }
                else
                {
                    searchPortion = command.SearchText;
                    UpdateFilteredVerses();
                    DisplaySearchResults?.Invoke();
                    UserScrolledSomewhere?.Invoke();
                }
                break;
            case Direct.Up:
                if (command.Quantity == 0)
                {
                    GCommand(false);
                    break;
                }
                else
                {
                    PanUpAndDown(command.Direction, command.Quantity);
                    break;
                }
            case Direct.Down:
                if (command.Quantity == 0)
                {
                    GCommand(true);
                    break;
                }
                else
                {
                    PanUpAndDown(command.Direction, command.Quantity);
                    break;
                }
        }
    }

    //==========================================================================================================================
    //                                                 HELPERS
    //==========================================================================================================================

    public int VerseEncodedNumber()
    {
        int bookPart = CurrentVerse.BookId;
        int chapterPart = CurrentVerse.Chapter;
        int versePart = CurrentVerse.VerseNumber;
        return bookPart * 1000000 + chapterPart * 1000 + versePart;
    }


    // =========================================================
    // SEARCH FUNCTIONALITY
    // =========================================================
    private void AbsoluteSearch(string? search)
    {
        PressEnterVisibilityChange?.Invoke(false);
        locatedVerses = _database.Search(userRequest);
        filteringListedVerses = locatedVerses;
        searchPortion = "";
        choiceVerseIndex = 0;

        if (filteringListedVerses.Count != 0)
        {
            filteringListedVerses[choiceVerseIndex].Selected = true;
        }
        DisplaySearchResults?.Invoke();
        UserScrolledSomewhere?.Invoke();
    }

    private void ChoiceMade()
    {
        Verse verse = filteringListedVerses[choiceVerseIndex];
        GoTo(verse.BookId, verse.Chapter, verse.VerseNumber);
    }

    private void PanUpAndDown(Direct direction, int amount)
    {
        if (filteringListedVerses.Count == 0)
            return;

        if (direction == Direct.Up)
            amount *= -1;

        int newIndex = Math.Clamp(
            choiceVerseIndex + amount,
            0,
            filteringListedVerses.Count - 1);

        // No movement means no updates necessary.
        if (newIndex == choiceVerseIndex)
            return;

        // Deselect the old verse.
        filteringListedVerses[choiceVerseIndex].Selected = false;

        // Update the navigator's selection.
        choiceVerseIndex = newIndex;

        // Select the new verse.
        filteringListedVerses[choiceVerseIndex].Selected = true;

        Verse justForTestingVerse = filteringListedVerses[choiceVerseIndex];
        // Tell MainWindow to scroll the selected verse into view.
        UserScrolledSomewhere?.Invoke();
    }

    private void GCommand(bool isItLast)
    {
        if (filteringListedVerses.Count == 0)
            return;

        filteringListedVerses[choiceVerseIndex].Selected = false;

        if (isItLast)
        {
            choiceVerseIndex = filteringListedVerses.Count - 1;
        }
        else
        {
            choiceVerseIndex = 0;
        }

        // Select the new verse.
        filteringListedVerses[choiceVerseIndex].Selected = true;

        // Tell MainWindow to scroll the selected verse into view.
        UserScrolledSomewhere?.Invoke();

    }

    private void UpdateFilteredVerses()
    {
        filteringListedVerses = string.IsNullOrEmpty(searchPortion)
            ? locatedVerses
            : locatedVerses
                .Where(verse => verse.ToString().Contains(
                    searchPortion,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

        choiceVerseIndex = 0;

        foreach (Verse verse in locatedVerses)
        {
            verse.Selected = false;
        }

        if (filteringListedVerses.Count > 0)
        {
            filteringListedVerses[0].Selected = true;
        }
    }

    public List<Verse> LocatedVerses()
    {
        return filteringListedVerses;
    }

    public string ActualSearchQuery()
    {
        return searchQuery;
    }

    public int SelectedSearchIndex()
    {
        return choiceVerseIndex;
    }

    private SearchRequest ParseSearch(string search)
    {
        string[] parts = search.Split(
            ',',
            StringSplitOptions.None
        );

        if (parts.Length != 8)
            throw new ArgumentException(
                "Invalid search command format."
            );

        searchQuery = parts[0];
        SearchRequest request = new()
        {
            SearchText = searchQuery
        };

        ParseBook(parts[1], request);
        ParseChapter(parts[2], request);
        ParseVerse(parts[3], request);
        ParseTestament(parts[4], request);
        ParseLanguage(parts[5], request);
        ParseOrder(parts[6], request);
        ParseCase(parts[7], request);

        return request;
    }

    private void ParseCase(string value, SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            request.CaseSensitive = false;
            return;
        }

        switch (value)
        {
            case "mt":
                request.CaseSensitive = true;
                break;

            case "mf":
                request.CaseSensitive = false;
                break;

            default:
                throw new ArgumentException(
                    $"Invalid case-sensitivity filter: {value}"
                );
        }
    }

    private void ParseBook(string value, SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!value.StartsWith('b'))
            throw new ArgumentException(
                $"Invalid book filter: {value}"
            );

        if (!int.TryParse(value[1..], out int bookId))
            throw new ArgumentException(
                $"Invalid book ID: {value}"
            );

        request.BookId = bookId;
    }

    private void ParseChapter(string value, SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!value.StartsWith('c'))
            throw new ArgumentException(
                $"Invalid chapter filter: {value}"
            );

        string numberPart = value[1..];

        char direction = '\0';

        if (numberPart.EndsWith('j') ||
            numberPart.EndsWith('k'))
        {
            direction = numberPart[^1];
            numberPart = numberPart[..^1];
        }

        if (!int.TryParse(numberPart, out int amount))
            throw new ArgumentException(
                $"Invalid chapter value: {value}"
            );

        request.Chapter = ResolveRelativeValue(
            amount,
            direction,
            CurrentVerse.Chapter
        );
    }

    private void ParseVerse(string value, SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!value.StartsWith('v'))
            throw new ArgumentException(
                $"Invalid verse filter: {value}"
            );

        string numberPart = value[1..];

        char direction = '\0';

        if (numberPart.EndsWith('j') ||
            numberPart.EndsWith('k'))
        {
            direction = numberPart[^1];
            numberPart = numberPart[..^1];
        }

        if (!int.TryParse(numberPart, out int amount))
            throw new ArgumentException(
                $"Invalid verse value: {value}"
            );

        request.Verse = ResolveRelativeValue(
            amount,
            direction,
            CurrentVerse.VerseNumber
        );
    }

    private static int ResolveRelativeValue(int amount, char direction, int currentValue)
    {
        return direction switch
        {
            '\0' => amount,
            'j' => currentValue + amount,
            'k' => currentValue - amount,
            _ => throw new ArgumentException(
                $"Invalid direction: {direction}"
            )
        };
    }

    private void ParseLanguage(string value, SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            request.LanguageSpecified = false;
            return;
        }

        request.LanguageSpecified = true;
        request.TranslationId = value switch
        {
            "le" => 2,
            "lt" => 1,
            _ => throw new ArgumentException(
                $"Invalid language filter: {value}"
            )
        };
    }

    private void ParseTestament(string value, SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (value == "to")
        {
            request.Testament = Testament.Old;
            return;
        }

        if (value == "tn")
        {
            request.Testament = Testament.New;
            return;
        }

        throw new ArgumentException(
            $"Invalid testament filter: {value}"
        );
    }

    private void ParseOrder(string value, SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            request.SearchMode = SearchMode.AnyOrder;
            return;
        }

        request.SearchMode = value switch
        {
            "oa" => SearchMode.AnyOrder,
            "os" => SearchMode.StrictOrder,

            _ => throw new ArgumentException(
                $"Invalid search order: {value}"
            )
        };
    }

    // =========================================================
    // SEARCH FILTER DISPLAYS
    // =========================================================

    public List<string> GetActiveSearchFilters()
    {
        List<string> filters = new();

        if (userRequest == null)
            return filters;

        if (userRequest.BookId.HasValue)
        {
            Verse verse = new Verse { BookId = userRequest.BookId.Value };
            filters.Add(
                $"Book: {verse.Title()}"
            );
        }

        if (userRequest.Chapter.HasValue)
        {
            filters.Add(
                $"Chapter: {userRequest.Chapter.Value}"
            );
        }

        if (userRequest.Verse.HasValue)
        {
            filters.Add(
                $"Verse: {userRequest.Verse.Value}"
            );
        }

        if (userRequest.Testament.HasValue)
        {
            filters.Add(
                userRequest.Testament.Value == Testament.Old
                    ? "Testament: Old"
                    : "Testament: New"
            );
        }
        filters.Add(
            userRequest.TranslationId switch
            {
                1 => "Language: Tagalog",
                2 => "Language: English",
                _ => $"Language: {userRequest.TranslationId}"
            }
        );

        filters.Add(
            userRequest.SearchMode switch
            {
                SearchMode.AnyOrder => "Order: Any",
                SearchMode.StrictOrder => "Order: Strict",
                _ => $"Order: {userRequest.SearchMode}"
            }
        );

        filters.Add(
            userRequest.CaseSensitive
            ? "Match-case: True"
            : "Match-case: False"
            );
        return filters;
    }


    // =========================================================
    // VERSE NAVIGATION
    // =========================================================

    private void NextVerse(int amount = 1)
    {
        if (amount < 0)
        {
            PreviousVerse(-amount);
            return;
        }

        var chapters = _database.GetChapters();

        int chapterIndex = chapters.IndexOf(
            (CurrentVerse.BookId, CurrentVerse.Chapter));

        if (chapterIndex == -1)
            throw new InvalidOperationException(
                "Current chapter does not exist in the database.");

        int currentVerse = CurrentVerse.VerseNumber;

        for (int i = 0; i < amount; i++)
        {
            int maxVerse = _database.GetMaxVerse(
                CurrentVerse.BookId,
                CurrentVerse.Chapter);

            if (currentVerse < maxVerse)
            {
                currentVerse++;
            }
            else
            {
                // End of chapter → next chapter
                chapterIndex++;

                // End of Bible → wrap to beginning
                if (chapterIndex >= chapters.Count)
                    chapterIndex = 0;

                var nextChapter = chapters[chapterIndex];

                CurrentVerse = _database.GetVerse(
                    nextChapter.BookId,
                    nextChapter.Chapter,
                    1)!;

                currentVerse = 1;
                continue;
            }

            CurrentVerse = _database.GetVerse(
                CurrentVerse.BookId,
                CurrentVerse.Chapter,
                currentVerse)!;
        }
        Visit(CurrentVerse);
    }

    private void PreviousVerse(int amount = 1)
    {
        if (amount < 0)
        {
            NextVerse(-amount);
            return;
        }

        var chapters = _database.GetChapters();

        int chapterIndex = chapters.IndexOf(
            (CurrentVerse.BookId, CurrentVerse.Chapter));

        if (chapterIndex == -1)
            throw new InvalidOperationException(
                "Current chapter does not exist in the database.");

        int currentVerse = CurrentVerse.VerseNumber;

        for (int i = 0; i < amount; i++)
        {
            if (currentVerse > 1)
            {
                currentVerse--;
            }
            else
            {
                // Beginning of chapter → previous chapter
                chapterIndex--;

                // Beginning of Bible → wrap to end
                if (chapterIndex < 0)
                    chapterIndex = chapters.Count - 1;

                var previousChapter = chapters[chapterIndex];

                int maxVerse = _database.GetMaxVerse(
                    previousChapter.BookId,
                    previousChapter.Chapter);

                CurrentVerse = _database.GetVerse(
                    previousChapter.BookId,
                    previousChapter.Chapter,
                    maxVerse)!;

                currentVerse = maxVerse;
                continue;
            }

            CurrentVerse = _database.GetVerse(
                CurrentVerse.BookId,
                CurrentVerse.Chapter,
                currentVerse)!;
        }
        Visit(CurrentVerse);
    }


    // =========================================================
    // CHAPTER NAVIGATION
    // =========================================================

    private void NextChapter(int amount = 1)
    {
        if (amount < 0)
        {
            PreviousChapter(-amount);
            return;
        }

        var chapters = _database.GetChapters();

        int index = chapters.IndexOf(
            (CurrentVerse.BookId, CurrentVerse.Chapter));

        if (index == -1)
            throw new InvalidOperationException(
                "Current chapter does not exist in the database.");

        index = WrapIndex(index + amount, chapters.Count);

        var destination = chapters[index];

        int maxVerse = _database.GetMaxVerse(
            destination.BookId,
            destination.Chapter);

        int verse = Math.Min(
            CurrentVerse.VerseNumber,
            maxVerse);

        CurrentVerse = _database.GetVerse(
            destination.BookId,
            destination.Chapter,
            verse)!;

        Visit(CurrentVerse);
    }

    private void PreviousChapter(int amount = 1)
    {
        if (amount < 0)
        {
            NextChapter(-amount);
            return;
        }

        var chapters = _database.GetChapters();

        int index = chapters.IndexOf(
            (CurrentVerse.BookId, CurrentVerse.Chapter));

        if (index == -1)
            throw new InvalidOperationException(
                "Current chapter does not exist in the database.");

        index = WrapIndex(index - amount, chapters.Count);

        var destination = chapters[index];

        int maxVerse = _database.GetMaxVerse(
            destination.BookId,
            destination.Chapter);

        int verse = Math.Min(
            CurrentVerse.VerseNumber,
            maxVerse);

        CurrentVerse = _database.GetVerse(
            destination.BookId,
            destination.Chapter,
            verse)!;

        Visit(CurrentVerse);
    }


    // =========================================================
    // BOOK NAVIGATION
    // =========================================================

    private void NextBook(int amount = 1)
    {
        if (amount < 0)
        {
            PreviousBook(-amount);
            return;
        }

        var books = _database.GetBookIds();

        int index = books.IndexOf(CurrentVerse.BookId);

        if (index == -1)
            throw new InvalidOperationException(
                "Current book does not exist in the database.");

        index = WrapIndex(index + amount, books.Count);

        int destinationBookId = books[index];

        SetBookPosition(
            destinationBookId,
            CurrentVerse.Chapter,
            CurrentVerse.VerseNumber);
    }

    private void PreviousBook(int amount = 1)
    {
        if (amount < 0)
        {
            NextBook(-amount);
            return;
        }

        var books = _database.GetBookIds();

        int index = books.IndexOf(CurrentVerse.BookId);

        if (index == -1)
            throw new InvalidOperationException(
                "Current book does not exist in the database.");

        index = WrapIndex(index - amount, books.Count);

        int destinationBookId = books[index];

        SetBookPosition(
            destinationBookId,
            CurrentVerse.Chapter,
            CurrentVerse.VerseNumber);
    }


    // =========================================================
    // ABSOLUTE NAVIGATION
    // =========================================================

    private void GoTo(int bookId, int chapter, int verse)
    {
        int maxChapter = _database.GetMaxChapter(bookId);

        if (maxChapter == 0)
            throw new ArgumentException(
                $"Book {bookId} does not exist.");

        chapter = Math.Clamp(
            chapter,
            1,
            maxChapter);

        int maxVerse = _database.GetMaxVerse(
            bookId,
            chapter);

        verse = Math.Clamp(
            verse,
            1,
            maxVerse);

        CurrentVerse = _database.GetVerse(
            bookId,
            chapter,
            verse)!;

        Visit(CurrentVerse);
    }

    // =========================================================
    // VISITED VERSES
    // =========================================================

    private void BackInTime()
    {
        if (indexVisited <= 0)
        {
            return;
        }
        indexVisited--;
        CurrentVerse = History[indexVisited];
    }

    private void ForwardInTime()
    {
        if (indexVisited >= History.Count - 1) 
        {
            return;
        }
        indexVisited++;
        CurrentVerse = History[indexVisited];
    }

    private void Visit(Verse verse)
    {
        if (indexVisited < History.Count - 1)
        {
            History.RemoveRange(indexVisited + 1, History.Count - indexVisited - 1);
        }

        History.Add(verse);
        while (History.Count > maxUndos)
        {
            History.RemoveAt(0);
            indexVisited--;
        }
        indexVisited++;
    }

    // =========================================================
    // INTERNAL HELPERS
    // =========================================================

    private void SetBookPosition(int bookId, int requestedChapter, int requestedVerse)
    {
        int maxChapter = _database.GetMaxChapter(bookId);

        int chapter = Math.Min(
            requestedChapter,
            maxChapter);

        int maxVerse = _database.GetMaxVerse(
            bookId,
            chapter);

        int verse = Math.Min(
            requestedVerse,
            maxVerse);

        CurrentVerse = _database.GetVerse(
            bookId,
            chapter,
            verse)!;

        Visit(CurrentVerse);
    }

    private void AliasPosition(int reference)
    {
        int book = reference / 1_000_000;
        int chapter = (reference / 1_000) % 1_000;
        int verse = reference % 1_000;

        CurrentVerse = _database.GetVerse(book, chapter, verse)!;
        Visit(CurrentVerse);
    }

    private static int WrapIndex(int index, int count)
    {
        return ((index % count) + count) % count;
    }

    private void SetMarkVerse(int character)
    {
        markedVerses[character] = CurrentVerse;
    }

    public Color CommandColor()
    {
        switch (validityCheck)
        {
            case EntireCommandStatus.Incomplete:
                return Color.FromRgb(85, 200, 208);
            case EntireCommandStatus.Invalid:
                return Color.FromRgb(255, 107, 120);
            case EntireCommandStatus.Valid:
                return Color.FromRgb(142, 214, 165);
            default:
                return Color.FromRgb(197, 206, 216);
        }
    }

    private void TeleportToMarkVerse(int character)
    {
        character = character * -1;
        if (markedVerses.TryGetValue(character, out Verse? hopefulVerse))
        {
            CurrentVerse = _database.GetVerse(hopefulVerse.BookId, hopefulVerse.Chapter, hopefulVerse.VerseNumber)!;
            Visit(CurrentVerse);
        }
        else
        {
            validityCheck = EntireCommandStatus.Invalid;
        }

    }

    private void TranslationChange()
    {
        _database.ChangeTranslation();
        CurrentVerse = _database.GetVerse(CurrentVerse.BookId, CurrentVerse.Chapter, CurrentVerse.VerseNumber)!;
    }
}