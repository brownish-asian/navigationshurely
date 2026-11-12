using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using navigationshurely.models;
using System.IO;

namespace BibleApp.Data;

public class BibleDatabase
{
    private readonly SqliteConnection _connection;
    private int translationNumber;
    private static BibleDatabase? databaseInstance;

    private BibleDatabase(string databasePath)
    {
        string connectionString = $"Data Source={databasePath}";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
    }

    public static BibleDatabase Master(int translationNumber)
    {
        if (databaseInstance == null)
        {
            databaseInstance = new BibleDatabase(Path.Combine(AppContext.BaseDirectory,"data","bible.db")) ;
        }

        databaseInstance.translationNumber = translationNumber;
        return databaseInstance;
    }

    public static BibleDatabase Master()
    {
        if (databaseInstance == null)
        {
            databaseInstance = new BibleDatabase("data/bible.db");
            databaseInstance.translationNumber = 1;
        }

        return databaseInstance;
    }

    public string TranslationName()
    {
        if (translationNumber == 1)
            return "ANG DATING BIBLIA";

        if (translationNumber == 2)
            return "KING JAMES VERSION";

        return translationNumber.ToString();
    }

    public int TranslationNumber()
    {
        return translationNumber;
    }

    public void ChangeTranslation()
    {
        if (translationNumber == 1)
            translationNumber = 2;
        else if (translationNumber == 2)
            translationNumber = 1;
    }

    // ---------------------------------------------------------
    // TRANSLATIONS
    // ---------------------------------------------------------

    public List<(int Id, string Name, string Language)> GetTranslations()
    {
        using var command = _connection.CreateCommand();

        command.CommandText = """
            SELECT Id, Name, Language
            FROM Translations
            ORDER BY Id;
            """;

        using var reader = command.ExecuteReader();

        var translations = new List<(int Id, string Name, string Language)>();

        while (reader.Read())
        {
            translations.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2)
            ));
        }

        return translations;
    }

    // ---------------------------------------------------------
    // VERSE
    // ---------------------------------------------------------

    public Verse? GetVerse(int bookId, int chapter, int verseNumber)
    {
        using var command = _connection.CreateCommand();

        // Do not filter by TranslationId here. We want both translation rows
        // so the returned Verse has both TagalogText and EnglishText.
        command.CommandText = """
            SELECT
                BookId,
                Chapter,
                Verse,
                MAX(CASE WHEN TranslationId = 1 THEN Text END),
                MAX(CASE WHEN TranslationId = 2 THEN Text END)
            FROM Verses
            WHERE BookId = $bookId
              AND Chapter = $chapter
              AND Verse = $verse
            GROUP BY BookId, Chapter, Verse;
            """;

        command.Parameters.AddWithValue("$bookId", bookId);
        command.Parameters.AddWithValue("$chapter", chapter);
        command.Parameters.AddWithValue("$verse", verseNumber);

        using var reader = command.ExecuteReader();

        if (!reader.Read())
            return null;

        return new Verse
        {
            BookId = reader.GetInt32(0),
            Chapter = reader.GetInt32(1),
            VerseNumber = reader.GetInt32(2),
            TagalogText = reader.IsDBNull(3) ? "" : reader.GetString(3),
            EnglishText = reader.IsDBNull(4) ? "" : reader.GetString(4)
        };
    }

    // ---------------------------------------------------------
    // BOOKS
    // ---------------------------------------------------------

    public List<int> GetBookIds()
    {
        using var command = _connection.CreateCommand();

        command.CommandText = """
            SELECT DISTINCT BookId
            FROM Verses
            WHERE TranslationId = $translationId
            ORDER BY BookId;
            """;

        command.Parameters.AddWithValue("$translationId", translationNumber);

        using var reader = command.ExecuteReader();

        var books = new List<int>();

        while (reader.Read())
        {
            books.Add(reader.GetInt32(0));
        }

        return books;
    }

    // ---------------------------------------------------------
    // CHAPTERS
    // ---------------------------------------------------------

    public List<(int BookId, int Chapter)> GetChapters()
    {
        using var command = _connection.CreateCommand();

        command.CommandText = """
            SELECT DISTINCT BookId, Chapter
            FROM Verses
            WHERE TranslationId = $translationId
            ORDER BY BookId, Chapter;
            """;

        command.Parameters.AddWithValue("$translationId", translationNumber);

        using var reader = command.ExecuteReader();

        var chapters = new List<(int BookId, int Chapter)>();

        while (reader.Read())
        {
            chapters.Add((
                reader.GetInt32(0),
                reader.GetInt32(1)
            ));
        }

        return chapters;
    }

    // ---------------------------------------------------------
    // VERSES
    // ---------------------------------------------------------

    public int GetMaxVerse(int bookId, int chapter)
    {
        using var command = _connection.CreateCommand();

        command.CommandText = """
            SELECT MAX(Verse)
            FROM Verses
            WHERE TranslationId = $translationId
              AND BookId = $bookId
              AND Chapter = $chapter;
            """;

        command.Parameters.AddWithValue("$translationId", translationNumber);
        command.Parameters.AddWithValue("$bookId", bookId);
        command.Parameters.AddWithValue("$chapter", chapter);

        var result = command.ExecuteScalar();

        if (result == null || result == DBNull.Value)
            return 0;

        return Convert.ToInt32(result);
    }

    public int GetMaxChapter(int bookId)
    {
        using var command = _connection.CreateCommand();

        command.CommandText = """
            SELECT MAX(Chapter)
            FROM Verses
            WHERE TranslationId = $translationId
              AND BookId = $bookId;
            """;

        command.Parameters.AddWithValue("$translationId", translationNumber);
        command.Parameters.AddWithValue("$bookId", bookId);

        var result = command.ExecuteScalar();

        if (result == null || result == DBNull.Value)
            return 0;

        return Convert.ToInt32(result);
    }

    // ---------------------------------------------------------
    // SEARCHING
    // ---------------------------------------------------------

    private List<Verse> ReadSearchResults(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();

        var results = new List<Verse>();

        while (reader.Read())
        {
            results.Add(new Verse
            {
                BookId = reader.GetInt32(0),
                Chapter = reader.GetInt32(1),
                VerseNumber = reader.GetInt32(2),
                TagalogText = reader.IsDBNull(3) ? "" : reader.GetString(3),
                EnglishText = reader.IsDBNull(4) ? "" : reader.GetString(4)
            });
        }

        return results;
    }

    private static string[] GetSearchWords(string searchText)
    {
        // Split on all whitespace, not just ordinary spaces.
        return (searchText ?? "").Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries
        );
    }

    private int GetSearchTranslationId(SearchRequest request)
    {
        // An explicitly supplied language wins. Otherwise search in the
        // translation currently selected in the application.
        int searchTranslationId = request.LanguageSpecified
            ? request.TranslationId
            : translationNumber;

        if (searchTranslationId != 1 && searchTranslationId != 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.TranslationId),
                searchTranslationId,
                "The search translation must be 1 (Tagalog) or 2 (English)."
            );
        }

        return searchTranslationId;
    }

    private List<Verse> SearchCandidates(
        SearchRequest request,
        string[] words,
        int searchTranslationId)
    {
        using var command = _connection.CreateCommand();

        var conditions = new List<string>
    {
        "TranslationId = $translationId"
    };

        command.Parameters.AddWithValue("$translationId", searchTranslationId);

        // Search only the requested translation.
        // Every word must occur somewhere in the verse.
        for (int i = 0; i < words.Length; i++)
        {
            string parameterName = $"$word{i}";

            if (request.CaseSensitive)
            {
                conditions.Add($"instr(Text, {parameterName}) > 0");
            }
            else
            {
                conditions.Add(
                    $"instr(LOWER(Text), LOWER({parameterName})) > 0");
            }

            command.Parameters.AddWithValue(parameterName, words[i]);
        }

        AddLocationConditions(command, conditions, request);

        // First find matching verse locations.
        // Then retrieve each translation using the complete primary key.
        command.CommandText = $"""
        WITH MatchingVerses AS
        (
            SELECT BookId, Chapter, Verse
            FROM Verses
            WHERE {string.Join(" AND ", conditions)}
        )
        SELECT
            m.BookId,
            m.Chapter,
            m.Verse,
            t.Text AS TagalogText,
            e.Text AS EnglishText
        FROM MatchingVerses AS m
        LEFT JOIN Verses AS t
            ON t.TranslationId = 1
           AND t.BookId = m.BookId
           AND t.Chapter = m.Chapter
           AND t.Verse = m.Verse
        LEFT JOIN Verses AS e
            ON e.TranslationId = 2
           AND e.BookId = m.BookId
           AND e.Chapter = m.Chapter
           AND e.Verse = m.Verse
        ORDER BY m.BookId, m.Chapter, m.Verse;
        """;

        return ReadSearchResults(command);
    }

    private List<Verse> SearchAnyOrder(SearchRequest request, string[] words, int searchTranslationId)
    {
        // SearchCandidates adds one condition per word, so every word must
        // occur somewhere in the selected translation of the verse.
        return SearchCandidates(request, words, searchTranslationId);
    }

    private List<Verse> SearchStrictOrder(SearchRequest request, string[] words, int searchTranslationId)
    {
        // First retrieve verses containing every word. Then check their order
        // in C#, because SQL's individual word checks do not enforce ordering.
        List<Verse> candidates = SearchCandidates(request, words, searchTranslationId);

        return candidates
            .Where(verse => WordsAppearInOrder(
                searchTranslationId == 1 ? verse.TagalogText : verse.EnglishText,
                words,
                request.CaseSensitive))
            .ToList();
    }

    private static bool WordsAppearInOrder(string text, string[] words, bool caseSensitive)
    {
        int position = 0;

        StringComparison comparison = caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        foreach (string word in words)
        {
            position = text.IndexOf(word, position, comparison);

            if (position < 0)
                return false;

            position += word.Length;
        }

        return true;
    }

    private static void AddLocationConditions(
        SqliteCommand command,
        List<string> conditions,
        SearchRequest request)
    {
        if (request.BookId.HasValue)
        {
            conditions.Add("BookId = $bookId");
            command.Parameters.AddWithValue("$bookId", request.BookId.Value);
        }

        if (request.Chapter.HasValue)
        {
            conditions.Add("Chapter = $chapter");
            command.Parameters.AddWithValue("$chapter", request.Chapter.Value);
        }

        if (request.Verse.HasValue)
        {
            conditions.Add("Verse = $verse");
            command.Parameters.AddWithValue("$verse", request.Verse.Value);
        }

        if (request.Testament.HasValue)
        {
            string comparison = request.Testament.Value == Testament.Old
                ? "<= 39"
                : "> 39";

            conditions.Add($"BookId {comparison}");
        }
    }

    public List<Verse> Search(SearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string[] words = GetSearchWords(request.SearchText);

        if (words.Length == 0)
            return new List<Verse>();

        int searchTranslationId = GetSearchTranslationId(request);

        return request.SearchMode switch
        {
            SearchMode.AnyOrder => SearchAnyOrder(request, words, searchTranslationId),
            SearchMode.StrictOrder => SearchStrictOrder(request, words, searchTranslationId),
            _ => throw new ArgumentException("Invalid search mode.", nameof(request))
        };
    }
}
