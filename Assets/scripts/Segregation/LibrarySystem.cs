using System;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using UnityEngine;

/// <summary>
/// The daily library choice from the Riley Playthrough doc [design]: once a day, at a random moment, the book
/// cart comes by. Riley picks one of a few titles (Go) or keeps what they have (Stay); the offer times out.
/// Riley holds at most 3 books, which share the property box with commissary items. About one day a week has
/// no library (lockdown, short staff), never two days in a row.
/// </summary>
public class LibrarySystem : SegDayListener
{
    [SerializeField] private RileySegState riley;

    [Header("Books [design]")]
    [SerializeField] private string[] titles =
    {
        "The Count of Monte Cristo",
        "Of Mice and Men",
        "The Old Man and the Sea",
        "Man's Search for Meaning",
        "The Autobiography of Malcolm X",
        "Into the Wild",
        "Call of the Wild",
        "GED Prep: Math",
        "GED Prep: Reading & Writing",
        "Know Your Rights (Prisoner Handbook)",
        "Westerns Omnibus",
        "Word Search Puzzles, Vol. 2",
        "Birds of Alaska",
        "Basic Drawing",
        "A Wrinkle in Time",
    };
    [SerializeField] private int maxBooksHeld = 3;
    [SerializeField] private int titlesOffered = 3;

    [Header("Timing")]
    [Tooltip("The cart comes at a random point in this part of the day (0 = day start, 1 = day end).")]
    [Range(0f, 1f)] [SerializeField] private float earliestInDay = 0.15f;
    [Range(0f, 1f)] [SerializeField] private float latestInDay = 0.7f;
    [Tooltip("Seconds of play time before the cart moves on and Riley keeps their current book.")]
    [SerializeField] private float promptTimeoutSeconds = 30f;

    [Header("No-library days")]
    [Tooltip("Pick one no-library day per week, never two in a row.")]
    [SerializeField] private bool scheduleNoLibraryDays = true;
    [Tooltip("No cart while GameController.isLockdown is set.")]
    [SerializeField] private bool skipDuringLockdown = true;
    [Tooltip("No cart while the Dialogue System variable 'bookBan' is true.")]
    [SerializeField] private bool skipDuringBookBan = true;

    [Header("Testing")]
    [Tooltip("0 = different every run. Any other value replays the same rolls.")]
    [SerializeField] private int randomSeed;

    private readonly HashSet<int> noLibraryDays = new HashSet<int>();
    private System.Random rng;
    private float cartArrivesAt = float.PositiveInfinity;
    private float promptDeadline;
    private string[] offer = new string[0];

    /// <summary>Raised when the cart arrives, with the titles on offer.</summary>
    public event Action<string[]> PromptOpened;
    /// <summary>Raised when Riley picks, stays, or the cart moves on.</summary>
    public event Action PromptClosed;

    public bool IsPromptOpen { get; private set; }
    public IReadOnlyList<string> Offer => offer;
    public float SecondsLeft => IsPromptOpen ? Mathf.Max(0f, promptDeadline - Time.time) : 0f;
    public RileySegState Riley => riley;
    public bool IsAtBookLimit => riley.books.Count >= maxBooksHeld;
    /// <summary>The book that goes back on the cart if Riley takes a new one while at the limit.</summary>
    public string BookToReturn => IsAtBookLimit && riley.books.Count > 0 ? riley.books[0] : null;
    public bool IsNoLibraryDay(int day) => noLibraryDays.Contains(day);

    void Awake()
    {
        if(riley == null) riley = GetComponent<RileySegState>();
        if(riley == null) riley = FindFirstObjectByType<RileySegState>();
        rng = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
        if(scheduleNoLibraryDays) PlanNoLibraryDays();
    }

    void Update()
    {
        if(!IsPromptOpen && Time.time >= cartArrivesAt)
        {
            cartArrivesAt = float.PositiveInfinity;
            CartArrives();
        }
        else if(IsPromptOpen && Time.time >= promptDeadline)
        {
            ClosePrompt("The cart moves on. You keep what you have.");
        }
    }

    // ---------- Player choices ----------

    /// <summary>Go: take the title at <paramref name="index"/> in <see cref="Offer"/>.</summary>
    public void Go(int index)
    {
        if(!IsPromptOpen || index < 0 || index >= offer.Length) return;
        string title = offer[index];

        string returned = BookToReturn;
        if(returned == null && riley.FreeSlots <= 0)
        {
            SegNotice.Post("Your property box is full. Make room before taking a book.");
            return;
        }
        if(returned != null) riley.RemoveBook(returned);
        riley.AddBook(title);

        ClosePrompt(returned != null
            ? "You hand back " + returned + " and take " + title + "."
            : "You take " + title + ".");
    }

    /// <summary>Stay: keep the current book(s).</summary>
    public void Stay()
    {
        if(IsPromptOpen) ClosePrompt("You wave the cart on.");
    }

    /// <summary>Brings the cart right now (testing, or a scripted visit).</summary>
    [ContextMenu("Bring Cart Now")]
    public void BringCartNow()
    {
        if(!IsPromptOpen) CartArrives();
    }

    // ---------- Day processing ----------

    protected override void OnDayStarted(int day)
    {
        //the cart never carries over into the next day
        if(IsPromptOpen) ClosePrompt(null);
        cartArrivesAt = float.PositiveInfinity;

        if(day > SegCalendar.TotalDays) return;
        if(noLibraryDays.Contains(day))
        {
            SegNotice.Post("No library today. Short staff.");
            return;
        }

        float dayLength = game.end - game.DayStartTime;
        float fraction = Mathf.Lerp(earliestInDay, Mathf.Max(earliestInDay, latestInDay), (float)rng.NextDouble());
        cartArrivesAt = game.DayStartTime + dayLength * fraction;
    }

    private void CartArrives()
    {
        if(skipDuringLockdown && game != null && game.isLockdown)
        {
            SegNotice.Post("No library today. The unit is on lockdown.");
            return;
        }
        if(skipDuringBookBan && DialogueLua.GetVariable("bookBan").asBool)
        {
            SegNotice.Post("The book cart doesn't come. There's a book ban.");
            return;
        }

        offer = PickOffer();
        if(offer.Length == 0) return;

        IsPromptOpen = true;
        promptDeadline = Time.time + promptTimeoutSeconds;
        SegNotice.Post("An officer pushes the book cart up to your door.");
        PromptOpened?.Invoke(offer);
    }

    private void ClosePrompt(string message)
    {
        IsPromptOpen = false;
        if(message != null) SegNotice.Post(message);
        PromptClosed?.Invoke();
    }

    private string[] PickOffer()
    {
        var pool = new List<string>();
        foreach(string title in titles)
        {
            if(!riley.books.Contains(title)) pool.Add(title);
        }
        //partial Fisher-Yates: the first n entries end up a random sample
        int count = Mathf.Min(titlesOffered, pool.Count);
        for(int i = 0; i < count; i++)
        {
            int j = rng.Next(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool.GetRange(0, count).ToArray();
    }

    //one missed day per 7-day week; the last partial week (days 29-30) only sometimes gets one
    private void PlanNoLibraryDays()
    {
        noLibraryDays.Clear();
        int previous = -10;
        for(int weekStart = 1; weekStart <= SegCalendar.TotalDays; weekStart += 7)
        {
            int length = Mathf.Min(7, SegCalendar.TotalDays - weekStart + 1);
            if(length < 7 && rng.NextDouble() > length / 7.0) continue;

            int day;
            do
            {
                day = weekStart + rng.Next(length);
            }
            while(day - previous <= 1);
            noLibraryDays.Add(day);
            previous = day;
        }
    }
}
