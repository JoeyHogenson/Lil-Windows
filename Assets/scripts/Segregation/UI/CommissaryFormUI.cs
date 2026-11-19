using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The in-game Commissary Request form from the Riley Playthrough doc. Riley taps items from the price sheet
/// into Section A, picks at most one item each for B and C, picks a reason, and signs. Once signed, the
/// staff box fills in as decisions and deliveries happen. Open with the hotkey or call <see cref="SegPanel.Open"/>
/// from a button/trigger (e.g. the form under the door).
/// </summary>
public class CommissaryFormUI : SegPanel
{
    [SerializeField] private CommissarySystem commissary;
    [SerializeField] private Key openKey = Key.C;
    [Tooltip("Shown in the header. The doc leaves it to the game.")]
    [SerializeField] private string prisonerNumber = "500817";
    [SerializeField] private string cellNumber = "4";

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
        if(commissary == null) return;
        commissary.OrderChanged += OnOrderChanged;
        commissary.RfiClosed += RefreshIfOpen;
    }

    void Start()
    {
        //CommissarySystem fills in its Riley reference during its own Awake, so read it from Start on
        if(Riley != null) Riley.Changed += RefreshIfOpen;
    }

    void OnDisable()
    {
        if(commissary == null) return;
        commissary.OrderChanged -= OnOrderChanged;
        commissary.RfiClosed -= RefreshIfOpen;
        if(Riley != null) Riley.Changed -= RefreshIfOpen;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if(keyboard != null && keyboard[openKey].wasPressedThisFrame) Toggle();
    }

    protected override void Build(Transform root)
    {
        RectTransform panel = SegUIKit.Panel(root, "Commissary Request", new Vector2(1180f, 1000f), SegUIKit.Paper);
        content = SegUIKit.ScrollArea(panel, out scroll);
    }

    protected override void OnOpened()
    {
        Rebuild(keepScroll: false);
    }

    private void OnOrderChanged(CommissaryOrder order)
    {
        RefreshIfOpen();
    }

    private void RefreshIfOpen()
    {
        if(IsOpen) Rebuild(keepScroll: true);
    }

    // ---------- Layout ----------

    private void Rebuild(bool keepScroll)
    {
        float scrollPosition = scroll.verticalNormalizedPosition;
        SegUIKit.Clear(content);

        Text("STATE OF ALASKA - DEPARTMENT OF CORRECTIONS", 22f, FontStyles.Bold, TextAlignmentOptions.Center);
        Text("COMMISSARY REQUEST - SEGREGATION", 30f, FontStyles.Bold, TextAlignmentOptions.Center);
        Text("Game form, modeled on P&P 808.13", 18f, FontStyles.Italic, TextAlignmentOptions.Center, SegUIKit.FadedInk);

        CommissaryOrder order = commissary.DisplayOrder;
        if(order == null)
        {
            Text("No request form right now. Forms come under the door on Mondays.", 24f, FontStyles.Italic, TextAlignmentOptions.Center, SegUIKit.FadedInk);
            Footer(null, false);
            Finish(keepScroll, scrollPosition);
            return;
        }

        bool editable = commissary.IsEditable(order);
        int formDay = order.IsSigned ? order.submittedDay : order.openedDay;
        Text("Name: RILEY          Prisoner #: " + prisonerNumber, 22f);
        Text("Housing: SEG / Cell " + cellNumber + "     Date: " + SegCalendar.Describe(formDay) + "     Week: " + order.week + " of " + SegCalendar.RfiWeeks, 22f);
        Text("PIP Phase: " + CommissarySystem.ToRoman(Riley.phase) + "          Account balance: " + SegUIKit.Money(Riley.balance)
            + "          Property box: " + Riley.SlotsUsed + "/" + Riley.cellSlots, 22f);

        if(order.state == OrderState.RejectedFunds)
        {
            Text("RETURNED BY OFFICER: Insufficient funds.", 24f, FontStyles.Bold, TextAlignmentOptions.Left, SegUIKit.Warning);
        }
        if(order.state == OrderState.Missed)
        {
            Text("NOT SUBMITTED. The form was collected blank.", 24f, FontStyles.Bold, TextAlignmentOptions.Left, SegUIKit.Warning);
        }

        SectionA(order, editable);
        SectionB(order, editable);
        SectionC(order, editable);

        Text("I understand: funds must be in my account when the order is placed; items are checked at delivery; "
            + "no refunds or exchanges; discrepancies must be reported in writing.", 18f, FontStyles.Italic, TextAlignmentOptions.Left, SegUIKit.FadedInk);

        if(order.IsSigned)
        {
            Text("Prisoner signature: Riley", 22f, FontStyles.Italic);
            StaffBox(order);
        }

        Footer(order, editable);
        Finish(keepScroll, scrollPosition);
    }

    private void SectionA(CommissaryOrder order, bool editable)
    {
        Heading("SECTION A - REGULAR COMMISSARY (limit " + SegUIKit.Money(Riley.weeklyCap) + "/week)");

        if(editable)
        {
            foreach(CommissaryItem item in commissary.Catalog.InSection(CommissarySection.Regular))
            {
                string id = item.id;
                int qty = commissary.QuantityOf(id);
                float price = commissary.PriceOf(item);
                RectTransform row = SegUIKit.Row(content);
                SegUIKit.Width(SegUIKit.Label(row, item.displayName, 22f), 290f);
                SegUIKit.Width(SegUIKit.Label(row, item.effectText, 18f, SegUIKit.FadedInk), 270f);
                SegUIKit.Width(SegUIKit.Label(row, SegUIKit.Money(price), 22f, null, FontStyles.Normal, TextAlignmentOptions.Right), 90f);
                SegUIKit.Button(row, "-", () => commissary.SetQuantity(id, qty - 1), 44f, null, qty > 0);
                SegUIKit.Width(SegUIKit.Label(row, qty.ToString(), 22f, null, FontStyles.Bold, TextAlignmentOptions.Center), 44f);
                SegUIKit.Button(row, "+", () => commissary.SetQuantity(id, qty + 1), 44f);
                SegUIKit.Width(SegUIKit.Label(row, qty > 0 ? SegUIKit.Money(price * qty) : "", 22f, null, FontStyles.Normal, TextAlignmentOptions.Right), 100f);
            }
        }
        else if(order.sectionA.Count == 0)
        {
            Text("(none)", 22f, FontStyles.Italic, TextAlignmentOptions.Left, SegUIKit.FadedInk);
        }
        else
        {
            foreach(OrderLine line in order.sectionA)
            {
                RectTransform row = SegUIKit.Row(content);
                SegUIKit.Width(SegUIKit.Label(row, commissary.Item(line.itemId).displayName, 22f), 330f);
                SegUIKit.Width(SegUIKit.Label(row, "x" + line.qty, 22f), 60f);
                SegUIKit.Width(SegUIKit.Label(row, SegUIKit.Money(line.price), 22f, null, FontStyles.Normal, TextAlignmentOptions.Right), 90f);
                SegUIKit.Width(SegUIKit.Label(row, SegUIKit.Money(line.Total), 22f, null, FontStyles.Normal, TextAlignmentOptions.Right), 100f);
                SegUIKit.Width(SegUIKit.Label(row, "   " + LineStatusText(line), 20f, SegUIKit.FadedInk), 400f);
            }
        }

        bool overCap = order.SubtotalA > Riley.weeklyCap + 0.001f;
        Text("Subtotal: " + SegUIKit.Money(order.SubtotalA) + "  (must be no more than " + SegUIKit.Money(Riley.weeklyCap) + ")",
            22f, FontStyles.Bold, TextAlignmentOptions.Right, overCap ? SegUIKit.Warning : SegUIKit.Ink);
    }

    private void SectionB(CommissaryOrder order, bool editable)
    {
        Heading("SECTION B - SPECIAL COMMISSARY (Superintendent approval required)");

        if(!editable)
        {
            ReadOnlySingle(order.sectionB, order.sectionB != null ? "Reason: " + ReasonText(order.sectionB.reason) : "");
            return;
        }

        Text("Pick one item at most. Click it again to clear it.", 18f, FontStyles.Italic, TextAlignmentOptions.Left, SegUIKit.FadedInk);
        foreach(CommissaryItem item in commissary.Catalog.InSection(CommissarySection.Special))
        {
            string id = item.id;
            bool chosen = order.sectionB != null && order.sectionB.itemId == id;
            PickRow(item, chosen, () => commissary.SelectSpecial(id), "");
        }

        if(order.sectionB != null)
        {
            RectTransform reasons = SegUIKit.Row(content, 8f);
            SegUIKit.Width(SegUIKit.Label(reasons, "Reason for request:", 22f), 220f);
            foreach(SpecialReason reason in System.Enum.GetValues(typeof(SpecialReason)))
            {
                if(reason == SpecialReason.None) continue;
                SpecialReason picked = reason;
                bool selected = order.sectionB.reason == reason;
                SegUIKit.Button(reasons, SegUIKit.Check(selected) + " " + ReasonText(reason), () => commissary.SetSpecialReason(picked), -1f,
                    selected ? SegUIKit.SelectedColor : SegUIKit.ButtonColor);
            }
        }
    }

    private void SectionC(CommissaryOrder order, bool editable)
    {
        Heading("SECTION C - MEDICAL COMMISSARY (not counted toward " + SegUIKit.Money(Riley.weeklyCap) + ")");
        Text("Medications need a healthcare provider's approval in segregation.", 18f, FontStyles.Italic, TextAlignmentOptions.Left, SegUIKit.FadedInk);

        if(!editable)
        {
            ReadOnlySingle(order.sectionC, "");
            return;
        }

        foreach(CommissaryItem item in commissary.Catalog.InSection(CommissarySection.Medical))
        {
            string id = item.id;
            bool chosen = order.sectionC != null && order.sectionC.itemId == id;
            PickRow(item, chosen, () => commissary.SelectMedical(id), item.isMedication ? "medication - provider OK" : "supply");
        }
    }

    private void StaffBox(CommissaryOrder order)
    {
        Heading("--- FOR STAFF USE ---");

        bool decided = order.state == OrderState.Decided || order.state == OrderState.Closed;
        DecisionLine("Section B", "Superintendent", order.sectionB, decided);
        DecisionLine("Section C", "Provider", order.sectionC, decided);

        string deliveredA = order.sectionA.Count == 0 ? "-" : DeliveredText(order.sectionA[0], order.deliveryDayA);
        string deliveredBC = order.sectionB == null && order.sectionC == null ? "-" : "Day " + order.deliveryDayBC;
        Text("Delivered on:  A: " + deliveredA + "     B, C: " + deliveredBC, 22f);

        bool anyMissing = false;
        foreach(OrderLine line in order.AllLines)
        {
            if(line.status != LineStatus.Missing) continue;
            anyMissing = true;
            RectTransform row = SegUIKit.Row(content);
            SegUIKit.Width(SegUIKit.Label(row, "Missing: " + commissary.Item(line.itemId).displayName + " x" + line.missingQty, 22f, SegUIKit.Warning), 420f);
            if(line.fixDay < 0)
            {
                OrderLine missing = line;
                SegUIKit.Button(row, "Write a Request for Interview", () => commissary.FileDiscrepancyRequest(order, missing));
            }
            else
            {
                SegUIKit.Label(row, "Request filed. Expected " + SegCalendar.Describe(line.fixDay) + ".", 20f, SegUIKit.FadedInk);
            }
        }
        Text("Discrepancy noted: " + SegUIKit.Check(anyMissing) + " Yes  " + SegUIKit.Check(!anyMissing) + " No", 22f);
    }

    private void Footer(CommissaryOrder order, bool editable)
    {
        string problem = null;
        if(editable)
        {
            Text("Order total: " + SegUIKit.Money(order.Total) + "     Balance: " + SegUIKit.Money(Riley.balance), 24f, FontStyles.Bold, TextAlignmentOptions.Right);
            problem = commissary.Validate();
            if(problem != null) Text(problem, 20f, FontStyles.Italic, TextAlignmentOptions.Right, SegUIKit.Warning);
        }

        RectTransform buttons = SegUIKit.Row(content, 16f);
        buttons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
        if(editable)
        {
            SegUIKit.Button(buttons, "Sign and slide under the door", () => commissary.Sign(), -1f, SegUIKit.SelectedColor, problem == null);
        }
        SegUIKit.Button(buttons, "Close", Close, 140f);
    }

    // ---------- Small builders ----------

    private void PickRow(CommissaryItem item, bool chosen, System.Action onClick, string tag)
    {
        RectTransform row = SegUIKit.Row(content);
        SegUIKit.Button(row, SegUIKit.Check(chosen) + " " + item.displayName, onClick, 420f, chosen ? SegUIKit.SelectedColor : SegUIKit.ButtonColor);
        SegUIKit.Width(SegUIKit.Label(row, SegUIKit.Money(commissary.PriceOf(item)), 22f, null, FontStyles.Normal, TextAlignmentOptions.Right), 90f);
        SegUIKit.Width(SegUIKit.Label(row, "  " + item.effectText, 18f, SegUIKit.FadedInk), 330f);
        if(!string.IsNullOrEmpty(tag)) SegUIKit.Label(row, tag, 18f, SegUIKit.FadedInk, FontStyles.Italic);
    }

    private void ReadOnlySingle(OrderLine line, string extra)
    {
        if(line == null)
        {
            Text("Item requested: (none)", 22f, FontStyles.Italic, TextAlignmentOptions.Left, SegUIKit.FadedInk);
            return;
        }
        Text("Item requested: " + commissary.Item(line.itemId).displayName + "  " + SegUIKit.Money(line.price) + "     " + extra, 22f);
        Text(LineStatusText(line), 20f, FontStyles.Italic, TextAlignmentOptions.Left, SegUIKit.FadedInk);
    }

    private void DecisionLine(string section, string signer, OrderLine line, bool decided)
    {
        if(line == null)
        {
            Text(section + ":  -", 22f);
            return;
        }
        bool denied = line.status == LineStatus.Denied;
        bool approved = decided && !denied;
        string text = section + ":  " + SegUIKit.Check(approved) + " Approved  " + SegUIKit.Check(denied) + " Denied     "
            + signer + ": " + (decided ? "(initialed)" : "______");
        if(denied || line.status == LineStatus.BackOrdered) text += "     " + line.note;
        Text(text, 22f, FontStyles.Normal, TextAlignmentOptions.Left, denied ? SegUIKit.Warning : SegUIKit.Ink);
    }

    private static string DeliveredText(OrderLine line, int expectedDay)
    {
        return line.deliveredDay >= 0 ? "Day " + line.deliveredDay : "(expected Day " + expectedDay + ")";
    }

    private static string LineStatusText(OrderLine line)
    {
        switch(line.status)
        {
            case LineStatus.Pending: return "waiting for a decision";
            case LineStatus.Approved: return "approved, arrives " + SegCalendar.Describe(line.deliveryDay);
            case LineStatus.Denied: return "denied: " + line.note;
            case LineStatus.BackOrdered: return "back-ordered, now " + SegCalendar.Describe(line.deliveryDay);
            case LineStatus.Delivered: return "delivered " + SegCalendar.Describe(line.deliveredDay);
            case LineStatus.Missing: return line.missingQty + " missing from the " + SegCalendar.Describe(line.deliveredDay) + " delivery";
            default: return "";
        }
    }

    private static string ReasonText(SpecialReason reason)
    {
        return reason == SpecialReason.ContactWithFamily ? "Contact with family" : reason.ToString();
    }

    private void Heading(string text)
    {
        TextMeshProUGUI label = Text(text, 24f, FontStyles.Bold);
        label.margin = new Vector4(0f, 18f, 0f, 4f);
    }

    private TextMeshProUGUI Text(string text, float size, FontStyles style = FontStyles.Normal,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left, Color? color = null)
    {
        return SegUIKit.Label(content, text, size, color, style, alignment);
    }

    private void Finish(bool keepScroll, float scrollPosition)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scroll.verticalNormalizedPosition = keepScroll ? scrollPosition : 1f;
    }
}
