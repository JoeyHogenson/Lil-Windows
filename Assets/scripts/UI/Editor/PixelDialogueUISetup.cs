using System.IO;
using System.Linq;
using System.Text;
using PixelCrushers.DialogueSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Reskins the Dialogue System's Basic Standard Dialogue UI with the 9-sliced pixel boxes in Assets/UI/Pixel Boxes.
/// Saves the result as a Prefab Variant (Assets/UI/Lil Windows Dialogue UI.prefab), so the Pixel Crushers prefab
/// itself is untouched and plugin updates still flow through, and points every Dialogue Manager in the open
/// scenes at it. Images keep their colors, which tint the grey boxes. Safe to run again.
/// </summary>
public static class PixelDialogueUISetup
{
    private const string BoxFolder = "Assets/UI/Pixel Boxes";
    private const string BasePrefabPath = "Assets/Plugins/Pixel Crushers/Dialogue System/Prefabs/Standard UI Prefabs/Templates/Basic/Basic Standard Dialogue UI.prefab";
    private const string VariantPath = "Assets/UI/Lil Windows Dialogue UI.prefab";
    //v2: the subtitle cap; a new name so an older compiled copy of this tool can't consume the request
    public const string RequestPath = "Library/LilWindows.pixel-ui-v2.request";
    public const string ReportPath = "Library/LilWindows.pixel-ui-report.txt";

    //each box texture is 8x8: 1px outline + 1px bevel + 1px of fill in the slice border
    private const int SliceBorder = 3;
    //screen pixels (at the canvas's reference resolution) per texel, so the boxes read as chunky pixel art
    private const float PixelScale = 3f;

    private enum Box { Panel, Button, Inset, Knob, Line }

    [MenuItem("Lil Windows/Reskin Dialogue UI With Pixel Boxes")]
    private static void RunFromMenu()
    {
        Run();
    }

    [InitializeOnLoadMethod]
    private static void WatchForRequest()
    {
        EditorApplication.update -= PollRequest;
        EditorApplication.update += PollRequest;
    }

    private static double nextPoll;

    private static void PollRequest()
    {
        if(EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 2.0;
        if(!File.Exists(RequestPath)) return;
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(RequestPath);
        try
        {
            Run();
        }
        catch(System.Exception e)
        {
            File.AppendAllText(ReportPath, "\n\nFAILED WITH EXCEPTION:\n" + e);
            Debug.LogException(e);
        }
    }

    public static void Run()
    {
        var report = new StringBuilder("Pixel dialogue UI setup  " + System.DateTime.Now + "\n");

        Sprite panel = ConfigureSprite("PixelPanel", report);
        Sprite button = ConfigureSprite("PixelButton", report);
        Sprite inset = ConfigureSprite("PixelInset", report);
        Sprite knob = ConfigureSprite("PixelKnob", report);
        if(panel == null || button == null || inset == null || knob == null)
        {
            report.AppendLine("Missing box sprites; stopping.");
            Finish(report);
            return;
        }

        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
        if(basePrefab == null)
        {
            report.AppendLine("Base prefab not found: " + BasePrefabPath);
            Finish(report);
            return;
        }

        //start from the existing variant if there is one, so re-runs keep any hand edits made to it
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(existing != null ? existing : basePrefab);
        try
        {
            report.AppendLine("Images:");
            foreach(Image image in instance.GetComponentsInChildren<Image>(true))
            {
                Box? box = Classify(image, out string why);
                string path = AnimationUtility.CalculateTransformPath(image.transform, instance.transform);
                if(box == null)
                {
                    report.AppendLine("  skip  " + path + "  (" + why + ")");
                    continue;
                }
                if(box == Box.Line)
                {
                    //a solid, hard-edged line in the image's own color
                    image.sprite = null;
                    image.type = Image.Type.Simple;
                }
                else
                {
                    image.sprite = box == Box.Panel ? panel : box == Box.Button ? button : box == Box.Inset ? inset : knob;
                    image.type = Image.Type.Sliced;
                    image.fillCenter = true;
                    image.pixelsPerUnitMultiplier = 1f / PixelScale;
                }
                report.AppendLine("  " + box.Value.ToString().ToLowerInvariant().PadRight(7) + path + "  (" + why + ")");
            }

            //the NPC subtitle grows down from the top; keep it above the response menu (VCR's wider glyphs wrap
            //onto more lines than the old font, and long lines ran over the options)
            Transform npcPanel = instance.transform.Find("Dialogue Panel/NPC Subtitle Panel");
            Transform menuPanel = instance.transform.Find("Dialogue Panel/Response Menu Panel");
            if(npcPanel != null && menuPanel != null)
            {
                var fit = npcPanel.GetComponent<SubtitleFitAbove>();
                if(fit == null) fit = npcPanel.gameObject.AddComponent<SubtitleFitAbove>();
                fit.limit = (RectTransform)menuPanel;
                Transform subtitle = npcPanel.Find("Subtitle Text");
                fit.text = subtitle != null ? subtitle.GetComponent<Graphic>() : null;
                var fitter = npcPanel.GetComponent<ContentSizeFitter>();
                if(fitter != null) fitter.enabled = false;
                report.AppendLine("NPC Subtitle Panel: capped above Response Menu Panel (SubtitleFitAbove; text "
                    + (fit.text != null ? fit.text.GetType().Name : "NOT FOUND") + "), its ContentSizeFitter disabled");
            }
            else
            {
                report.AppendLine("NPC Subtitle Panel / Response Menu Panel not found; subtitle cap not added");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(VariantPath));
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, VariantPath, out bool success);
            report.AppendLine(success ? "Saved " + VariantPath + (existing == null ? " (new variant of Basic Standard Dialogue UI)" : "") : "FAILED to save " + VariantPath);
            if(success) AssignToDialogueManagers(saved, report);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
        Finish(report);
    }

    private static void Finish(StringBuilder report)
    {
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log("[PixelDialogueUISetup]\n" + report);
    }

    private static Sprite ConfigureSprite(string name, StringBuilder report)
    {
        string path = BoxFolder + "/" + name + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer == null)
        {
            report.AppendLine("Missing " + path);
            return null;
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = new Vector4(SliceBorder, SliceBorder, SliceBorder, SliceBorder);
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        //sliced images need the full quad, not a tight mesh
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    //decides from what the image is used for, falling back to which built-in sprite it had
    private static Box? Classify(Image image, out string why)
    {
        string sprite = image.sprite != null ? image.sprite.name : null;
        string spritePath = image.sprite != null ? AssetDatabase.GetAssetPath(image.sprite) : "";
        bool ours = spritePath.StartsWith(BoxFolder);

        //thin separators: a 9-sliced box squashed into a line just smears, so they become solid lines
        if(image.name.IndexOf("Divider", System.StringComparison.OrdinalIgnoreCase) >= 0) { why = "divider line"; return Box.Line; }

        if(image.sprite == null)
        {
            why = "no sprite: solid color block";
            return null;
        }
        if(!ours && !spritePath.StartsWith("Resources/unity_builtin_extra") && spritePath.Length > 0 && !spritePath.Contains("unity_builtin"))
        {
            why = "art sprite " + sprite;
            return null;
        }

        Scrollbar scrollbar = image.GetComponentInParent<Scrollbar>(true);
        if(scrollbar != null && scrollbar.handleRect == image.rectTransform) { why = "scrollbar handle"; return Box.Knob; }
        if(scrollbar != null && scrollbar.gameObject == image.gameObject) { why = "scrollbar track"; return Box.Inset; }

        Slider slider = image.GetComponentInParent<Slider>(true);
        if(slider != null && slider.handleRect == image.rectTransform) { why = "slider knob"; return Box.Knob; }
        if(slider != null && slider.fillRect != image.rectTransform) { why = "slider track"; return Box.Inset; }

        if(image.GetComponentInParent<InputField>(true) != null && image.GetComponent<InputField>() != null) { why = "input field"; return Box.Inset; }
        if(image.GetComponent<TMPro.TMP_InputField>() != null) { why = "input field"; return Box.Inset; }

        if(image.GetComponent<Selectable>() != null) { why = "button/selectable"; return Box.Button; }

        switch(sprite)
        {
            case "Knob": why = "built-in Knob"; return Box.Knob;
            case "InputFieldBackground": why = "built-in InputFieldBackground"; return Box.Inset;
            case "PixelButton": why = "already pixel button"; return Box.Button;
            case "PixelInset": why = "already pixel inset"; return Box.Inset;
            case "PixelKnob": why = "already pixel knob"; return Box.Knob;
            default: why = "panel (" + sprite + ")"; return Box.Panel;
        }
    }

    private static void AssignToDialogueManagers(GameObject dialogueUI, StringBuilder report)
    {
        for(int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if(!scene.isLoaded) continue;
            foreach(DialogueSystemController controller in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DialogueSystemController>(true)))
            {
                var so = new SerializedObject(controller);
                SerializedProperty property = so.FindProperty("displaySettings.dialogueUI");
                if(property == null)
                {
                    report.AppendLine("  " + scene.name + "/" + controller.name + ": no displaySettings.dialogueUI field");
                    continue;
                }
                if(property.objectReferenceValue == dialogueUI) continue;
                Object previous = property.objectReferenceValue;
                property.objectReferenceValue = dialogueUI;
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(scene);
                report.AppendLine("  " + scene.name + "/" + controller.name + ": dialogue UI " + (previous != null ? previous.name : "none")
                    + " -> " + dialogueUI.name + "  (scene marked dirty; save to keep)");
            }
        }
    }
}
