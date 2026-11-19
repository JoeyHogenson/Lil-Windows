using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Riley's property box: balance, stats, books, delivered items (with Use buttons), what's still on the way,
/// and any missing items that need a Request for Interview.
/// </summary>
public class PropertyBoxUI : SegPanel
{
    [SerializeField] private CommissarySystem commissary;
    [SerializeField] private Key openKey = Key.I;

    private RectTransform content;
    private ScrollRect scroll;

    private RileySegState Riley => commissary != null ? commissary.Riley : null;

    protected override void Awake()
    {
        if(commissary == null) commissary = GetComponent<CommissarySystem>();
        if(commissary == null) commissary = FindFirstObjectByType<CommissarySystem>();
        base.Awake();
    }

    void OnEnable()
    {
        if(commissary != null) commissary.OrderChanged += OnOrderChanged;
    }

    void Start()
    {
        if(Riley != null) Riley.Changed += RefreshIfOpen;
    }

    void OnDisable()
    {
        if(commissary != null) commissary.OrderChanged -= OnOrderChanged;
        if(Riley != null) Riley.Changed -= RefreshIfOpen;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if(keyboard != null && keyboard[openKey].wasPressedThisFrame) Toggle();
    }

    protected override void Build(Transform root)
    {
        RectTransform panel = SegUIKit.Panel(root, "Property Box", new Vector2(900f, 860f), SegUIKit.Paper);
        content = SegUIKit.ScrollArea(panel, out scroll);
    }

    protected override void OnOpened()
    {
        Rebuild();
        scroll.verticalNormalizedPosition = 1f;
    }

    private void OnOrderChanged(CommissaryOrder order)
    {
        RefreshIfOpen();
    }

    private void RefreshIfOpen()
    {
        if(IsOpen) Rebuild();
    }

    private void Rebuild()
    {
        float scrollPosition = scroll.verticalNormalizedPosition;
        SegUIKit.Clear(content);
        RileySegState riley = Riley;

        Label("PROPERTY BOX", 30f, FontStyles.Bold, TextAlignmentOptions.Center);
        Label("Slots used: " + riley.SlotsUsed + "/" + riley.cellSlots + "     Balance: " + SegUIKit.Money(riley.balance)
            + "     PIP Phase " + CommissarySystem.ToRoman(riley.phase), 22f);
        Label("Hunger " + riley.GetStat(RileyStat.Hunger) + "   Energy " + riley.GetStat(RileyStat.Energy)
            + "   Hygiene " + riley.GetStat(RileyStat.Hygiene) + "   Mood " + riley.GetStat(RileyStat.Mood), 22f);

        Heading("BOOKS");
        if(riley.books.Count == 0) Faded("(none)");
        foreach(string title in riley.books) Label(title, 22f);

        Heading("ITEMS");
        if(riley.property.Count == 0) Faded("(none)");
        foreach(RileySegState.OwnedItem owned in riley.property)
        {
            CommissaryItem item = commissary.Item(owned.itemId);
            if(item == null) continue;
            RectTransform row = SegUIKit.Row(content);
            SegUIKit.Width(SegUIKit.Label(row, item.displayName + " x" + owned.count, 22f), 360f);
            SegUIKit.Width(SegUIKit.Label(row, item.effectText, 18f, SegUIKit.FadedInk), 300f);
            if(item.HasUseEffect && item.consumable) SegUIKit.Button(row, "Use", () => riley.UseItem(item), 90f);
        }

        Heading("ON THE WAY");
        bool anyIncoming = false;
        foreach(CommissaryOrder order in commissary.Orders)
        {
            if(!order.IsSigned) continue;
            foreach(OrderLine line in order.AllLines)
            {
                string name = commissary.Item(line.itemId).displayName;
                if(line.IsOutstanding)
                {
                    anyIncoming = true;
                    string when = line.status == LineStatus.Pending
                        ? "waiting for approval"
                        : "expected " + SegCalendar.Describe(line.deliveryDay) + (line.status == LineStatus.BackOrdered ? " (back-ordered)" : "");
                    Label(name + " x" + line.qty + "  -  " + when, 20f);
                }
                else if(line.status == LineStatus.Missing)
                {
                    anyIncoming = true;
                    RectTransform row = SegUIKit.Row(content);
                    SegUIKit.Width(SegUIKit.Label(row, "Missing: " + name + " x" + line.missingQty, 20f, SegUIKit.Warning), 360f);
                    if(line.fixDay < 0)
                    {
                        CommissaryOrder missingOrder = order;
                        OrderLine missingLine = line;
                        SegUIKit.Button(row, "Write a Request for Interview", () => commissary.FileDiscrepancyRequest(missingOrder, missingLine));
                    }
                    else
                    {
                        SegUIKit.Label(row, "Request filed. Expected " + SegCalendar.Describe(line.fixDay) + ".", 18f, SegUIKit.FadedInk);
                    }
                }
            }
        }
        if(!anyIncoming) Faded("(nothing)");

        RectTransform buttons = SegUIKit.Row(content);
        buttons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
        SegUIKit.Button(buttons, "Close", Close, 140f);

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scroll.verticalNormalizedPosition = scrollPosition;
    }

    private void Heading(string text)
    {
        Label(text, 24f, FontStyles.Bold).margin = new Vector4(0f, 16f, 0f, 4f);
    }

    private void Faded(string text)
    {
        SegUIKit.Label(content, text, 20f, SegUIKit.FadedInk, FontStyles.Italic);
    }

    private TextMeshProUGUI Label(string text, float size, FontStyles style = FontStyles.Normal, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        return SegUIKit.Label(content, text, size, null, style, alignment);
    }
}
