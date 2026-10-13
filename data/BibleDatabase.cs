using Microsoft.Data.Sqlite;
using navigationshurely.models;
using static System.Net.Mime.MediaTypeNames;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;

namespace BibleApp.Data;

public class BibleDatabase
{
    private readonly string _connectionString;
    private readonly SqliteConnection _connection;
    private int translationNumber = 1;
    private static BibleDatabase databaseInstance;

    private BibleDatabase(string databasePath)
    {
        _connectionString = $"Data Source={databasePath}";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
    }

    public static BibleDatabase Master()
    {
        if (databaseInstance == null)
        {
            databaseInstance = new BibleDatabase("data/bible.db");
        }
        return databaseInstance;

    }
    public string TranslationName()
    {
        if (translationNumber == 1)
        {
            return "ANG DATING BIBLIA";
        }
        else if (translationNumber == 2)
        {
            return "KING JAMES VERSION";
        }
        else
        {
            return translationNumber.ToString();
        }
    }
    public int TranslationNumber()
    {
        return (int) translationNumber;
    }
    public void ChangeTranslation()
    {
        if (translationNumber == 1)
        {
            translationNumber = 2;
        }
        else if (translationNumber == 2)
        {
            translationNumber = 1;
        }
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

        var translations =
            new List<(int Id, string Name, string Language)>();

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

        command.CommandText = """
            SELECT BookId, Chapter, Verse, Text
            FROM Verses
            WHERE TranslationId = $translationId
              AND BookId = $bookId
              AND Chapter = $chapter
              AND Verse = $verse;
            """;

        command.Parameters.AddWithValue(
            "$translationId",
            translationNumber);

        command.Parameters.AddWithValue(
            "$bookId",
            bookId);

        command.Parameters.AddWithValue(
            "$chapter",
            chapter);

        command.Parameters.AddWithValue(
            "$verse",
            verseNumber);

        using var reader = command.ExecuteReader();

        if (!reader.Read())
            return null;

        return new Verse
        {
            BookId = reader.GetInt32(0),
            Chapter = reader.GetInt32(1),
            VerseNumber = reader.GetInt32(2),
            Text = reader.GetString(3)
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

        command.Parameters.AddWithValue(
            "$translationId",
            translationNumber);

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

        command.Parameters.AddWithValue(
            "$translationId",
            translationNumber);

        using var reader = command.ExecuteReader();

        var chapters =
            new List<(int BookId, int Chapter)>();

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

        command.Parameters.AddWithValue(
            "$translationId",
            translationNumber);

        command.Parameters.AddWithValue(
            "$bookId",
            bookId);

        command.Parameters.AddWithValue(
            "$chapter",
            chapter);

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

        command.Parameters.AddWithValue(
            "$translationId",
            translationNumber);

        command.Parameters.AddWithValue(
            "$bookId",
            bookId);

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
                Text = reader.GetString(3)
            });
        }

        return results;
    }

    private List<Verse> SearchStrictOrder(SearchRequest request)
    {
        string[] words = request.SearchText.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
            return new List<Verse>();

        using var command = _connection.CreateCommand();

        var conditions = new List<string>();

        command.Parameters.AddWithValue(
            "$translationId",
            request.TranslationId);

        conditions.Add("TranslationId = $translationId");

        AddLocationConditions(command, conditions, request);

        for (int i = 0; i < words.Length; i++)
        {
            string parameterName = $"$word{i}";

            if (request.CaseSensitive)
            {
                conditions.Add($"instr(Text, {parameterName}) > 0");

                command.Parameters.AddWithValue(
                    parameterName,
                    words[i]);
            }
            else
            {
                conditions.Add(
                    $"LOWER(Text) LIKE LOWER({parameterName})");

                command.Parameters.AddWithValue(
                    parameterName,
                    $"%{words[i]}%");
            }
        }

        command.CommandText = $"""
    SELECT BookId, Chapter, Verse, Text
    FROM Verses
    WHERE {string.Join(" AND ", conditions)}
    ORDER BY BookId, Chapter, Verse;
    """;

        List<Verse> candidates = ReadSearchResults(command);

        return candidates
            .Where(v => WordsAppearInOrder(
                v.Text,
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
            position = text.IndexOf(
                word,
                position,
                comparison);

            if (position == -1)
                return false;

            position += word.Length;
        }

        return true;
    }

    private List<Verse> SearchAnyOrder(SearchRequest request)
    {
        string[] words = request.SearchText.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
            return new List<Verse>();

        using var command = _connection.CreateCommand();

        var conditions = new List<string>();

        command.Parameters.AddWithValue(
            "$translationId",
            request.TranslationId);

        conditions.Add("TranslationId = $translationId");

        AddLocationConditions(command, conditions, request);

        for (int i = 0; i < words.Length; i++)
        {
            string parameterName = $"$word{i}";

            if (request.CaseSensitive)
            {
                conditions.Add($"instr(Text, {parameterName}) > 0");

                command.Parameters.AddWithValue(
                    parameterName,
                    words[i]);
            }
            else
            {
                conditions.Add(
                    $"LOWER(Text) LIKE LOWER({parameterName})");

                command.Parameters.AddWithValue(
                    parameterName,
                    $"%{words[i]}%");
            }
        }

        command.CommandText = $"""
    SELECT BookId, Chapter, Verse, Text
    FROM Verses
    WHERE {string.Join(" AND ", conditions)}
    ORDER BY BookId, Chapter, Verse;
    """;

        return ReadSearchResults(command);
    }

    private static void AddLocationConditions(SqliteCommand command, List<string> conditions, SearchRequest request)
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
            string comparison = request.Testament.Value == Testament.Old? "<= 39": "> 39";

            conditions.Add($"BookId {comparison}");
        }
    }

    public List<Verse> Search(SearchRequest request)
    {
        return request.SearchMode switch
        {
            SearchMode.AnyOrder => SearchAnyOrder(request),
            SearchMode.StrictOrder => SearchStrictOrder(request), _ => throw new ArgumentException("Invalid search mode.")
        };
    }
}