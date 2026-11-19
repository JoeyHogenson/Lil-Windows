using TMPro;
using UnityEngine;

/// <summary>
/// The daily book cart prompt: pick one of the offered titles (Go) or keep your book (Stay), before the
/// timer runs out. Opens and closes itself from <see cref="LibrarySystem"/>'s events; Escape counts as Stay.
/// </summary>
public class LibraryPromptUI : SegPanel
{
    [SerializeField] private LibrarySystem library;

    private RectTransform choices;
    private TextMeshProUGUI heldText;
    private TextMeshProUGUI timerText;

    protected override void Awake()
    {
        if(library == null) library = GetComponent<LibrarySystem>();
        if(library == null) library = FindFirstObjectByType<LibrarySystem>();
        base.Awake();
    }

    void OnEnable()
    {
        if(library == null) return;
        library.PromptOpened += OnPromptOpened;
        library.PromptClosed += Close;
    }

    void OnDisable()
    {
        if(library == null) return;
        library.PromptOpened -= OnPromptOpened;
        library.PromptClosed -= Close;
    }

    void Update()
    {
        if(IsOpen) timerText.text = "The officer waits... " + Mathf.CeilToInt(library.SecondsLeft) + "s";
    }

    protected override void Build(Transform root)
    {
        RectTransform panel = SegUIKit.Panel(root, "Book Cart", new Vector2(760f, 620f), SegUIKit.Paper);
        SegUIKit.Column(panel, 12f, 32);

        SegUIKit.Label(panel, "BOOK CART", 32f, null, FontStyles.Bold, TextAlignmentOptions.Center);
        SegUIKit.Label(panel, "An officer stops the cart at your door. Take one, or keep what you have.", 22f, null, FontStyles.Italic, TextAlignmentOptions.Center);
        heldText = SegUIKit.Label(panel, "", 20f, SegUIKit.FadedInk, FontStyles.Normal, TextAlignmentOptions.Center);

        choices = SegUIKit.Rect("Choices", panel);
        SegUIKit.Column(choices, 10f, 8);

        timerText = SegUIKit.Label(panel, "", 20f, SegUIKit.FadedInk, FontStyles.Italic, TextAlignmentOptions.Center);
        SegUIKit.Button(panel, "Stay (keep your book)", () => library.Stay());
    }

    public override void Close()
    {
        if(!IsOpen) return;
        base.Close();
        //closing the panel any other way (Escape) means Riley stays; Stay() raises PromptClosed, which lands back here as a no-op
        if(library.IsPromptOpen) library.Stay();
    }

    private void OnPromptOpened(string[] offer)
    {
        SegUIKit.Clear(choices);
        for(int i = 0; i < offer.Length; i++)
        {
            int index = i;
            SegUIKit.Button(choices, "Go: " + offer[i], () => library.Go(index));
        }

        RileySegState riley = library.Riley;
        string held = riley.books.Count == 0 ? "You have no books." : "In your cell: " + string.Join(", ", riley.books);
        string returned = library.BookToReturn;
        if(returned != null) held += "\nYou're at the limit; taking one returns " + returned + ".";
        heldText.text = held;

        Open();
    }
}
