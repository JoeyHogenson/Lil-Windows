using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helpers for building the segregation UI in code, so the systems work in any scene without hand-built
/// prefabs. Restyle by changing the colors here, or replace a panel with a hand-built one later.
/// Text sticks to ASCII so the default TMP font renders everything.
/// </summary>
public static class SegUIKit
{
    public static readonly Color Paper = new Color(0.95f, 0.93f, 0.86f);
    public static readonly Color Ink = new Color(0.12f, 0.12f, 0.14f);
    public static readonly Color FadedInk = new Color(0.42f, 0.42f, 0.44f);
    public static readonly Color Warning = new Color(0.7f, 0.12f, 0.1f);
    public static readonly Color ButtonColor = new Color(0.8f, 0.77f, 0.68f);
    public static readonly Color SelectedColor = new Color(0.56f, 0.72f, 0.56f);
    public static readonly Color ShadeColor = new Color(0f, 0f, 0f, 0.55f);

    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>Full-screen dim layer that also blocks clicks to whatever is behind the panel.</summary>
    public static RectTransform Shade(Transform parent)
    {
        RectTransform rect = Rect("Shade", parent);
        Stretch(rect);
        rect.gameObject.AddComponent<Image>().color = ShadeColor;
        return rect;
    }

    /// <summary>A centered panel of the given size.</summary>
    public static RectTransform Panel(Transform parent, string name, Vector2 size, Color color)
    {
        RectTransform rect = Rect(name, parent);
        rect.sizeDelta = size;
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    public static VerticalLayoutGroup Column(RectTransform rect, float spacing, int padding)
    {
        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static RectTransform Row(Transform parent, float spacing = 12f)
    {
        RectTransform rect = Rect("Row", parent);
        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return rect;
    }

    public static TextMeshProUGUI Label(Transform parent, string text, float size = 24f, Color? color = null,
        FontStyles style = FontStyles.Normal, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        RectTransform rect = Rect("Label", parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.fontSharedMaterial = WhiteFaceMaterial(label.fontSharedMaterial);
        label.text = text;
        label.fontSize = size;
        label.color = color ?? Ink;
        label.fontStyle = style;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static Material whiteFaceMaterial;

    //the project's default TMP font material has its Face Color set to black, and Face Color multiplies the
    //text color, so white text renders black on it. One shared copy with a white face lets label.color work as set.
    private static Material WhiteFaceMaterial(Material defaultMaterial)
    {
        if(defaultMaterial == null) return null;
        if(whiteFaceMaterial == null)
        {
            whiteFaceMaterial = new Material(defaultMaterial) { name = defaultMaterial.name + " (Seg UI)" };
            whiteFaceMaterial.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        }
        return whiteFaceMaterial;
    }

    public static Button Button(Transform parent, string text, Action onClick, float width = -1f, Color? color = null, bool interactable = true)
    {
        RectTransform rect = Rect("Button", parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color ?? ButtonColor;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.interactable = interactable;
        if(onClick != null) button.onClick.AddListener(() => onClick());

        LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 40f;
        layout.preferredHeight = 40f;

        TextMeshProUGUI label = Label(rect, text, 22f, interactable ? Ink : FadedInk, FontStyles.Normal, TextAlignmentOptions.Center);
        Stretch(label.rectTransform, 4f);
        //sizes the button to its text when no width is given
        float preferredWidth = width > 0f ? width : label.GetPreferredValues(text).x + 28f;
        layout.minWidth = preferredWidth;
        layout.preferredWidth = preferredWidth;
        return button;
    }

    /// <summary>Fixes a layout child's width.</summary>
    public static T Width<T>(T component, float width) where T : Component
    {
        //not ??: Unity's fake-null from GetComponent would slip through it in the editor
        if(!component.TryGetComponent(out LayoutElement layout)) layout = component.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        return component;
    }

    /// <summary>A vertical scroll area filling <paramref name="parent"/>; returns the content to add children to.</summary>
    public static RectTransform ScrollArea(RectTransform parent, out ScrollRect scroll)
    {
        scroll = parent.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        RectTransform viewport = Rect("Viewport", parent);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        //a clear image so the mouse wheel/drag hits the viewport
        viewport.gameObject.AddComponent<Image>().color = Color.clear;

        RectTransform content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        Column(content, 8f, 28);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
        return content;
    }

    public static void Clear(Transform parent)
    {
        for(int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            //inactive children drop out of the layout right away; Destroy waits until end of frame
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
    }

    public static string Money(float amount)
    {
        return "$" + amount.ToString("0.00");
    }

    public static string Check(bool isChecked)
    {
        return isChecked ? "[X]" : "[  ]";
    }
}
