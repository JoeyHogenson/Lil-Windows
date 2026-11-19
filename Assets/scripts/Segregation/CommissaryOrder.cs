using System;
using System.Collections.Generic;

/// <summary>Where a weekly RFI is in its lifecycle (doc: "State logic for Joey").</summary>
public enum OrderState
{
    /// <summary>RFI is open and Riley is filling it out.</summary>
    Draft,
    /// <summary>Officer returned it: balance was below the order total at signing. Still editable while the RFI is open.</summary>
    RejectedFunds,
    /// <summary>Signed and slid under the door; the total is held from the balance.</summary>
    Submitted,
    /// <summary>The day after submitting. Nothing happens. Waiting.</summary>
    PendingDecision,
    /// <summary>Sections B and C have been approved or denied (Wednesday).</summary>
    Decided,
    /// <summary>Every line is delivered, denied, or written off.</summary>
    Closed,
    /// <summary>The RFI day passed without Riley signing. Riley waits a full week.</summary>
    Missed,
}

public enum LineStatus { Pending, Approved, Denied, BackOrdered, Delivered, Missing }

public enum SpecialReason { None, Hygiene, Warmth, Education, Health, ContactWithFamily }

/// <summary>One item on the form. Section A can have several; B and C have at most one each.</summary>
[Serializable]
public class OrderLine
{
    public string itemId;
    public int qty = 1;
    /// <summary>Unit price locked in from that week's price sheet.</summary>
    public float price;
    public LineStatus status;
    /// <summary>Staff note: denial reason, "Unavailable - supply", discrepancy, etc.</summary>
    public string note;
    public int deliveryDay = -1;
    public int deliveredDay = -1;
    /// <summary>Units that didn't arrive (discrepancy). Fixed only by a written Request for Interview.</summary>
    public int missingQty;
    /// <summary>Day the Request for Interview fix arrives; -1 if no request filed.</summary>
    public int fixDay = -1;
    /// <summary>Section B only.</summary>
    public SpecialReason reason;

    public float Total => price * qty;
    public bool IsOutstanding => status == LineStatus.Pending || status == LineStatus.Approved || status == LineStatus.BackOrdered;
}

/// <summary>One weekly Commissary Request (RFI). Mirrors the doc's suggested data shape.</summary>
[Serializable]
public class CommissaryOrder
{
    public int week;
    public OrderState state;
    public List<OrderLine> sectionA = new List<OrderLine>();
    public OrderLine sectionB;
    public OrderLine sectionC;

    public int openedDay;
    public int submittedDay = -1;
    public int decisionDay = -1;
    public int deliveryDayA = -1;
    public int deliveryDayBC = -1;

    public float SubtotalA
    {
        get
        {
            float total = 0f;
            foreach(OrderLine line in sectionA) total += line.Total;
            return total;
        }
    }

    public float Total => SubtotalA + (sectionB != null ? sectionB.Total : 0f) + (sectionC != null ? sectionC.Total : 0f);

    public bool IsSigned => submittedDay >= 0;

    public IEnumerable<OrderLine> AllLines
    {
        get
        {
            foreach(OrderLine line in sectionA) yield return line;
            if(sectionB != null) yield return sectionB;
            if(sectionC != null) yield return sectionC;
        }
    }

    public int ItemCount
    {
        get
        {
            int count = 0;
            foreach(OrderLine line in AllLines) count += line.qty;
            return count;
        }
    }

    public bool IsEmpty => sectionA.Count == 0 && sectionB == null && sectionC == null;
}
