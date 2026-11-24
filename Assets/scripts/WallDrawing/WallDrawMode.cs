using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Drawing on a <see cref="WallCanvas"/>: the player stands still, the cursor is freed, and holding the left mouse
/// button draws. Escape or Done leaves. Creates itself the first time a canvas is used. Being a SegPanel gives it
/// the cursor handling, Escape, and closing when another panel opens.
/// </summary>
public class WallDrawMode : SegPanel
{
    private const float EraserScale = 3f;
    //being moved further than this (count, cutscene, teleport) ends drawing; in player heights
    private const float MaxDrift = 0.5f;

    private static readonly (string name, float diameter)[] Sizes = { ("Thin", 1f), ("Medium", 3f), ("Thick", 7f) };

    private static WallDrawMode instance;

    private RectTransform toolbar;
    private readonly List<Image> penButtons = new List<Image>();
    private readonly List<Image> sizeButtons = new List<Image>();
    private int pen;
    private int size;

    private WallCanvas canvas;
    private bool stroking;
    private Vector2 lastPixel;

    private PlayerLogic player;
    private StarterAssetsInputs input;
    private FirstPersonController walker;
    private Selector[] selectors = new Selector[0];
    private bool frozen;
    private readonly List<Selector> pausedSelectors = new List<Selector>();
    private float moveSpeed, sprintSpeed, jumpHeight;
    private Vector3 enteredAt;
    private float maxDrift;

    //the eraser sits after the pens
    private bool Erasing => pen >= canvas.pens.Length;

    public static void Enter(WallCanvas canvas)
    {
        if(canvas == null) return;
        if(instance == null) instance = new GameObject("Wall Draw Mode").AddComponent<WallDrawMode>();
        instance.EnterCanvas(canvas);
    }

    protected override void Awake()
    {
        base.Awake();
        player = FindFirstObjectByType<PlayerLogic>();
        if(player == null) return;
        input = player.GetComponent<StarterAssetsInputs>();
        walker = player.GetComponent<FirstPersonController>();
        selectors = player.GetComponentsInChildren<Selector>();
    }

    protected override void Build(Transform root)
    {
        //no dimming: the player needs to see the wall
        Image shade = root.GetComponent<Image>();
        shade.color = Color.clear;
        shade.raycastTarget = false;

        toolbar = SegUIKit.Row(root, 10f);
        toolbar.name = "Draw Toolbar";
        toolbar.anchorMin = toolbar.anchorMax = toolbar.pivot = new Vector2(0.5f, 0f);
        toolbar.anchoredPosition = new Vector2(0f, 28f);
        toolbar.gameObject.AddComponent<Image>().color = SegUIKit.Paper;
        toolbar.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(16, 16, 12, 12);
        ContentSizeFitter fitter = toolbar.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void EnterCanvas(WallCanvas target)
    {
        if(IsOpen || player == null) return;
        //some other UI (pause menu, newspaper, dialogue) already has the cursor
        if(input != null && !input.cursorInputForLook) return;

        canvas = target;
        stroking = false;
        Freeze();
        Open();
    }

    protected override void OnOpened()
    {
        pen = Mathf.Clamp(pen, 0, canvas.pens.Length);
        BuildToolbar();
    }

    public override void Close()
    {
        if(!IsOpen) return;
        stroking = false;
        Unfreeze();
        base.Close();
    }

    protected override void OnDestroy()
    {
        if(instance == this) instance = null;
        Unfreeze();
        base.OnDestroy();
    }

    void Update()
    {
        if(!IsOpen) return;

        if(canvas == null || !canvas.isActiveAndEnabled
            || (player.transform.position - enteredAt).sqrMagnitude > maxDrift * maxDrift)
        {
            Close();
            return;
        }
        if(DialogueManager.hasInstance && DialogueManager.isConversationActive)
        {
            Close();
            //Close hands the mouse back to looking around, but the conversation needs the cursor
            player.LockCursor();
            return;
        }
        Draw();
    }

    private void Draw()
    {
        Mouse mouse = Mouse.current;
        Camera view = Camera.main;
        if(mouse == null || view == null || !mouse.leftButton.isPressed)
        {
            stroking = false;
            return;
        }

        Vector2 pointer = mouse.position.ReadValue();
        bool pressed = mouse.leftButton.wasPressedThisFrame;
        //a press on the toolbar never starts a stroke, even if dragged off it
        if((pressed && IsOverToolbar(pointer)) || (!pressed && !stroking)) return;

        if(!canvas.TryGetPixel(view.ScreenPointToRay(pointer), out Vector2 pixel))
        {
            stroking = false;
            return;
        }
        if(pressed) lastPixel = pixel;
        stroking = true;

        float diameter = Sizes[size].diameter;
        if(Erasing) canvas.Erase(lastPixel, pixel, diameter * EraserScale);
        else canvas.Stroke(lastPixel, pixel, diameter, canvas.pens[pen].color);
        lastPixel = pixel;
    }

    private bool IsOverToolbar(Vector2 pointer)
    {
        Canvas ui = toolbar.GetComponentInParent<Canvas>().rootCanvas;
        Camera uiCamera = ui.renderMode == RenderMode.ScreenSpaceOverlay ? null : ui.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(toolbar, pointer, uiCamera);
    }

    // ---------- Holding the player still ----------

    private void Freeze()
    {
        if(frozen) return;
        frozen = true;
        enteredAt = player.transform.position;
        CharacterController body = player.GetComponent<CharacterController>();
        maxDrift = MaxDrift * (body != null ? body.height * Mathf.Abs(player.transform.lossyScale.y) : 2f);

        if(walker != null)
        {
            moveSpeed = walker.MoveSpeed;
            sprintSpeed = walker.SprintSpeed;
            jumpHeight = walker.JumpHeight;
            walker.MoveSpeed = 0f;
            walker.SprintSpeed = 0f;
            walker.JumpHeight = 0f;
        }
        //hides the "(E to draw)" prompt over the wall, and E/right click can't use anything meanwhile
        pausedSelectors.Clear();
        foreach(Selector selector in selectors)
        {
            if(selector == null || !selector.enabled) continue;
            selector.enabled = false;
            pausedSelectors.Add(selector);
        }
    }

    private void Unfreeze()
    {
        if(!frozen) return;
        frozen = false;
        if(walker != null)
        {
            walker.MoveSpeed = moveSpeed;
            walker.SprintSpeed = sprintSpeed;
            walker.JumpHeight = jumpHeight;
        }
        foreach(Selector selector in pausedSelectors)
        {
            if(selector != null) selector.enabled = true;
        }
        pausedSelectors.Clear();
    }

    // ---------- Toolbar ----------

    //rebuilt per canvas, since each can offer different pens
    private void BuildToolbar()
    {
        SegUIKit.Clear(toolbar);
        penButtons.Clear();
        sizeButtons.Clear();

        for(int i = 0; i < canvas.pens.Length; i++) AddPen(canvas.pens[i].name, i);
        AddPen("Eraser", canvas.pens.Length);
        AddGap();
        for(int i = 0; i < Sizes.Length; i++)
        {
            int index = i;
            sizeButtons.Add(AddButton(Sizes[i].name, () => { size = index; Highlight(); }));
        }
        AddGap();
        AddButton("Done (Esc)", Close);
        Highlight();
    }

    private void AddPen(string label, int index)
    {
        penButtons.Add(AddButton(label, () => { pen = index; Highlight(); }));
    }

    private Image AddButton(string label, System.Action onClick)
    {
        Button button = SegUIKit.Button(toolbar, label, onClick);
        //keeps the movement keys from stepping through the toolbar (fully named: the project has its own Navigation)
        button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        return button.image;
    }

    private void AddGap()
    {
        SegUIKit.Width(SegUIKit.Rect("Gap", toolbar), 16f);
    }

    private void Highlight()
    {
        for(int i = 0; i < penButtons.Count; i++) penButtons[i].color = i == pen ? SegUIKit.SelectedColor : SegUIKit.ButtonColor;
        for(int i = 0; i < sizeButtons.Count; i++) sizeButtons[i].color = i == size ? SegUIKit.SelectedColor : SegUIKit.ButtonColor;
    }
}
