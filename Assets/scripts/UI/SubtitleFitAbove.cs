using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps a top-anchored subtitle panel from growing down over the response menu. Replaces the panel's
/// ContentSizeFitter: the panel is sized to its text's preferred height, capped at the space above
/// <see cref="limit"/> (the response menu; the bottom of the parent when the menu is hidden). If the text still
/// doesn't fit, the font steps down until it does (never below <see cref="minFontScale"/> of its size).
/// Works in the panel's local space, so overlay, camera and world canvases all behave the same.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SubtitleFitAbove : MonoBehaviour
{
    [Tooltip("Stay above this (the response menu panel). When it's hidden, the parent's bottom edge is used.")]
    public RectTransform limit;
    [Tooltip("The subtitle text whose font shrinks when it can't fit. Legacy Text or TextMeshPro.")]
    public Graphic text;
    [Tooltip("Space kept between this panel and the limit, in this panel's units.")]
    public float gap = 8f;
    [Range(0.3f, 1f)] public float minFontScale = 0.6f;

    private RectTransform rect;
    private float baseFontSize = -1f;
    private string lastText;
    private float lastAvailable = -1f;

    void Awake()
    {
        rect = (RectTransform)transform;
        //this component does the sizing now; a fitter would overwrite it during the layout pass
        var fitter = GetComponent<ContentSizeFitter>();
        if(fitter != null) fitter.enabled = false;
    }

    void OnEnable()
    {
        lastText = null;
    }

    void LateUpdate()
    {
        if(text == null) return;
        if(baseFontSize < 0f) baseFontSize = GetFontSize();

        float available = AvailableHeight();
        string current = GetText();
        if(current == lastText && Mathf.Abs(available - lastAvailable) < 0.5f) return;
        lastText = current;
        lastAvailable = available;

        float size = baseFontSize;
        float minSize = Mathf.Max(8f, baseFontSize * minFontScale);
        SetFontSize(size);
        float preferred = PreferredHeight();
        while(preferred > available && size > minSize)
        {
            size = Mathf.Max(minSize, size - 1f);
            SetFontSize(size);
            preferred = PreferredHeight();
        }
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Min(preferred, available));
    }

    private float PreferredHeight()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        return LayoutUtility.GetPreferredHeight(rect);
    }

    //from this panel's top edge down to the limit's top edge (or the parent's bottom), in local units
    private float AvailableHeight()
    {
        var corners = new Vector3[4];
        float floor;
        if(limit != null && limit.gameObject.activeInHierarchy)
        {
            limit.GetWorldCorners(corners);
            floor = rect.InverseTransformPoint(corners[1]).y; //top-left of the limit
        }
        else if(rect.parent is RectTransform parent)
        {
            parent.GetWorldCorners(corners);
            floor = rect.InverseTransformPoint(corners[0]).y; //bottom-left of the parent
        }
        else
        {
            return float.MaxValue;
        }
        //measured from the anchored top, which doesn't move as the height changes
        float top = rect.rect.yMax;
        return Mathf.Max(0f, top - floor - gap);
    }

    private float GetFontSize()
    {
        if(text is Text legacy) return legacy.fontSize;
        if(text is TMPro.TMP_Text tmp) return tmp.fontSize;
        return 14f;
    }

    private void SetFontSize(float size)
    {
        if(text is Text legacy) legacy.fontSize = Mathf.RoundToInt(size);
        else if(text is TMPro.TMP_Text tmp) tmp.fontSize = size;
    }

    private string GetText()
    {
        if(text is Text legacy) return legacy.text;
        if(text is TMPro.TMP_Text tmp) return tmp.text;
        return string.Empty;
    }
}
