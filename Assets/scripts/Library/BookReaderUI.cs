using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Reads a <see cref="BookInfo"/> page by page. The text is cut into ~5,000-character sections at paragraph breaks
/// (laying out a whole 1 MB novel at once would stall), and each section is paged by TextMeshPro's page mode.
/// Remembers the bookmark (<see cref="ReadingLog"/>), counts pages toward Riley's mood, and marks the book finished
/// on its last page. Opens over other panels (e.g. previewing from the book cart) without closing them.
/// Prev/Next buttons, Left/Right arrows or PageUp/PageDown turn pages; Escape closes.
/// </summary>
public class BookReaderUI : SegPanel
{
    public const int ChunkChars = 5000;

    private static BookReaderUI instance;

    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI body;
    private TextMeshProUGUI pageLabel;

    private BookInfo book;
    private readonly List<string> chunks = new List<string>();
    private readonly List<int> chunkStarts = new List<int>();
    private int totalChars;
    private int chunk;
    private int page = 1;
    private int furthestPage;

    /// <summary>Opens <paramref name="bookToRead"/> at its bookmark.</summary>
    public static void Show(BookInfo bookToRead)
    {
        if(bookToRead == null) return;
        if(instance == null) instance = new GameObject("Book Reader").AddComponent<BookReaderUI>();
        instance.Load(bookToRead);
    }

    protected override void Awake()
    {
        instance = this;
        sortingOrder = 70;
        opensOnTop = true;
        base.Awake();
    }

    protected override void Build(Transform root)
    {
        RectTransform panel = SegUIKit.Panel(root, "Book", new Vector2(1180f, 900f), SegUIKit.Paper);
        VerticalLayoutGroup column = SegUIKit.Column(panel, 12f, 40);
        column.childForceExpandHeight = false;

        titleLabel = SegUIKit.Label(panel, "", 28f, null, FontStyles.Bold, TextAlignmentOptions.Center);

        body = SegUIKit.Label(panel, "", 26f, SegUIKit.Ink, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Page;
        body.lineSpacing = 8f;
        var bodyLayout = body.gameObject.AddComponent<LayoutElement>();
        bodyLayout.minHeight = 660f;
        bodyLayout.preferredHeight = 660f;
        bodyLayout.flexibleHeight = 1f;

        RectTransform row = SegUIKit.Row(panel, 16f);
        row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        SegUIKit.Button(row, "< Prev", () => Turn(-1), 140f);
        pageLabel = SegUIKit.Width(SegUIKit.Label(row, "", 22f, SegUIKit.FadedInk, FontStyles.Normal, TextAlignmentOptions.Center), 420f);
        SegUIKit.Button(row, "Next >", () => Turn(1), 140f);
        SegUIKit.Button(row, "Close", Close, 140f);
    }

    void Update()
    {
        if(!IsOpen) return;
        Keyboard keyboard = Keyboard.current;
        if(keyboard == null) return;
        if(keyboard.rightArrowKey.wasPressedThisFrame || keyboard.pageDownKey.wasPressedThisFrame) Turn(1);
        if(keyboard.leftArrowKey.wasPressedThisFrame || keyboard.pageUpKey.wasPressedThisFrame) Turn(-1);
    }

    public override void Close()
    {
        if(book != null) ReadingLog.SetBookmark(book, chunk, page);
        PlayerPrefs.Save();
        base.Close();
    }

    private void Load(BookInfo bookToRead)
    {
        book = bookToRead;
        Split(BookCatalog.LoadText(book) ?? "(This copy is missing its pages.)");
        titleLabel.text = book.title.ToUpperInvariant() + "\n<size=70%>" + book.Byline + "</size>";
        body.alignment = TextAlignmentOptions.TopLeft;

        ReadingLog.GetBookmark(book, out int savedChunk, out int savedPage);
        chunk = Mathf.Clamp(savedChunk, 0, chunks.Count - 1);
        page = savedPage;
        furthestPage = GlobalPage();
        Open();
        ShowChunk(page);
    }

    //sections end at paragraph breaks, so a page never starts mid-paragraph more than TMP itself decides
    private void Split(string text)
    {
        chunks.Clear();
        chunkStarts.Clear();
        totalChars = text.Length;
        int start = 0;
        while(start < text.Length)
        {
            int end = Mathf.Min(text.Length, start + ChunkChars);
            if(end < text.Length)
            {
                int paragraph = text.LastIndexOf("\n\n", end, end - start, System.StringComparison.Ordinal);
                if(paragraph > start + ChunkChars / 3) end = paragraph + 2;
            }
            chunkStarts.Add(start);
            chunks.Add(text.Substring(start, end - start).Trim('\n'));
            start = end;
        }
        if(chunks.Count == 0)
        {
            chunks.Add("");
            chunkStarts.Add(0);
        }
    }

    private void ShowChunk(int pageInChunk)
    {
        body.text = chunks[chunk];
        body.ForceMeshUpdate();
        int pages = Mathf.Max(1, body.textInfo.pageCount);
        page = pageInChunk < 1 ? pages : Mathf.Clamp(pageInChunk, 1, pages);
        body.pageToDisplay = page;
        UpdateLabel(pages);
    }

    private void Turn(int direction)
    {
        if(book == null) return;
        int pages = Mathf.Max(1, body.textInfo.pageCount);
        if(direction > 0)
        {
            if(page < pages)
            {
                page++;
                body.pageToDisplay = page;
                UpdateLabel(pages);
            }
            else if(chunk < chunks.Count - 1)
            {
                chunk++;
                ShowChunk(1);
            }
            else
            {
                ReadingLog.MarkFinished(book);
                return;
            }
            //only new ground counts toward reading (no mood from flipping back and forth)
            int global = GlobalPage();
            if(global > furthestPage)
            {
                furthestPage = global;
                ReadingLog.PageTurned(book);
            }
            if(chunk == chunks.Count - 1 && page == Mathf.Max(1, body.textInfo.pageCount)) ReadingLog.MarkFinished(book);
        }
        else
        {
            if(page > 1)
            {
                page--;
                body.pageToDisplay = page;
                UpdateLabel(pages);
            }
            else if(chunk > 0)
            {
                chunk--;
                ShowChunk(0);
            }
        }
        ReadingLog.SetBookmark(book, chunk, page);
    }

    //pages are measured in the current section; the whole-book number is estimated from its characters per page
    private int CharsPerPage()
    {
        int pages = Mathf.Max(1, body.textInfo.pageCount);
        return Mathf.Max(400, chunks[chunk].Length / pages);
    }

    private int GlobalPage()
    {
        return Mathf.RoundToInt(chunkStarts[chunk] / (float)CharsPerPage()) + page;
    }

    private void UpdateLabel(int pagesInChunk)
    {
        int total = Mathf.Max(GlobalPage(), Mathf.RoundToInt(totalChars / (float)CharsPerPage()));
        pageLabel.text = "page " + GlobalPage() + " of ~" + total + (ReadingLog.IsFinished(book) ? "  (finished)" : "");
    }
}
