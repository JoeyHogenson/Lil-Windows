using System;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using UnityEngine;

/// <summary>
/// The weekly Commissary Request (RFI) from the Riley Playthrough doc. Each Monday an RFI opens for one day;
/// Riley fills Sections A (regular, $25 Phase I cap), B (one special item, superintendent approval) and
/// C (one medical item, provider approval for medications), then signs. Each order then moves through
/// fixed states on fixed days: decision Wednesday, regular items Thursday, special/medical items next Monday.
/// </summary>
public class CommissarySystem : SegDayListener
{
    [SerializeField] private RileySegState riley;
    [Tooltip("Left empty, the doc's default price sheet is used.")]
    [SerializeField] private CommissaryCatalog catalog;

    [Header("Timing (days after the RFI is signed) [design]")]
    [SerializeField] private int decisionDelayDays = 2;
    [SerializeField] private int regularDeliveryDelayDays = 3;
    [SerializeField] private int specialDeliveryDelayDays = 7;
    [Tooltip("Days for a written Request for Interview to fix a missing/wrong item.")]
    [SerializeField] private int discrepancyFixDelayDays = 2;

    [Header("Odds [design]")]
    [Range(0f, 1f)] [SerializeField] private float likelyApproval = 0.85f;
    [Range(0f, 1f)] [SerializeField] private float maybeApproval = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float unlikelyApproval = 0.1f;
    [Tooltip("Chance a healthcare provider signs off on a medication in seg. Supplies need no sign-off.")]
    [Range(0f, 1f)] [SerializeField] private float providerApproval = 0.75f;
    [Range(0f, 1f)] [SerializeField] private float backOrderChance = 0.15f;
    [Tooltip("Chance per delivery that one item is missing or wrong.")]
    [Range(0f, 1f)] [SerializeField] private float discrepancyChance = 0.10f;
    [Tooltip("Prices can change between weeks without notice: each week's price is base * (1 +/- this).")]
    [Range(0f, 0.5f)] [SerializeField] private float weeklyPriceDrift = 0.10f;

    [Header("Testing")]
    [Tooltip("0 = different every run. Any other value replays the same rolls.")]
    [SerializeField] private int randomSeed;

    private readonly List<CommissaryOrder> orders = new List<CommissaryOrder>();
    private readonly Dictionary<string, float> priceSheet = new Dictionary<string, float>();
    private System.Random rng;

    public event Action RfiOpened;
    public event Action RfiClosed;
    /// <summary>Raised for any change to any order (draft edits, signing, decisions, deliveries).</summary>
    public event Action<CommissaryOrder> OrderChanged;

    public CommissaryCatalog Catalog => catalog;
    public RileySegState Riley => riley;
    public IReadOnlyList<CommissaryOrder> Orders => orders;

    /// <summary>This week's RFI while it's open (Monday), whether or not it's been signed; otherwise null.</summary>
    public CommissaryOrder Current { get; private set; }
    public bool IsRfiOpen => Current != null;

    /// <summary>The order the form should show: the open one, or the most recent one.</summary>
    public CommissaryOrder DisplayOrder => Current ?? (orders.Count > 0 ? orders[orders.Count - 1] : null);

    void Awake()
    {
        if(riley == null) riley = GetComponent<RileySegState>();
        if(riley == null) riley = FindFirstObjectByType<RileySegState>();
        if(catalog == null) catalog = CommissaryCatalog.CreateDefault();
        rng = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
        RollPriceSheet(1);
    }

    // ---------- Queries ----------

    public CommissaryItem Item(string itemId)
    {
        return catalog.Find(itemId);
    }

    /// <summary>This week's price on the paper price sheet.</summary>
    public float PriceOf(CommissaryItem item)
    {
        return priceSheet.TryGetValue(item.id, out float price) ? price : item.price;
    }

    public bool IsEditable(CommissaryOrder order)
    {
        return order != null && order == Current && (order.state == OrderState.Draft || order.state == OrderState.RejectedFunds);
    }

    public int QuantityOf(string itemId)
    {
        OrderLine line = Current != null ? Current.sectionA.Find(l => l.itemId == itemId) : null;
        return line != null ? line.qty : 0;
    }

    /// <summary>Items signed for but not yet in the box. They need room when they arrive.</summary>
    public int IncomingItemCount
    {
        get
        {
            int count = 0;
            foreach(CommissaryOrder order in orders)
            {
                if(!order.IsSigned) continue;
                foreach(OrderLine line in order.AllLines)
                {
                    if(line.IsOutstanding) count += line.qty;
                    if(line.fixDay >= 0) count += line.missingQty;
                }
            }
            return count;
        }
    }

    /// <summary>Why the current draft can't be signed, or null if it can. Doesn't check funds; that's the officer's job.</summary>
    public string Validate()
    {
        CommissaryOrder order = Current;
        if(!IsEditable(order)) return "There's no open request form.";
        if(order.IsEmpty) return "The form is blank.";
        if(order.SubtotalA > riley.weeklyCap + 0.001f)
            return "Section A is over the Phase " + ToRoman(riley.phase) + " limit of $" + riley.weeklyCap.ToString("0.00") + ".";
        if(order.sectionB != null && order.sectionB.reason == SpecialReason.None)
            return "Section B needs a reason for the request.";
        int incoming = order.ItemCount + IncomingItemCount;
        if(incoming > riley.FreeSlots)
            return "That won't fit in your property box (" + riley.FreeSlots + " free, " + incoming + " incoming).";
        return null;
    }

    // ---------- Filling out the form (Draft only) ----------

    public void SetQuantity(string itemId, int qty)
    {
        if(!IsEditable(Current)) return;
        CommissaryItem item = Item(itemId);
        if(item == null || item.section != CommissarySection.Regular) return;

        qty = Mathf.Clamp(qty, 0, 99);
        OrderLine line = Current.sectionA.Find(l => l.itemId == itemId);
        if(qty == 0)
        {
            if(line != null) Current.sectionA.Remove(line);
        }
        else if(line == null)
        {
            Current.sectionA.Add(new OrderLine { itemId = itemId, qty = qty, price = PriceOf(item) });
        }
        else
        {
            line.qty = qty;
        }
        EditedDraft();
    }

    /// <summary>Picks (or, with null or the already-picked id, clears) the one Section B item.</summary>
    public void SelectSpecial(string itemId)
    {
        if(!IsEditable(Current)) return;
        Current.sectionB = PickSingle(Current.sectionB, itemId, CommissarySection.Special);
        EditedDraft();
    }

    public void SetSpecialReason(SpecialReason reason)
    {
        if(!IsEditable(Current) || Current.sectionB == null) return;
        Current.sectionB.reason = reason;
        EditedDraft();
    }

    /// <summary>Picks (or, with null or the already-picked id, clears) the one Section C item.</summary>
    public void SelectMedical(string itemId)
    {
        if(!IsEditable(Current)) return;
        Current.sectionC = PickSingle(Current.sectionC, itemId, CommissarySection.Medical);
        EditedDraft();
    }

    /// <summary>
    /// Riley signs and slides the form under the door. Returns false (with a reason posted) if the form is
    /// invalid or the officer returns it for insufficient funds.
    /// </summary>
    public bool Sign()
    {
        string problem = Validate();
        if(problem != null)
        {
            SegNotice.Post(problem);
            return false;
        }

        CommissaryOrder order = Current;
        if(!riley.TryDebit(order.Total))
        {
            order.state = OrderState.RejectedFunds;
            SegNotice.Post("The officer slides the form back: \"Insufficient funds.\"");
            RaiseChanged(order);
            return false;
        }

        order.state = OrderState.Submitted;
        order.submittedDay = Today;
        order.decisionDay = Today + decisionDelayDays;
        order.deliveryDayA = Today + regularDeliveryDelayDays;
        order.deliveryDayBC = Today + specialDeliveryDelayDays;
        foreach(OrderLine line in order.sectionA)
        {
            line.status = LineStatus.Approved;
            line.deliveryDay = order.deliveryDayA;
        }
        if(order.sectionB != null) order.sectionB.deliveryDay = order.deliveryDayBC;
        if(order.sectionC != null) order.sectionC.deliveryDay = order.deliveryDayBC;

        SegNotice.Post("You slide the form under the door. $" + order.Total.ToString("0.00") + " is held from your account.");
        RaiseChanged(order);
        return true;
    }

    /// <summary>
    /// Files a written Request for Interview about a missing/wrong item. Policy: no refunds or exchanges,
    /// so the fix is the only way to get it; it arrives a couple of days later.
    /// </summary>
    public void FileDiscrepancyRequest(CommissaryOrder order, OrderLine line)
    {
        if(line == null || line.status != LineStatus.Missing || line.fixDay >= 0) return;
        line.fixDay = Today + discrepancyFixDelayDays;
        line.note = "Request for Interview filed " + SegCalendar.Describe(Today);
        SegNotice.Post("You write up a Request for Interview about the missing " + Item(line.itemId).displayName + ".");
        RaiseChanged(order);
    }

    /// <summary>Opens an RFI today even if it isn't Monday (testing). It closes at the next day like any other.</summary>
    [ContextMenu("Open RFI Now (testing)")]
    public void OpenRfiNow()
    {
        if(Current == null) OpenRfi(Mathf.Max(1, Today));
    }

    // ---------- Day processing ----------

    protected override void OnDayStarted(int day)
    {
        CloseYesterdaysRfi(day);

        foreach(CommissaryOrder order in orders)
        {
            if(!order.IsSigned || order.state == OrderState.Closed) continue;
            bool changed = false;

            if(order.state == OrderState.Submitted && day > order.submittedDay)
            {
                order.state = OrderState.PendingDecision;
                changed = true;
            }
            if(order.state == OrderState.PendingDecision && day >= order.decisionDay)
            {
                Decide(order);
                changed = true;
            }
            changed |= DeliverDueLines(order, day);
            changed |= DeliverDueFixes(order, day);

            if(order.state == OrderState.Decided && !HasAnythingPending(order))
            {
                order.state = OrderState.Closed;
                changed = true;
            }
            if(changed) RaiseChanged(order);
        }

        if(SegCalendar.IsRfiDay(day)) OpenRfi(day);
    }

    private void CloseYesterdaysRfi(int day)
    {
        if(Current == null || day <= Current.openedDay) return;

        CommissaryOrder closing = Current;
        Current = null;
        if(!closing.IsSigned)
        {
            closing.state = OrderState.Missed;
            SegNotice.Post("You missed this week's commissary request. The next one comes Monday.");
            RaiseChanged(closing);
        }
        DialogueLua.SetVariable("rfiOpen", false);
        RfiClosed?.Invoke();
    }

    private void OpenRfi(int day)
    {
        int week = SegCalendar.WeekOf(day);
        if(week > 1) RollPriceSheet(week);

        Current = new CommissaryOrder { week = week, openedDay = day, state = OrderState.Draft };
        orders.Add(Current);

        DialogueLua.SetVariable("rfiOpen", true);
        SegNotice.Post("A commissary request form and a price sheet slide under your door. Week " + week + " of " + SegCalendar.RfiWeeks + ".");
        RfiOpened?.Invoke();
        RaiseChanged(Current);
    }

    private void Decide(CommissaryOrder order)
    {
        order.state = OrderState.Decided;

        OrderLine special = order.sectionB;
        if(special != null)
        {
            CommissaryItem item = Item(special.itemId);
            bool approved = Roll(ApprovalChance(item.approvalOdds));
            ResolveDecision(special, approved, item.denialReason);
            SegNotice.Post("Superintendent " + (approved ? "approved" : "denied") + " your " + item.displayName + ".");
        }

        OrderLine medical = order.sectionC;
        if(medical != null)
        {
            CommissaryItem item = Item(medical.itemId);
            bool approved = !item.isMedication || Roll(providerApproval);
            ResolveDecision(medical, approved, item.denialReason);
            if(approved && Roll(backOrderChance))
            {
                medical.status = LineStatus.BackOrdered;
                medical.note = "Unavailable - supply";
                medical.deliveryDay += 7;
                SegNotice.Post(item.displayName + " was approved but is back-ordered. Maybe next week.");
            }
            else
            {
                SegNotice.Post(item.displayName + (approved ? " was approved." : " was denied: " + item.denialReason));
            }
        }
    }

    private void ResolveDecision(OrderLine line, bool approved, string denialReason)
    {
        line.status = approved ? LineStatus.Approved : LineStatus.Denied;
        if(!approved)
        {
            line.note = denialReason;
            //the held money comes back for a denied request
            riley.Credit(line.Total);
        }
    }

    private bool DeliverDueLines(CommissaryOrder order, int day)
    {
        //each delivery day is one bag at the door; the discrepancy roll is per bag
        var bag = new List<OrderLine>();
        foreach(OrderLine line in order.AllLines)
        {
            bool deliverable = line.status == LineStatus.Approved || line.status == LineStatus.BackOrdered;
            //Section B/C lines aren't decided until decision day, so Pending never delivers
            if(deliverable && line.deliveryDay >= 0 && day >= line.deliveryDay) bag.Add(line);
        }
        if(bag.Count == 0) return false;

        OrderLine shorted = Roll(discrepancyChance) ? bag[rng.Next(bag.Count)] : null;
        var received = new List<string>();
        foreach(OrderLine line in bag)
        {
            int arrived = line.qty;
            if(line == shorted)
            {
                line.missingQty = 1;
                arrived -= 1;
                line.status = LineStatus.Missing;
                line.note = "Discrepancy: 1 missing or wrong";
            }
            else
            {
                line.status = LineStatus.Delivered;
            }
            line.deliveredDay = day;
            riley.AddItem(line.itemId, arrived);
            if(arrived > 0) received.Add(Item(line.itemId).displayName + (arrived > 1 ? " x" + arrived : ""));
        }

        SegNotice.Post("Commissary bag at the door. You sign for: " + (received.Count > 0 ? string.Join(", ", received) : "nothing") + ".");
        if(shorted != null)
        {
            SegNotice.Post("Something's wrong: a " + Item(shorted.itemId).displayName + " is missing. Fixing it takes a written request.");
        }
        return true;
    }

    private bool DeliverDueFixes(CommissaryOrder order, int day)
    {
        bool changed = false;
        foreach(OrderLine line in order.AllLines)
        {
            if(line.status != LineStatus.Missing || line.fixDay < 0 || day < line.fixDay) continue;
            riley.AddItem(line.itemId, line.missingQty);
            SegNotice.Post("Your Request for Interview went through. The missing " + Item(line.itemId).displayName + " arrives.");
            line.missingQty = 0;
            line.fixDay = -1;
            line.status = LineStatus.Delivered;
            line.note = "Corrected " + SegCalendar.Describe(day);
            changed = true;
        }
        return changed;
    }

    private static bool HasAnythingPending(CommissaryOrder order)
    {
        foreach(OrderLine line in order.AllLines)
        {
            if(line.IsOutstanding) return true;
            //an unfiled discrepancy can still be fixed, so the order stays open
            if(line.status == LineStatus.Missing) return true;
        }
        return false;
    }

    // ---------- Helpers ----------

    private OrderLine PickSingle(OrderLine existing, string itemId, CommissarySection section)
    {
        if(string.IsNullOrEmpty(itemId) || (existing != null && existing.itemId == itemId)) return null;
        CommissaryItem item = Item(itemId);
        if(item == null || item.section != section) return existing;
        return new OrderLine { itemId = itemId, qty = 1, price = PriceOf(item), reason = existing != null ? existing.reason : SpecialReason.None };
    }

    private void RollPriceSheet(int week)
    {
        priceSheet.Clear();
        foreach(CommissaryItem item in catalog.items)
        {
            float price = item.price;
            if(week > 1 && weeklyPriceDrift > 0f)
            {
                float multiplier = 1f + ((float)rng.NextDouble() * 2f - 1f) * weeklyPriceDrift;
                //rounded to the nickel, like a printed sheet
                price = Mathf.Max(0.05f, Mathf.Round(item.price * multiplier * 20f) / 20f);
            }
            priceSheet[item.id] = price;
        }
    }

    private float ApprovalChance(ApprovalOdds odds)
    {
        switch(odds)
        {
            case ApprovalOdds.Likely: return likelyApproval;
            case ApprovalOdds.Maybe: return maybeApproval;
            default: return unlikelyApproval;
        }
    }

    private bool Roll(float chance)
    {
        return rng.NextDouble() < chance;
    }

    private void EditedDraft()
    {
        //editing a returned form puts it back to a draft so it can be signed again
        if(Current.state == OrderState.RejectedFunds) Current.state = OrderState.Draft;
        RaiseChanged(Current);
    }

    private void RaiseChanged(CommissaryOrder order)
    {
        OrderChanged?.Invoke(order);
    }

    public static string ToRoman(int phase)
    {
        switch(phase)
        {
            case 1: return "I";
            case 2: return "II";
            case 3: return "III";
            default: return phase.ToString();
        }
    }
}
