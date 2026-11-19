using System;
using System.Collections.Generic;
using UnityEngine;

public enum CommissarySection { Regular, Special, Medical }

public enum RileyStat { None, Hunger, Energy, Hygiene, Mood }

/// <summary>How likely the superintendent is to approve a special item for someone in seg. [design]</summary>
public enum ApprovalOdds { Likely, Maybe, Unlikely }

[Serializable]
public class CommissaryItem
{
    public string id;
    public string displayName;
    public CommissarySection section;
    [Tooltip("Base price on the week 1 price sheet. All prices are [design] placeholders.")]
    public float price;
    [Tooltip("Shown to the player on the price sheet / property box.")]
    public string effectText;

    [Header("Effect when Riley uses it")]
    public RileyStat stat;
    public int amount;
    [Tooltip("0 = permanent change; otherwise the bonus lasts this many days.")]
    public int durationDays;
    [Tooltip("Used up on use. Non-consumables (pencil, lotion, glasses) stay in the box and are checked with RileySegState.Has.")]
    public bool consumable = true;

    [Header("Section B (special)")]
    public ApprovalOdds approvalOdds;
    [Tooltip("One-line reason shown in the staff box when the request is denied.")]
    public string denialReason;

    [Header("Section C (medical)")]
    [Tooltip("Medications need a healthcare provider's sign-off in seg; supplies do not.")]
    public bool isMedication;

    public bool HasUseEffect => consumable || stat != RileyStat.None;
}

/// <summary>
/// The price sheet: every item Riley can put on the commissary RFI. One table, as the doc asks for.
/// Create one with Assets > Create > Lil Windows > Commissary Catalog; it fills in the doc's lists by default.
/// If none is assigned, <see cref="CommissarySystem"/> uses <see cref="DefaultItems"/> directly.
/// </summary>
[CreateAssetMenu(menuName = "Lil Windows/Commissary Catalog", fileName = "Commissary Catalog")]
public class CommissaryCatalog : ScriptableObject
{
    public List<CommissaryItem> items = DefaultItems();

    public CommissaryItem Find(string id)
    {
        return items.Find(item => item.id == id);
    }

    public List<CommissaryItem> InSection(CommissarySection section)
    {
        return items.FindAll(item => item.section == section);
    }

    void Reset()
    {
        items = DefaultItems();
    }

    public static CommissaryCatalog CreateDefault()
    {
        CommissaryCatalog catalog = CreateInstance<CommissaryCatalog>();
        catalog.name = "Commissary Catalog (default)";
        return catalog;
    }

    /// <summary>The trimmed lists from the Riley Playthrough doc (808.13 attachments B and C, plus generic regular items).</summary>
    public static List<CommissaryItem> DefaultItems()
    {
        return new List<CommissaryItem>
        {
            // Section A: regular commissary, $25/week cap. Generic stand-ins; the real 808.13a list wasn't linked.
            Regular("ramen", "Ramen soup", 0.50f, "Hunger -1", RileyStat.Hunger, -1, 0, true),
            Regular("instant_coffee", "Instant coffee", 4.00f, "Energy +1 for 5 days", RileyStat.Energy, 1, 5, true),
            Regular("writing_paper", "Writing paper (pad)", 2.00f, "Unlocks writing actions", RileyStat.None, 0, 0, false),
            Regular("pencil", "Pencil / flex pen", 0.75f, "Required to write", RileyStat.None, 0, 0, false),
            Regular("stamped_envelope", "Stamped envelope", 1.00f, "Send 1 letter", RileyStat.None, 0, 0, true),
            Regular("soap", "Soap bar", 1.25f, "Hygiene +1", RileyStat.Hygiene, 1, 0, true),
            Regular("shampoo", "Shampoo", 3.00f, "Hygiene +1", RileyStat.Hygiene, 1, 0, true),
            Regular("candy_bar", "Candy bar", 1.50f, "Mood +1 (one day)", RileyStat.Mood, 1, 1, true),
            Regular("lotion", "Lotion", 2.50f, "Dry-skin event blocked", RileyStat.None, 0, 0, false),

            // Section B: special commissary, one item, superintendent approval.
            Special("thermal_pants", "Thermal pants", 12.00f, ApprovalOdds.Likely, "Keeps the cell's cold off"),
            Special("crew_socks", "Crew socks", 4.00f, ApprovalOdds.Likely, "Warm feet"),
            Special("grey_sweatpants", "Grey sweatpants", 15.00f, ApprovalOdds.Likely, "Warmth"),
            Special("bath_towel", "Bath towel", 8.00f, ApprovalOdds.Likely, "Hygiene"),
            Special("shoe_laces", "Shoe laces", 1.00f, ApprovalOdds.Unlikely, "Not permitted in segregation (safety).", "Shoes stay on"),
            Special("radio", "Sony AM/FM radio w/ headphones", 35.00f, ApprovalOdds.Unlikely, "Electronics not approved in segregation.", "Outside voices"),
            Special("calculator", "8-digit calculator", 6.00f, ApprovalOdds.Maybe, "Not approved at this time.", "Study / letters"),
            Special("scrabble", "Scrabble", 18.00f, ApprovalOdds.Unlikely, "Games not approved in segregation.", "Something to do"),

            // Section C: medical commissary, one item, not counted toward the $25 cap.
            Medical("ibuprofen", "Ibuprofen 200mg (max 30)", 3.00f, true, "Pain relief"),
            Medical("acetaminophen", "Acetaminophen 500mg (max 25)", 3.00f, true, "Pain relief"),
            Medical("loratadine", "Loratadine (max 30)", 4.00f, true, "Allergy relief"),
            Medical("hydrocortisone", "Hydrocortisone cream", 3.50f, true, "Itch / rash relief"),
            Medical("artificial_tears", "Artificial tears", 4.00f, false, "Dry eyes relief"),
            Medical("lip_balm", "Lip balm", 1.50f, false, "Chapped lips relief"),
            Medical("earplugs", "Foam earplugs", 1.00f, false, "Better sleep on loud nights", consumable: false),
            Medical("reading_glasses", "Reading glasses", 6.00f, false, "Read library books without eye strain", consumable: false),
            Medical("sensitive_toothpaste", "Sensitive toothpaste", 3.50f, false, "Hygiene +1", RileyStat.Hygiene, 1),
            Medical("pads_tampons", "Maxi pads / tampons", 3.00f, false, "Hygiene supply"),
        };
    }

    private static CommissaryItem Regular(string id, string name, float price, string effect, RileyStat stat, int amount, int days, bool consumable)
    {
        return new CommissaryItem { id = id, displayName = name, section = CommissarySection.Regular, price = price, effectText = effect, stat = stat, amount = amount, durationDays = days, consumable = consumable };
    }

    private static CommissaryItem Special(string id, string name, float price, ApprovalOdds odds, string effect)
    {
        return Special(id, name, price, odds, "Denied by superintendent.", effect);
    }

    private static CommissaryItem Special(string id, string name, float price, ApprovalOdds odds, string denialReason, string effect)
    {
        //clothing and the like stay in the box rather than getting used up
        return new CommissaryItem { id = id, displayName = name, section = CommissarySection.Special, price = price, effectText = effect, approvalOdds = odds, denialReason = denialReason, consumable = false };
    }

    private static CommissaryItem Medical(string id, string name, float price, bool isMedication, string effect, RileyStat stat = RileyStat.None, int amount = 0, bool consumable = true)
    {
        return new CommissaryItem { id = id, displayName = name, section = CommissarySection.Medical, price = price, effectText = effect, isMedication = isMedication, stat = stat, amount = amount, consumable = consumable, denialReason = "Healthcare provider did not approve." };
    }
}
