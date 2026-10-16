using System;
using System.Collections;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Runs the in-game day cycle: schedules each day's timed events (mail, count, commissary,
/// end of day, ...) relative to when the day started, and fires each one once.
/// </summary>
public class GameController : MonoBehaviour
{
    /// <summary>A day event that fires once when Time.time reaches <see cref="Time"/>.</summary>
    private class ScheduledEvent
    {
        public string Name;
        public float Time;
        //the event is skipped if this passes before it fires (e.g. a window the frame skipped over)
        public float ExpiresAt = float.PositiveInfinity;
        //evaluated at fire time, not schedule time, since dayCount can change mid-day
        public Func<bool> Condition;
        public Action Fire;
        public bool HasFired;
    }

    private const string JailSceneName = "New Jail Scene";
    private const float EventTextDuration = 5f;
    private const float CountWarningLeadTime = 5f;
    private const float CountGuardWalkDuration = 8f;
    private const float CountGuardSpeed = 0.03f;
    private const float EndOfDayFadeDelay = 10f;

    [Header("Day Length")]
    [Tooltip("Real-time seconds per in-game day. The next day starts exactly this long after the current one did.")]
    public float dayLengthSeconds = 300f;

    [Header("Day Schedule (seconds after the day starts)")]
    [SerializeField] private float thoughtsOffset = 20f;
    [SerializeField] private float bookBanOffset = 20f;
    [SerializeField] private float lockdownOffset = 20f;
    [SerializeField] private float mailOffset = 60f;
    [Tooltip("Clamped so the count deadline never falls after the end-of-day warning.")]
    [SerializeField] private float countOffset = 300f;
    [SerializeField] private float commissaryOffset = 1000f;

    [Header("Story Events")]
    [Tooltip("Book ban is imposed on day 2 and lifted on day 3.")]
    [SerializeField] private bool enableBookBan = false;
    [Tooltip("Lockdown triggers on day 2.")]
    [SerializeField] private bool enableLockdown = false;

    [Header("Scene References")]
    [SerializeField] private Collider countzone;
    [Tooltip("NPCs reset to their starting positions each night. Element 0 is the guard who walks the count.")]
    [FormerlySerializedAs("Characters")]
    [SerializeField] private GameObject[] characters;
    [SerializeField] private GameObject player;
    [SerializeField] private Transform playerStartingPosition;
    [SerializeField] private Animator lockdownPrisonerAnimator;
    [SerializeField] private GameObject blackSquare;
    [SerializeField] private GameObject thoughtsObject;
    [SerializeField] private TextMeshProUGUI eventText;
    [SerializeField] private GameObject eventTextObject;
    [SerializeField] private GameObject mailObject;
    [SerializeField] private GameObject bookBanNewspaper;
    [SerializeField] private GameObject intakeNewspaper;
    [FormerlySerializedAs("Books")]
    [SerializeField] private GameObject[] books;

    [Header("Day State")]
    [Tooltip("Top-left UI text that shows the current day.")]
    public TextMeshProUGUI dayCountText;
    [Tooltip("Format for dayCountText; {0} is replaced with the day number.")]
    public string dayCountFormat = "Day {0}";
    /// <summary>Current day, starting at 1 ("Sunday Morning"). Read by the calendar and intake.</summary>
    public int dayCount = 1;
    /// <summary>Time.time at which the current day ends and the next one starts. Read by <see cref="Clock"/>.</summary>
    public float end;
    /// <summary>True while a lockdown is in effect. Read by NPC navigation.</summary>
    public bool isLockdown;

    public bool IsDayStarted { get; private set; }
    /// <summary>Time.time at which the current day started. Read by <see cref="Clock"/>.</summary>
    public float DayStartTime => dayStartTime;
    public bool IsCommissary { get; private set; }
    /// <summary>Whether the player was inside the count zone at the last count deadline.</summary>
    public bool IsCount { get; private set; }

    private float dayStartTime;
    private readonly List<ScheduledEvent> schedule = new List<ScheduledEvent>();

    void Start()
    {
        Log("Start: dayCount=" + dayCount + " (StartDay will " + (dayCount != 1 ? "" : "NOT ") + "be called from Start)");
        UpdateDayCountText();
        //day 1 is started by IntakeControls once the intake sequence finishes
        if(dayCount != 1)
        {
            StartDay();
        }
    }

    void Update()
    {
        if(IsDayStarted)
        {
            RunDueEvents();
        }
    }

    /// <summary>
    /// Begins a new day from the current time: resets per-day state and schedules the day's events.
    /// Called on wake-up (after intake on day 1, after the end-of-day fade otherwise).
    /// </summary>
    public void StartDay()
    {
        dayStartTime = Time.time;
        ResetDayState();
        BuildSchedule();

        SetActiveSafe(eventTextObject, false);
        SetActiveSafe(mailObject, false);
        SetActiveSafe(blackSquare, false);
        UpdateDayCountText();
        IsDayStarted = true;

        Log("StartDay: dayCount=" + dayCount + " daystart=" + dayStartTime + " end=" + end
            + "\ncalled from:\n" + new System.Diagnostics.StackTrace(true));
    }

    /// <summary>Day of the week for <see cref="dayCount"/>; day 1 is a Sunday.</summary>
    public DayOfWeek GetDayOfWeek()
    {
        return (DayOfWeek)((dayCount - 1) % 7);
    }

    /// <summary>Loads the main jail scene. Wired to menu buttons.</summary>
    public void LoadScene()
    {
        SceneManager.LoadScene(JailSceneName);
    }

    /// <summary>Quits the application. Wired to menu buttons.</summary>
    public void Quit()
    {
        Application.Quit();
    }

    // ---------- Scheduling ----------

    private void ResetDayState()
    {
        IsCommissary = false;
        IsCount = false;
        DialogueLua.SetVariable("bookBan", false);
        DialogueLua.SetVariable("lockdown", false);
    }

    //list order is also the firing order for events that come due on the same frame
    //(count deadline is clamped to the end-of-day warning, so when they coincide the end-of-day text wins)
    private void BuildSchedule()
    {
        end = dayStartTime + dayLengthSeconds;
        //the warning goes up EndOfDayFadeDelay before the end so the fade finishes exactly at `end`
        float endOfDayWarningTime = Mathf.Max(dayStartTime, end - EndOfDayFadeDelay);
        float countTime = Mathf.Min(dayStartTime + countOffset, endOfDayWarningTime);

        schedule.Clear();
        schedule.Add(new ScheduledEvent { Name = "CountWarning", Time = countTime - CountWarningLeadTime, ExpiresAt = countTime, Fire = CountWarningEvent });
        schedule.Add(new ScheduledEvent { Name = "CountDeadline", Time = countTime, Fire = CountDeadlineEvent });
        schedule.Add(new ScheduledEvent { Name = "Commissary", Time = dayStartTime + commissaryOffset, Fire = CommissaryEvent });
        schedule.Add(new ScheduledEvent { Name = "Mail", Time = dayStartTime + mailOffset, Fire = MailEvent });
        schedule.Add(new ScheduledEvent { Name = "EndOfDay", Time = endOfDayWarningTime, Fire = EndOfDayEvent });
        schedule.Add(new ScheduledEvent { Name = "Thoughts", Time = dayStartTime + thoughtsOffset, Fire = ThoughtsEvent });
        schedule.Add(new ScheduledEvent { Name = "BookBan", Time = dayStartTime + bookBanOffset, Condition = () => enableBookBan && (dayCount == 2 || dayCount == 3), Fire = BookBanEvent });
        schedule.Add(new ScheduledEvent { Name = "Lockdown", Time = dayStartTime + lockdownOffset, Condition = () => enableLockdown && dayCount == 2, Fire = LockdownEvent });

        Log("BuildSchedule: daystart=" + dayStartTime + " end=" + end);
    }

    private void RunDueEvents()
    {
        float now = Time.time;
        foreach(ScheduledEvent scheduled in schedule)
        {
            if(scheduled.HasFired || now < scheduled.Time || now >= scheduled.ExpiresAt)
            {
                continue;
            }
            //a false condition consumes the event so it isn't re-checked every frame for the rest of the day
            scheduled.HasFired = true;
            if(scheduled.Condition == null || scheduled.Condition())
            {
                scheduled.Fire();
            }
        }
    }

    // ---------- Day events ----------

    private void CountWarningEvent()
    {
        GameObject guard = GetCountGuard();
        if(guard != null)
        {
            SetGuardWalking(guard, true);
            StartCoroutine(StopGuardAfterSeconds(guard, CountGuardWalkDuration));
        }
        ShowEventText("Count is occurring. Stand at your cell door.");
        Log("CountWarning triggered at " + Time.time);
    }

    private void CountDeadlineEvent()
    {
        IsCount = IsPlayerInCountZone();
        ShowEventText(IsCount ? "Count complete" : "You failed count and have been punished");
        Log("CountDeadline: player " + (IsCount ? "IN zone -> Count complete" : "NOT in zone -> Failed count"));
        CancelInvoke(nameof(HideEventText));
        Invoke(nameof(HideEventText), EventTextDuration);
    }

    private void CommissaryEvent()
    {
        //shows whatever text is already loaded; the commissary message itself is currently disabled
        SetActiveSafe(eventTextObject, true);
        IsCommissary = true;
        TeleportPlayer(playerStartingPosition);
    }

    private void MailEvent()
    {
        SetActiveSafe(mailObject, true);
        Log("MailEvent triggered at " + Time.time);
    }

    private void EndOfDayEvent()
    {
        ShowEventText("The end of the day is nearing. Get to your cell or you will be teleported there.");
        Log("EndOfDayEvent triggered at Time.time=" + Time.time + " (dayCount " + dayCount + " -> " + (dayCount + 1) + ")");
        //incremented now rather than after the fade so the calendar flips as the warning appears
        dayCount++;
        UpdateDayCountText();
        //waits until `end` exactly, so each day lasts dayLengthSeconds regardless of the warning lead time
        StartCoroutine(EndDayAfterSeconds(Mathf.Max(0f, end - Time.time)));
    }

    private void ThoughtsEvent()
    {
        //the thought text is currently disabled; this only clears any thought bubble left on screen
        StartCoroutine(DeactivateAfterSeconds(thoughtsObject, EventTextDuration));
    }

    private void BookBanEvent()
    {
        bool banImposed = dayCount == 2;
        SetActiveSafe(bookBanNewspaper, banImposed);
        if(banImposed)
        {
            SetActiveSafe(intakeNewspaper, false);
        }
        DialogueLua.SetVariable("bookBan", banImposed);
        SetAllActive(books, !banImposed);
        ShowEventText(banImposed ? "A book ban has now been instated" : "A book ban has now been removed");
        StartCoroutine(DeactivateAfterSeconds(eventTextObject, EventTextDuration));
    }

    private void LockdownEvent()
    {
        isLockdown = true;
        SetActiveSafe(bookBanNewspaper, false);
        SetActiveSafe(intakeNewspaper, false);
        DialogueLua.SetVariable("lockdown", true);
        if(lockdownPrisonerAnimator != null)
        {
            lockdownPrisonerAnimator.SetBool("getUp", true);
            lockdownPrisonerAnimator.SetBool("isSitting", false);
        }
        ShowEventText("There is now a lockdown. Get to your cell immediately.");
        StartCoroutine(DeactivateAfterSeconds(eventTextObject, EventTextDuration));
    }

    // ---------- Helpers ----------

    private bool IsPlayerInCountZone()
    {
        if(countzone == null || player == null)
        {
            Debug.LogWarning("[GameController] Count zone check failed: countzone or player not assigned.");
            return false;
        }
        //compares the player's pivot (feet), not their whole collider, against the zone
        bool inZone = countzone.bounds.Contains(player.transform.position);
        Log("CountZoneCheck: player position=" + player.transform.position + " countzone.bounds=" + countzone.bounds + " inZone=" + inZone);
        return inZone;
    }

    private void TeleportPlayer(Transform destination)
    {
        if(player == null || destination == null)
        {
            Debug.LogWarning("[GameController] Cannot teleport player: player or destination not assigned.");
            return;
        }
        //the CharacterController overwrites transform.position while active, so toggle it off around the move
        player.SetActive(false);
        player.transform.position = destination.position;
        player.SetActive(true);
    }

    private GameObject GetCountGuard()
    {
        return characters != null && characters.Length > 0 ? characters[0] : null;
    }

    private static void SetGuardWalking(GameObject guard, bool walking)
    {
        NPCController npc = guard.GetComponent<NPCController>();
        if(npc != null) npc.NPCspeed = walking ? CountGuardSpeed : 0f;

        Animator animator = guard.GetComponent<Animator>();
        if(animator != null) animator.SetBool("isWalking", walking);
    }

    private void ResetCharactersToStart()
    {
        if(characters == null) return;
        foreach(GameObject character in characters)
        {
            NPCController npc = character != null ? character.GetComponent<NPCController>() : null;
            if(npc == null) continue;
            character.transform.position = npc.startingPosition;
            npc.NPCspeed = 0f;
        }
    }

    private void UpdateDayCountText()
    {
        if(dayCountText != null) dayCountText.text = string.Format(dayCountFormat, dayCount);
    }

    private void ShowEventText(string message)
    {
        if(eventText != null) eventText.text = message;
        SetActiveSafe(eventTextObject, true);
    }

    //called by name via Invoke, so it must stay a parameterless method
    private void HideEventText()
    {
        SetActiveSafe(eventTextObject, false);
    }

    private static void SetActiveSafe(GameObject target, bool active)
    {
        if(target != null) target.SetActive(active);
    }

    private static void SetAllActive(GameObject[] targets, bool active)
    {
        if(targets == null) return;
        foreach(GameObject target in targets)
        {
            SetActiveSafe(target, active);
        }
    }

    private static void Log(string message)
    {
        Debug.Log("[GameController] " + message);
    }

    // ---------- Coroutines ----------

    private IEnumerator StopGuardAfterSeconds(GameObject guard, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if(guard == null) yield break;
        SetGuardWalking(guard, false);
        //turn around to face the cells after walking up to them
        guard.transform.Rotate(0f, 180f, 0f);
    }

    private IEnumerator EndDayAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        SetActiveSafe(eventTextObject, false);
        SetActiveSafe(blackSquare, true);
        ResetCharactersToStart();
        //note: StartDay hides blackSquare again on this same frame
        StartDay();
    }

    private static IEnumerator DeactivateAfterSeconds(GameObject target, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        SetActiveSafe(target, false);
    }
}
