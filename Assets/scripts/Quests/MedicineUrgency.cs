using PixelCrushers.DialogueSystem;
using UnityEngine;

/// <summary>
/// Puts a clock on OG's angina pills. When "Get Medicine" goes active, OG has <see cref="hoursAllowed"/> in-game
/// hours (nights count: lights out to wake-up is 8 hours gone). The HUD counts down, alerts escalate as time runs
/// out, and if the pills haven't reached OG by the deadline the quest fails: staff find him collapsed and carry
/// him to medical (he leaves the cell block).
///
/// Lua the dialogue can use: Variable["ogMedicineHoursLeft"] (number), Variable["ogCollapsed"] (bool),
/// Variable["hasOGMedicine"] (set by the nurse's conversation, cleared on hand-over).
/// Created automatically in any scene with a GameController and a Dialogue Manager.
/// </summary>
public class MedicineUrgency : MonoBehaviour
{
    public const string QuestName = "Get Medicine";
    private const string HudKey = "og-medicine";

    [Tooltip("In-game hours from accepting the quest to OG collapsing.")]
    public float hoursAllowed = 5f;
    [Tooltip("Name of OG's GameObject; hidden when he's taken to medical.")]
    public string ogObjectName = "OG";

    private GameController game;
    private float deadline = -1f;
    private bool warnedHalf;
    private bool warnedQuarter;
    private float nextPoll;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if(FindFirstObjectByType<MedicineUrgency>() != null) return;
        if(FindFirstObjectByType<GameController>() == null || FindFirstObjectByType<DialogueSystemController>() == null) return;
        new GameObject("Medicine Urgency").AddComponent<MedicineUrgency>();
    }

    void Update()
    {
        if(Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 0.25f;
        if(!DialogueManager.hasInstance) return;
        if(game == null) game = FindFirstObjectByType<GameController>();
        if(game == null || !game.IsDayStarted) return;

        QuestState state = QuestLog.GetQuestState(QuestName);
        if(state != QuestState.Active)
        {
            if(deadline >= 0f && GameHUD.Instance != null) GameHUD.Instance.ClearUrgent(HudKey);
            deadline = -1f;
            return;
        }

        if(deadline < 0f)
        {
            deadline = game.AbsoluteHour + hoursAllowed;
            warnedHalf = warnedQuarter = false;
        }

        float left = deadline - game.AbsoluteHour;
        DialogueLua.SetVariable("ogMedicineHoursLeft", left);
        if(left <= 0f)
        {
            Collapse();
            return;
        }

        float fraction = left / hoursAllowed;
        if(!warnedHalf && fraction <= 0.5f)
        {
            warnedHalf = true;
            DialogueManager.ShowAlert("OG is gray and sweating. He needs those pills soon.");
        }
        if(!warnedQuarter && fraction <= 0.25f)
        {
            warnedQuarter = true;
            DialogueManager.ShowAlert("You can hear OG wheezing from down the tier. Hurry.");
        }

        bool carrying = DialogueLua.GetVariable("hasOGMedicine").asBool;
        string task = carrying ? "Bring OG his angina pills" : "Get OG's angina pills";
        if(GameHUD.Instance != null) GameHUD.Instance.SetUrgent(HudKey, task + " - " + FormatLeft(left), 1f - fraction);
    }

    private void Collapse()
    {
        deadline = -1f;
        QuestLog.SetQuestState(QuestName, QuestState.Failure);
        DialogueLua.SetVariable("ogCollapsed", true);
        DialogueLua.SetVariable("hasOGMedicine", false);
        DialogueManager.ShowAlert("Too late. Staff found OG collapsed and carried him to medical.");
        if(GameHUD.Instance != null) GameHUD.Instance.ClearUrgent(HudKey);
        if(FirstPersonHands.Instance != null) FirstPersonHands.Instance.Drop();

        foreach(Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if(t.name == ogObjectName && t.GetComponent<Animator>() != null)
            {
                t.gameObject.SetActive(false);
                break;
            }
        }
    }

    private static string FormatLeft(float hours)
    {
        int minutes = Mathf.Max(0, Mathf.CeilToInt(hours * 60f));
        return (minutes / 60) + "h " + (minutes % 60).ToString("00") + "m left";
    }
}
