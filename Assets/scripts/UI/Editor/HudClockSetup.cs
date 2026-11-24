using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Puts the top-right HUD clock (<see cref="GameHUD"/>) on a canvas as real GameObjects, so it can be moved,
/// restyled and edited in the scene instead of being built in code at runtime. Uses the selected object's canvas,
/// else the open scene's first screen-space canvas, else makes one. Safe to run again: an existing HUD is just selected.
/// </summary>
public static class HudClockSetup
{
    //the default TMP material's Face Color is black and multiplies the text color; this copy has a white face
    private const string MaterialPath = "Assets/UI/Fonts/HUD Text (White Face).mat";

    [MenuItem("Lil Windows/Add HUD Clock To Canvas")]
    private static void AddFromMenu()
    {
        Add(Selection.activeGameObject);
    }

    [MenuItem("GameObject/UI/Lil Windows HUD Clock", false, 2100)]
    private static void AddFromGameObjectMenu(MenuCommand command)
    {
        Add(command.context as GameObject ?? Selection.activeGameObject);
    }

    private static void Add(GameObject selected)
    {
        GameHUD existing = Object.FindFirstObjectByType<GameHUD>(FindObjectsInactive.Include);
        if(existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            Debug.Log("[HudClockSetup] The scene already has a Game HUD (\"" + existing.name + "\"); selected it.", existing);
            return;
        }

        Canvas canvas = FindCanvas(selected) ?? CreateCanvas();
        int layer = canvas.gameObject.layer;

        RectTransform panel = NewRect("HUD Clock", canvas.transform, layer);
        Undo.RegisterCreatedObjectUndo(panel.gameObject, "Add HUD Clock");
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-24f, -20f);
        panel.sizeDelta = new Vector2(520f, 0f);
        //translucent backing keeps it readable over any background
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
        SegUIKit.Column(panel, 4f, 12).childAlignment = TextAnchor.UpperRight;
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Material material = WhiteFaceMaterial();
        TextMeshProUGUI clock = NewLabel("Clock", panel, layer, material, "Sun 6:40 AM  -  Day 1", 30f, Color.white, FontStyles.Bold);
        TextMeshProUGUI next = NewLabel("Next Activity", panel, layer, material, "Next: Count at 12:00 PM", 20f,
            new Color(0.85f, 0.85f, 0.8f), FontStyles.Normal);
        RectTransform urgent = NewRect("Urgent", panel, layer);
        SegUIKit.Column(urgent, 2f, 0).childAlignment = TextAnchor.UpperRight;

        GameHUD hud = panel.gameObject.AddComponent<GameHUD>();
        var so = new SerializedObject(hud);
        so.FindProperty("game").objectReferenceValue = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
        so.FindProperty("clockLabel").objectReferenceValue = clock;
        so.FindProperty("nextLabel").objectReferenceValue = next;
        so.FindProperty("urgentColumn").objectReferenceValue = urgent;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;
        EditorGUIUtility.PingObject(panel.gameObject);
        Debug.Log("[HudClockSetup] Added \"HUD Clock\" to canvas \"" + canvas.name + "\". The texts are placeholders; "
            + "GameHUD fills them in at runtime. Save the scene to keep it.", panel);
    }

    private static Canvas FindCanvas(GameObject selected)
    {
        if(selected != null)
        {
            Canvas parent = selected.GetComponentInParent<Canvas>(true);
            if(parent != null) return parent.rootCanvas;
        }
        return Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID)
            .Where(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace && c.gameObject.scene.IsValid())
            .OrderBy(c => c.name == "Canvas" ? 0 : 1)
            .FirstOrDefault();
    }

    private static Canvas CreateCanvas()
    {
        var go = new GameObject("HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, "Add HUD Canvas");
        go.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : 5;
        Canvas canvas = go.GetComponent<Canvas>();
        //RetroCanvasRouter moves it onto its UI camera at runtime
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static RectTransform NewRect(string name, Transform parent, int layer)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = layer };
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static TextMeshProUGUI NewLabel(string name, Transform parent, int layer, Material material, string text,
        float size, Color color, FontStyles style)
    {
        RectTransform rect = NewRect(name, parent, layer);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if(material != null) label.fontSharedMaterial = material;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Right;
        label.raycastTarget = false;
        return label;
    }

    private static Material WhiteFaceMaterial()
    {
        var saved = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(saved != null) return saved;

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if(font == null || font.material == null)
        {
            Debug.LogWarning("[HudClockSetup] No default TMP font; the labels keep TMP's default material.");
            return null;
        }
        var material = new Material(font.material) { name = "HUD Text (White Face)" };
        material.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }
}
