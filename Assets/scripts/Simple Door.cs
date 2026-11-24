using UnityEngine;

/// <summary>
/// A hinged door the player opens with E (<see cref="PlayerLogic"/>). Swings <see cref="Door"/> around the
/// vertical axis between the rotation it starts in (closed) and <see cref="openAngle"/> degrees from it.
/// <see cref="isOpen"/> flips as soon as it's toggled; the swing follows, and toggling mid-swing turns it around
/// from where it is.
/// </summary>
public class SimpleDoor : MonoBehaviour
{
    [Tooltip("The part that swings (pivot at the hinge). Empty = this object.")]
    public GameObject Door;
    public bool isOpen;

    [Tooltip("Degrees around the vertical axis when open. Negative and positive swing opposite ways.")]
    [SerializeField] private float openAngle = -90f;
    [Tooltip("Seconds for a full swing.")]
    [SerializeField, Min(0.01f)] private float swingDuration = 0.6f;
    [Tooltip("Shape of the swing over time (0..1 both ways). Default eases in and out.")]
    [SerializeField] private AnimationCurve swingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Transform hinge;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    //0 = closed, 1 = open
    private float progress;

    void Awake()
    {
        hinge = Door != null ? Door.transform : transform;
        closedRotation = hinge.localRotation;
        //around the parent's up axis, like the old "localEulerAngles.y += angle", so tilted models (e.g. Blender
        //imports rotated -90 on X) still swing flat
        openRotation = Quaternion.Euler(0f, openAngle, 0f) * closedRotation;
    }

    void Start()
    {
        //doors are authored closed
        isOpen = false;
        progress = 0f;
    }

    void Update()
    {
        float target = isOpen ? 1f : 0f;
        if(Mathf.Approximately(progress, target)) return;

        progress = Mathf.MoveTowards(progress, target, Time.deltaTime / swingDuration);
        hinge.localRotation = Quaternion.SlerpUnclamped(closedRotation, openRotation, swingCurve.Evaluate(progress));
    }

    public void Open()
    {
        isOpen = true;
    }

    public void Close()
    {
        isOpen = false;
    }

    /// <summary>True while the door is still moving.</summary>
    public bool IsSwinging => !Mathf.Approximately(progress, isOpen ? 1f : 0f);
}
