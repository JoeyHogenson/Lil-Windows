using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A readable book: public-domain text in Resources/Books/&lt;id&gt;.txt, listed in Resources/Books/catalog.json.</summary>
[Serializable]
public class BookInfo
{
    public string id;
    public string title;
    public string author;
    public int year;
    [Tooltip("Keep line breaks as written (verse).")]
    public bool poetry;
    [Tooltip("The library's copy is incomplete (the text ends with a note saying so).")]
    public bool truncated;
    public int chars;

    /// <summary>Rough page count at the reader's page size, for lists.</summary>
    public int EstimatedPages => Mathf.Max(1, Mathf.RoundToInt(chars / (float)BookCatalog.CharsPerPageEstimate));
    public string Byline => author + ", " + year;
}

/// <summary>
/// The library's books. All public domain (pre-1929 originals or translations) from Project Gutenberg texts, with
/// the Gutenberg header/footer removed. Add a book: drop its text in Resources/Books and add a catalog.json entry.
/// </summary>
public static class BookCatalog
{
    public const int CharsPerPageEstimate = 1800;

    [Serializable]
    private class CatalogFile
    {
        public BookInfo[] books;
    }

    private static List<BookInfo> books;

    public static IReadOnlyList<BookInfo> All
    {
        get
        {
            if(books == null)
            {
                var file = Resources.Load<TextAsset>("Books/catalog");
                books = file != null ? new List<BookInfo>(JsonUtility.FromJson<CatalogFile>(file.text).books) : new List<BookInfo>();
            }
            return books;
        }
    }

    public static BookInfo ByTitle(string title)
    {
        foreach(BookInfo book in All) if(book.title == title) return book;
        return null;
    }

    /// <summary>The full text (loaded on demand; the long ones are ~1 MB).</summary>
    public static string LoadText(BookInfo book)
    {
        var text = book != null ? Resources.Load<TextAsset>("Books/" + book.id) : null;
        return text != null ? text.text.Replace("\r\n", "\n") : null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        books = null;
    }
}

/// <summary>
/// Reading progress per book (bookmark, finished) and what reading does for Riley: +1 mood every
/// <see cref="PagesPerMood"/> pages, at most <see cref="MaxMoodPerDay"/> a day, +2 for finishing a book.
/// Progress is kept in PlayerPrefs, so it survives quitting (not tied to a save slot).
/// </summary>
public static class ReadingLog
{
    public const int PagesPerMood = 10;
    public const int MaxMoodPerDay = 3;

    private static int trackedDay = -1;
    private static int pagesToday;
    private static int moodToday;

    public static event Action<BookInfo> Finished;

    private static string Key(BookInfo book) => "LilWindows.book." + book.id;

    public static void GetBookmark(BookInfo book, out int chunk, out int page)
    {
        string[] parts = PlayerPrefs.GetString(Key(book), "0|1|0").Split('|');
        chunk = parts.Length > 0 && int.TryParse(parts[0], out int c) ? c : 0;
        page = parts.Length > 1 && int.TryParse(parts[1], out int p) ? Mathf.Max(1, p) : 1;
    }

    public static bool IsFinished(BookInfo book)
    {
        if(book == null) return false;
        string[] parts = PlayerPrefs.GetString(Key(book), "0|1|0").Split('|');
        return parts.Length > 2 && parts[2] == "1";
    }

    public static void SetBookmark(BookInfo book, int chunk, int page)
    {
        PlayerPrefs.SetString(Key(book), chunk + "|" + page + "|" + (IsFinished(book) ? "1" : "0"));
    }

    /// <summary>"p. 34 of ~600", or "finished".</summary>
    public static string ProgressText(BookInfo book)
    {
        if(book == null) return "";
        if(IsFinished(book)) return "finished";
        GetBookmark(book, out int chunk, out int page);
        return chunk == 0 && page <= 1 ? "unread" : "bookmarked";
    }

    /// <summary>Called by the reader on every page turned forward.</summary>
    public static void PageTurned(BookInfo book)
    {
        int day = CurrentDay();
        if(day != trackedDay)
        {
            trackedDay = day;
            pagesToday = 0;
            moodToday = 0;
        }
        pagesToday++;
        if(pagesToday % PagesPerMood == 0 && moodToday < MaxMoodPerDay)
        {
            moodToday++;
            if(AdjustRileyMood(1)) SegNotice.Post("Reading takes your mind off the walls for a while. (+1 mood)");
        }
    }

    /// <summary>Called by the reader when the last page of a book is reached.</summary>
    public static void MarkFinished(BookInfo book)
    {
        if(book == null || IsFinished(book)) return;
        GetBookmark(book, out int chunk, out int page);
        PlayerPrefs.SetString(Key(book), chunk + "|" + page + "|1");
        PlayerPrefs.Save();
        bool riley = AdjustRileyMood(2);
        SegNotice.Post("You finished " + book.title + "." + (riley ? " (+2 mood)" : ""));
        Finished?.Invoke(book);
    }

    private static bool AdjustRileyMood(int amount)
    {
        RileySegState riley = UnityEngine.Object.FindFirstObjectByType<RileySegState>();
        if(riley == null) return false;
        riley.AdjustStat(RileyStat.Mood, amount);
        return true;
    }

    private static int CurrentDay()
    {
        GameController game = UnityEngine.Object.FindFirstObjectByType<GameController>();
        return game != null ? game.dayCount : 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        trackedDay = -1;
        pagesToday = 0;
        moodToday = 0;
        Finished = null;
    }
}
