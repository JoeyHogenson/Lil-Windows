using System;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using UnityEngine;

/// <summary>
/// Riley's side of the segregation month: PIP phase, account balance, the property box
/// (commissary items and library books share its slots), and the simple stats items affect.
/// </summary>
public class RileySegState : SegDayListener
{
    [Serializable]
    public class OwnedItem
    {
        public string itemId;
        public int count;
    }

    [Serializable]
    private class TimedEffect
    {
        public RileyStat stat;
        public int amount;
        public int expiresOnDay;
    }

    [Header("Account (doc: starting balance $60, +$20 family deposit mid-month) [design]")]
    [Tooltip("Prisoner Incentive Program phase. Segregation locks Riley to Phase I.")]
    public int phase = 1;
    public float balance = 60f;
    [Tooltip("Phase I regular-commissary limit per week (AKDOC 808.13).")]
    public float weeklyCap = 25f;
    public int familyDepositDay = 15;
    public float familyDepositAmount = 20f;

    [Header("Property box")]
    [Tooltip("Stands in for the one-property-box limit. Books and delivered items share these slots. [design]")]
    public int cellSlots = 12;
    public List<OwnedItem> property = new List<OwnedItem>();
    public List<string> books = new List<string>();

    [Header("Stats (base values; timed effects stack on top)")]
    public int hunger;
    public int energy;
    public int hygiene;
    public int mood;

    [SerializeField] private List<TimedEffect> timedEffects = new List<TimedEffect>();

    /// <summary>Raised whenever balance, property, books or stats change. UI listens to this.</summary>
    public event Action Changed;

    public int SlotsUsed
    {
        get
        {
            int used = books.Count;
            foreach(OwnedItem owned in property) used += owned.count;
            return used;
        }
    }

    public int FreeSlots => Mathf.Max(0, cellSlots - SlotsUsed);

    public bool Has(string itemId)
    {
        return Count(itemId) > 0;
    }

    public int Count(string itemId)
    {
        OwnedItem owned = property.Find(o => o.itemId == itemId);
        return owned != null ? owned.count : 0;
    }

    public int GetStat(RileyStat stat)
    {
        int value = BaseStat(stat);
        foreach(TimedEffect effect in timedEffects)
        {
            if(effect.stat == stat) value += effect.amount;
        }
        return value;
    }

    // ---------- Money ----------

    public bool CanAfford(float amount)
    {
        //rounded to cents so float drift never blocks an exact-change order
        return Mathf.Round(amount * 100f) <= Mathf.Round(balance * 100f);
    }

    public bool TryDebit(float amount)
    {
        if(!CanAfford(amount)) return false;
        balance = RoundCents(balance - amount);
        NotifyChanged();
        return true;
    }

    public void Credit(float amount)
    {
        balance = RoundCents(balance + amount);
        NotifyChanged();
    }

    // ---------- Property ----------

    public void AddItem(string itemId, int qty)
    {
        if(qty <= 0) return;
        OwnedItem owned = property.Find(o => o.itemId == itemId);
        if(owned == null)
        {
            owned = new OwnedItem { itemId = itemId };
            property.Add(owned);
        }
        owned.count += qty;
        NotifyChanged();
    }

    public bool RemoveItem(string itemId, int qty = 1)
    {
        OwnedItem owned = property.Find(o => o.itemId == itemId);
        if(owned == null || owned.count < qty) return false;
        owned.count -= qty;
        if(owned.count == 0) property.Remove(owned);
        NotifyChanged();
        return true;
    }

    /// <summary>Applies the item's effect and, if it's consumable, removes one from the box.</summary>
    public bool UseItem(CommissaryItem item)
    {
        if(item == null || !Has(item.id) || !item.HasUseEffect) return false;

        if(item.stat != RileyStat.None && item.amount != 0)
        {
            if(item.durationDays > 0)
            {
                timedEffects.Add(new TimedEffect { stat = item.stat, amount = item.amount, expiresOnDay = Today + item.durationDays });
            }
            else
            {
                SetBaseStat(item.stat, BaseStat(item.stat) + item.amount);
            }
        }

        if(item.consumable) RemoveItem(item.id);
        else NotifyChanged();
        return true;
    }

    /// <summary>Permanently shifts a base stat (e.g. +mood from reading).</summary>
    public void AdjustStat(RileyStat stat, int amount)
    {
        if(stat == RileyStat.None || amount == 0) return;
        SetBaseStat(stat, BaseStat(stat) + amount);
        NotifyChanged();
    }

    public void AddBook(string title)
    {
        books.Add(title);
        NotifyChanged();
    }

    public void RemoveBook(string title)
    {
        if(books.Remove(title)) NotifyChanged();
    }

    // ---------- Days ----------

    protected override void OnDayStarted(int day)
    {
        timedEffects.RemoveAll(effect => day >= effect.expiresOnDay);

        if(day == familyDepositDay && familyDepositAmount > 0f)
        {
            Credit(familyDepositAmount);
            SegNotice.Post("Your family put $" + familyDepositAmount.ToString("0.00") + " on your account.");
        }
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        //lets conversations react ("rileyBalance", "rileyBooks")
        DialogueLua.SetVariable("rileyBalance", balance);
        DialogueLua.SetVariable("rileyBooks", books.Count);
        Changed?.Invoke();
    }

    private int BaseStat(RileyStat stat)
    {
        switch(stat)
        {
            case RileyStat.Hunger: return hunger;
            case RileyStat.Energy: return energy;
            case RileyStat.Hygiene: return hygiene;
            case RileyStat.Mood: return mood;
            default: return 0;
        }
    }

    private void SetBaseStat(RileyStat stat, int value)
    {
        switch(stat)
        {
            case RileyStat.Hunger: hunger = value; break;
            case RileyStat.Energy: energy = value; break;
            case RileyStat.Hygiene: hygiene = value; break;
            case RileyStat.Mood: mood = value; break;
        }
    }

    public static float RoundCents(float amount)
    {
        return Mathf.Round(amount * 100f) / 100f;
    }
}
