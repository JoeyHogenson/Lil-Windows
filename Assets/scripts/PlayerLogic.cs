using UnityEngine;
using UnityEngine.Serialization;
using PixelCrushers.DialogueSystem;

namespace StarterAssets
{
/// <summary>
/// Player-side game logic: looking at and interacting with doors/teleporters, the pause menu,
/// and keeping the OS cursor in sync with whether the player is in a UI or free-looking.
/// </summary>
public class PlayerLogic : MonoBehaviour
{
    private enum InteractionType { None, Door, Teleporter }

    private const float InteractRange = 15f;

    [Header("Player Stats")]
    public int socialCred;
    public int troubleMeter;
    public int mentalHealth;

    [Header("UI")]
    [SerializeField] private GameObject eToOpenButton;
    [SerializeField] private GameObject eToCloseButton;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject blueImage;
    [Tooltip("Intro/tutorial overlays. The pause menu can't open until these are cleared.")]
    [FormerlySerializedAs("CanvasObjects")]
    [SerializeField] private GameObject[] canvasObjects;

    private StarterAssetsInputs _input;
    private Camera _camera;
    private bool _canvasCleared;

    //what the player is currently looking at within range; refreshed every frame
    private InteractionType _lookTarget;
    private SimpleDoor _lookedAtDoor;
    private InteractTele _lookedAtTeleporter;
    //last in-range collider, kept even after looking away so dialogue options still reach that NPC
    private Collider _lastInRangeCollider;

    void Awake()
    {
        _input = GetComponent<StarterAssetsInputs>();
    }

    void Start()
    {
        _camera = Camera.main;
        _input.cursorInputForLook = true;
        _input.cursorLocked = true;
        ApplyCursorState();
    }

    private void OnEnable()
    {
        //guarded so a scene without a Dialogue Manager doesn't throw here
        if(DialogueManager.hasInstance)
        {
            DialogueManager.instance.conversationStarted += OnConversationStarted;
            DialogueManager.instance.conversationEnded += OnConversationEnded;
        }
    }

    private void OnDisable()
    {
        if(DialogueManager.hasInstance)
        {
            DialogueManager.instance.conversationStarted -= OnConversationStarted;
            DialogueManager.instance.conversationEnded -= OnConversationEnded;
        }
    }

    //driven off the Dialogue System's own C# events instead of per-conversation Inspector
    //UnityEvents, since not every conversation/ending (e.g. walking away, some dialogue
    //options) has those wired up to call LockCursor()/UnlockCursor()
    private void OnConversationStarted(Transform actor)
    {
        _input.cursorInputForLook = false;
    }

    //fires however the conversation ended (completed, StopConversation(), interrupted, ...)
    //so look input can't get stuck disabled by a dialogue
    private void OnConversationEnded(Transform actor)
    {
        if(!IsMenuOpen)
        {
            _input.cursorInputForLook = true;
        }
    }

    void Update()
    {
        ApplyCursorState();
        UpdateLookTarget();
    }

    // ---------- Public API (also wired to UnityEvents in scenes; don't rename) ----------

    /// <summary>Handles the interact key (E): opens/closes the looked-at door or uses a teleporter.</summary>
    public void Interact()
    {
        switch(_lookTarget)
        {
            case InteractionType.Door:
                ToggleDoor(_lookedAtDoor);
                break;
            case InteractionType.Teleporter:
                Teleport(_lookedAtTeleporter);
                break;
        }
    }

    /// <summary>
    /// Handles the menu key (Escape): closes an open newspaper or RFI form if there is one; otherwise ends any
    /// conversation and toggles the pause menu (which only opens once the intro overlays are cleared).
    /// </summary>
    public void Menu()
    {
        if(Newspaper.CurrentlyOpen != null)
        {
            Newspaper.CurrentlyOpen.ShutNewspaper();
            return;
        }

        //otherwise the pause menu opens over the form, and closing it re-locks the cursor
        //while the form is still up, leaving it unclickable
        if(RFI.CurrentlyOpen != null)
        {
            RFI.CurrentlyOpen.Close();
            UnlockCursor();
            return;
        }

        DialogueManager.StopConversation();
        SetActiveSafe(blueImage, false);

        bool openMenu = !IsMenuOpen && _canvasCleared;
        if(menuPanel != null) menuPanel.SetActive(openMenu);
        _input.cursorInputForLook = !openMenu;
    }

    /// <summary>Advances the last-targeted NPC's legacy dialogue with option 1.</summary>
    public void Option1()
    {
        DialogueController dialogue = _lastInRangeCollider != null ? _lastInRangeCollider.GetComponent<DialogueController>() : null;
        if(dialogue == null)
        {
            Debug.LogWarning("[PlayerLogic] Option1: no DialogueController on the last targeted object.");
            return;
        }
        dialogue.nextLineOption1();
    }

    /// <summary>
    /// Enters UI mode: disables mouse-look and frees/shows the cursor.
    /// (Named for locking the camera, not the cursor.)
    /// </summary>
    public void LockCursor()
    {
        _input.cursorInputForLook = false;
    }

    /// <summary>
    /// Leaves UI mode: re-enables mouse-look and locks/hides the cursor.
    /// (Named for unlocking the camera, not the cursor.)
    /// </summary>
    public void UnlockCursor()
    {
        _input.cursorInputForLook = true;
    }

    /// <summary>Hides the intro overlays, after which the pause menu is allowed to open.</summary>
    public void ClearCanvas()
    {
        if(canvasObjects == null) return;
        foreach(GameObject canvasObject in canvasObjects)
        {
            SetActiveSafe(canvasObject, false);
        }
        _canvasCleared = true;
    }

    // ---------- Internals ----------

    private bool IsMenuOpen
    {
        get { return menuPanel != null && menuPanel.activeSelf; }
    }

    //reasserted every frame instead of only on toggle: in the Editor, Escape while locked
    //triggers Unity's own auto-unlock, which races with the menu toggle and otherwise leaves
    //the cursor stuck until something (e.g. alt-tab) forces Windows to resync it.
    //driven off cursorInputForLook because that's the flag every UI already toggles
    //(menu, dialogue, newspaper, LockCursor/UnlockCursor)
    private void ApplyCursorState()
    {
        bool lookEnabled = _input.cursorInputForLook;
        Cursor.lockState = lookEnabled ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !lookEnabled;
    }

    private void UpdateLookTarget()
    {
        _lookTarget = InteractionType.None;
        _lookedAtDoor = null;
        _lookedAtTeleporter = null;

        //camera can be swapped by scene loads/cutscenes, so re-resolve when the cached one is gone
        if(_camera == null) _camera = Camera.main;

        if(_camera != null)
        {
            Ray ray = _camera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));
            if(Physics.Raycast(ray, out RaycastHit hit, InteractRange))
            {
                _lastInRangeCollider = hit.collider;
                ClassifyTarget(hit.collider);
            }
        }

        SetActiveSafe(eToOpenButton, ShowsOpenPrompt());
        SetActiveSafe(eToCloseButton, _lookTarget == InteractionType.Door && _lookedAtDoor.isOpen);
    }

    private void ClassifyTarget(Collider target)
    {
        _lookedAtTeleporter = target.GetComponent<InteractTele>();
        if(_lookedAtTeleporter != null)
        {
            _lookTarget = InteractionType.Teleporter;
            return;
        }

        _lookedAtDoor = target.GetComponent<SimpleDoor>();
        if(_lookedAtDoor != null)
        {
            _lookTarget = InteractionType.Door;
        }
    }

    private bool ShowsOpenPrompt()
    {
        return _lookTarget == InteractionType.Teleporter
            || (_lookTarget == InteractionType.Door && !_lookedAtDoor.isOpen);
    }

    private static void ToggleDoor(SimpleDoor door)
    {
        if(door.isOpen)
        {
            door.Close();
        }
        else
        {
            door.Open();
        }
    }

    private void Teleport(InteractTele teleporter)
    {
        if(teleporter.targetLocation == null)
        {
            Debug.LogWarning("[PlayerLogic] Teleporter " + teleporter.name + " has no target location.");
            return;
        }
        transform.position = teleporter.targetLocation.transform.position;
    }

    private static void SetActiveSafe(GameObject target, bool active)
    {
        if(target != null) target.SetActive(active);
    }
}
}
