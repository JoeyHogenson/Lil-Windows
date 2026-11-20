using System;
using System.Collections;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using StarterAssets;
using TMPro;
using UnityEngine;

/// <summary>
/// Lets every playthrough share one scene. At the start of play it asks who the player is (or restores the
/// choice from the save file), then: toggles <see cref="PlaythroughOnly"/> objects, moves the player to that
/// character's cell, points the GameController's count zone/cell at it, sets the Lua variable "playthrough"
/// for conversations, and finally raises <see cref="Selected"/>, which starts day 1.
/// </summary>
public class PlaythroughDirector : MonoBehaviour
{
    [Serializable]
    public class PlaythroughOption
    {
        public PlayableCharacter character;
        public string displayName;
        [TextArea(2, 4)] public string description;
        [Tooltip("Where this character starts each day (their cell). Left empty, the GameController's current start position is kept.")]
        public Transform spawnPoint;
        [Tooltip("The zone this character must stand in during count. Left empty, the GameController's current zone is kept.")]
        public Collider countZone;
        [Tooltip("Optional Dialogue System conversation to start right after choosing.")]
        public string startConversation;
    }

    /// <summary>Dialogue System Lua variable holding the chosen character's name, e.g. Variable["playthrough"] == "Riley".</summary>
    public const string LuaVariable = "playthrough";
    /// <summary>Easy Save key the choice is stored under, next to the auto-save data.</summary>
    public const string SaveKey = "LilWindows.playthrough";

    /// <summary>Set before loading the scene (e.g. from a main menu) to skip the in-scene choice.</summary>
    public static PlayableCharacter? PendingSelection;

    /// <summary>The character chosen this run, or null before the choice is made.</summary>
    public static PlayableCharacter? Current { get; private set; }

    /// <summary>True while the character select screen is up. PlayerLogic ignores Escape while it is.</summary>
    public static bool IsChoosing { get; private set; }

    public static PlaythroughDirector Instance { get; private set; }

    [SerializeField] private List<PlaythroughOption> options = new List<PlaythroughOption>
    {
        new PlaythroughOption
        {
            character = PlayableCharacter.DC,
            displayName = "DC",
            description = "First time inside. Deaf, new to general population, and sharing a cell with Chico.",
        },
        new PlaythroughOption
        {
            character = PlayableCharacter.Riley,
            displayName = "Riley",
            description = "30 days in segregation. Almost anything can be requested; every answer runs on someone else's clock.",
        },
    };

    [Tooltip("Continue a saved run as the character it was saved with instead of asking again.")]
    [SerializeField] private bool restoreFromSave = true;
    [Tooltip("Freeze game time (NPCs, physics, the day clock) while the select screen is up.")]
    [SerializeField] private bool pauseWhileChoosing = true;
    [Tooltip("Canvas sort order of the select screen.")]
    [SerializeField] private int sortingOrder = 200;

    [Header("Scene References (found automatically when empty)")]
    [SerializeField] private GameController game;
    [SerializeField] private PlayerLogic playerLogic;

    /// <summary>Raised once, after the chosen character's content is in place.</summary>
    public event Action<PlayableCharacter> Selected;

    public bool HasSelected => Current.HasValue;
    public IReadOnlyList<PlaythroughOption> Options => options;

    private GameObject selectScreen;
    private float timeScaleBeforeChoosing = 1f;

    //statics survive Play mode restarts when domain reload is off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        PendingSelection = null;
        Current = null;
        IsChoosing = false;
        Instance = null;
    }

    void Awake()
    {
        Instance = this;
        Current = null;
        if(game == null) game = FindFirstObjectByType<GameController>();
        if(playerLogic == null) playerLogic = FindFirstObjectByType<PlayerLogic>();
    }

    IEnumerator Start()
    {
        //one frame so every other Start (PlayerLogic's cursor setup, Easy Save's Start load) has run first
        yield return null;

        if(TryGetSavedCharacter(out PlayableCharacter saved))
        {
            Select(saved);
        }
        else if(PendingSelection.HasValue)
        {
            PlayableCharacter pending = PendingSelection.Value;
            PendingSelection = null;
            Select(pending);
        }
        else if(options.Count == 1)
        {
            Select(options[0].character);
        }
        else
        {
            ShowSelectScreen();
        }
    }

    void OnDestroy()
    {
        if(Instance == this)
        {
            Instance = null;
            IsChoosing = false;
        }
    }

    /// <summary>Starts the run as <paramref name="character"/>. Does nothing after the first call.</summary>
    public void Select(PlayableCharacter character)
    {
        if(HasSelected)
        {
            Debug.LogWarning("[PlaythroughDirector] Already playing as " + Current + "; ignoring Select(" + character + ").");
            return;
        }

        Current = character;
        HideSelectScreen();
        ApplyPlaythroughOnly(character);

        PlaythroughOption option = FindOption(character);
        if(option != null && game != null)
        {
            game.SetPlayerCell(option.spawnPoint, option.countZone);
            if(option.spawnPoint != null) game.TeleportPlayerTo(option.spawnPoint);
        }

        DialogueLua.SetVariable(LuaVariable, character.ToString());
        Debug.Log("[PlaythroughDirector] Playing as " + character);

        Selected?.Invoke(character);

        if(option != null && !string.IsNullOrEmpty(option.startConversation) && DialogueManager.hasInstance)
        {
            Transform actor = playerLogic != null ? playerLogic.transform : null;
            DialogueManager.StartConversation(option.startConversation, actor);
        }
    }

    /// <summary>Wired from buttons/UnityEvents that only pass an int.</summary>
    public void Select(int character)
    {
        Select((PlayableCharacter)character);
    }

    /// <summary>Writes the choice into the save file. Called by GameController.SaveGame before Easy Save commits.</summary>
    public static void WriteToSave(ES3Settings settings)
    {
        if(Current.HasValue) ES3.Save(SaveKey, (int)Current.Value, settings);
    }

    // ---------- Internals ----------

    private PlaythroughOption FindOption(PlayableCharacter character)
    {
        return options.Find(o => o.character == character);
    }

    private bool TryGetSavedCharacter(out PlayableCharacter character)
    {
        character = default;
        if(!restoreFromSave) return false;

        ES3AutoSaveMgr saveManager = ES3AutoSaveMgr.Current;
        //only when this scene actually loads saves; otherwise an old file would skip the choice forever
        if(saveManager == null || saveManager.loadEvent == ES3AutoSaveMgr.LoadEvent.None) return false;

        try
        {
            if(!ES3.KeyExists(SaveKey, saveManager.settings)) return false;
            int saved = ES3.Load<int>(SaveKey, saveManager.settings);
            if(!Enum.IsDefined(typeof(PlayableCharacter), saved)) return false;
            character = (PlayableCharacter)saved;
            return true;
        }
        catch(Exception e)
        {
            Debug.LogWarning("[PlaythroughDirector] Couldn't read the saved playthrough: " + e.Message);
            return false;
        }
    }

    private static void ApplyPlaythroughOnly(PlayableCharacter character)
    {
        PlaythroughOnly[] tagged = FindObjectsByType<PlaythroughOnly>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach(PlaythroughOnly only in tagged)
        {
            only.gameObject.SetActive(only.AppearsFor(character));
        }
    }

    // ---------- Select screen ----------

    private void ShowSelectScreen()
    {
        IsChoosing = true;
        if(pauseWhileChoosing)
        {
            timeScaleBeforeChoosing = Time.timeScale;
            Time.timeScale = 0f;
        }
        if(playerLogic != null) playerLogic.LockCursor();

        if(selectScreen == null) selectScreen = BuildSelectScreen();
        selectScreen.SetActive(true);
    }

    private void HideSelectScreen()
    {
        if(!IsChoosing) return;
        IsChoosing = false;
        if(selectScreen != null) selectScreen.SetActive(false);
        if(pauseWhileChoosing) Time.timeScale = timeScaleBeforeChoosing;
        if(playerLogic != null) playerLogic.UnlockCursor();
    }

    private GameObject BuildSelectScreen()
    {
        Canvas canvas = SegUIKit.CreateCanvas("Character Select Canvas", sortingOrder);
        canvas.transform.SetParent(transform, false);

        RectTransform shade = SegUIKit.Shade(canvas.transform);
        shade.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.92f);

        float cardWidth = 460f;
        float panelWidth = Mathf.Max(640f, options.Count * (cardWidth + 28f) + 56f);
        RectTransform panel = SegUIKit.Panel(shade, "Character Select", new Vector2(panelWidth, 520f), SegUIKit.Paper);
        SegUIKit.Column(panel, 18f, 36);

        SegUIKit.Label(panel, "STATE OF ALASKA - DEPARTMENT OF CORRECTIONS", 20f, SegUIKit.FadedInk, FontStyles.Normal, TextAlignmentOptions.Center);
        SegUIKit.Label(panel, "Who are you?", 44f, SegUIKit.Ink, FontStyles.Bold, TextAlignmentOptions.Center);

        RectTransform row = SegUIKit.Row(panel, 28f);
        row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;

        foreach(PlaythroughOption option in options)
        {
            PlayableCharacter character = option.character;
            RectTransform card = SegUIKit.Rect(option.displayName + " Card", row);
            card.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.88f, 0.86f, 0.79f);
            SegUIKit.Column(card, 14f, 22);
            SegUIKit.Width(card, cardWidth).GetComponent<UnityEngine.UI.LayoutElement>().minHeight = 300f;

            string title = string.IsNullOrEmpty(option.displayName) ? character.ToString() : option.displayName;
            SegUIKit.Label(card, title, 36f, SegUIKit.Ink, FontStyles.Bold, TextAlignmentOptions.Left);
            TextMeshProUGUI blurb = SegUIKit.Label(card, option.description, 22f, SegUIKit.Ink);
            blurb.textWrappingMode = TextWrappingModes.Normal;
            blurb.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleHeight = 1f;
            SegUIKit.Button(card, "Play as " + title, () => Select(character));
        }

        return shade.gameObject;
    }
}
