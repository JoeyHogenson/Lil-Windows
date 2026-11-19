using StarterAssets;
using UnityEngine;

/// <summary>
/// Base for the segregation panels (commissary form, library cart, property box). Builds its own canvas,
/// frees the cursor while open, and registers itself so Escape (PlayerLogic.Menu) closes it.
/// </summary>
public abstract class SegPanel : MonoBehaviour
{
    public static SegPanel CurrentlyOpen { get; private set; }

    [Tooltip("Canvas sort order. Higher draws on top.")]
    [SerializeField] private int sortingOrder = 50;

    private GameObject root;
    private PlayerLogic playerLogic;

    public bool IsOpen => root != null && root.activeSelf;

    protected virtual void Awake()
    {
        playerLogic = FindFirstObjectByType<PlayerLogic>();
        Canvas canvas = SegUIKit.CreateCanvas(GetType().Name + " Canvas", sortingOrder);
        canvas.transform.SetParent(transform, false);
        root = SegUIKit.Shade(canvas.transform).gameObject;
        Build(root.transform);
        root.SetActive(false);
    }

    /// <summary>Builds the panel's fixed parts under <paramref name="root"/> (a full-screen shade).</summary>
    protected abstract void Build(Transform root);

    public void Open()
    {
        if(CurrentlyOpen != null && CurrentlyOpen != this) CurrentlyOpen.Close();
        root.SetActive(true);
        CurrentlyOpen = this;
        if(playerLogic != null) playerLogic.LockCursor();
        OnOpened();
    }

    public virtual void Close()
    {
        if(!IsOpen) return;
        root.SetActive(false);
        if(CurrentlyOpen == this) CurrentlyOpen = null;
        if(playerLogic != null) playerLogic.UnlockCursor();
    }

    public void Toggle()
    {
        if(IsOpen) Close();
        else Open();
    }

    protected virtual void OnOpened() { }

    protected virtual void OnDestroy()
    {
        if(CurrentlyOpen == this) CurrentlyOpen = null;
    }
}
