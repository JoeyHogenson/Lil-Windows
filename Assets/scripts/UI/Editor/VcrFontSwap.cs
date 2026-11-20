using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Makes VCR OSD Mono the game's only font: builds a dynamic TMP SDF font asset from the TTF, sets it as TMP's
/// default (runtime-built UI), and swaps every TextMeshPro, legacy UI Text and TextMesh in the game's prefabs,
/// the open scenes and the menu scene. Each old TMP material is rebuilt on the VCR atlas with its styling kept
/// (face/outline/underlay colors), so text looks the same apart from the typeface. Safe to run again.
/// </summary>
public static class VcrFontSwap
{
    private const string TtfPath = "Assets/textures/VCR_OSD_MONO_1.001.ttf";
    private const string FontFolder = "Assets/UI/Fonts";
    private const string FontAssetPath = FontFolder + "/VCR OSD Mono SDF.asset";
    private const string FallbackFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private static readonly string[] ExtraScenes = { "Assets/menu.unity" };
    private static readonly string[] SkipFolders =
        { "Assets/Plugins/", "Assets/TextMesh Pro/", "Assets/Samples/", "Assets/Draw On Screen/", "Assets/FreeDraw/", "Assets/Scenes/Archive/" };
    //properties that belong to the atlas, not the style; everything else is copied from the old material
    private static readonly string[] AtlasProperties = { "_MainTex", "_TextureWidth", "_TextureHeight", "_GradientScale" };

    public const string RequestPath = "Library/LilWindows.vcr-font.request";
    public const string ReportPath = "Library/LilWindows.vcr-font-report.txt";

    private static TMP_FontAsset vcr;
    private static Font ttf;
    private static Dictionary<Material, Material> materialMap;
    private static StringBuilder report;

    [MenuItem("Lil Windows/Swap All Fonts To VCR OSD Mono")]
    private static void RunFromMenu() => Run();

    [InitializeOnLoadMethod]
    private static void WatchForRequest()
    {
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
    }

    private static double nextPoll;

    private static void Poll()
    {
        if(EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 2.0;
        if(!File.Exists(RequestPath)) return;
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(RequestPath);
        try { Run(); }
        catch(Exception e) { File.AppendAllText(ReportPath, "\nFAILED:\n" + e); Debug.LogException(e); }
    }

    public static void Run()
    {
        report = new StringBuilder("VCR OSD Mono font swap  " + DateTime.Now + "\n");
        materialMap = new Dictionary<Material, Material>();

        ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if(ttf == null)
        {
            report.AppendLine("Font not found: " + TtfPath);
            Finish();
            return;
        }
        vcr = GetOrCreateFontAsset();
        SetTmpDefault();

        report.AppendLine("\nPrefabs:");
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if(SkipFolders.Any(path.StartsWith)) continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = Swap(root);
                if(changed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.AppendLine("  " + path + ": " + changed + " text(s)");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        report.AppendLine("\nScenes:");
        var openPaths = new HashSet<string>();
        for(int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if(!scene.isLoaded) continue;
            openPaths.Add(scene.path);
            int changed = scene.GetRootGameObjects().Sum(Swap);
            if(changed > 0) EditorSceneManager.MarkSceneDirty(scene);
            report.AppendLine("  " + scene.path + ": " + changed + " text(s)" + (changed > 0 ? " (open scene marked dirty; save to keep)" : ""));
        }
        foreach(string path in ExtraScenes.Where(p => !openPaths.Contains(p) && File.Exists(p)))
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            int changed = scene.GetRootGameObjects().Sum(Swap);
            if(changed > 0) EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
            report.AppendLine("  " + path + ": " + changed + " text(s)" + (changed > 0 ? " (saved)" : ""));
        }

        AssetDatabase.SaveAssets();
        Finish();
    }

    private static void Finish()
    {
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log("[VcrFontSwap]\n" + report);
    }

    private static TMP_FontAsset GetOrCreateFontAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if(existing != null)
        {
            report.AppendLine("Using existing " + FontAssetPath);
            return existing;
        }

        Directory.CreateDirectory(FontFolder);
        //dynamic: glyphs are rendered into the atlas on first use, so any text in the game works
        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(ttf, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        asset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
        AssetDatabase.CreateAsset(asset, FontAssetPath);
        asset.material.name = asset.name + " Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        asset.atlasTextures[0].name = asset.name + " Atlas";
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);

        //builds need the source font to render new glyphs at runtime
        var so = new SerializedObject(asset);
        SerializedProperty source = so.FindProperty("m_SourceFontFile");
        if(source != null) source.objectReferenceValue = ttf;
        so.ApplyModifiedPropertiesWithoutUndo();

        //VCR has no glyphs for some punctuation (curly quotes, em dashes); fall back instead of showing boxes
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
        if(fallback != null) asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };

        //the default material keeps the old default font's look (its face color etc.), since every scene text used it
        if(fallback != null) CopyStyle(fallback.material, asset.material);

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        report.AppendLine("Created " + FontAssetPath + " (dynamic SDF, LiberationSans fallback, style copied from LiberationSans SDF Material)");
        return asset;
    }

    private static void SetTmpDefault()
    {
        var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
        if(settings == null) { report.AppendLine("TMP Settings not found"); return; }
        var so = new SerializedObject(settings);
        so.FindProperty("m_defaultFontAsset").objectReferenceValue = vcr;
        so.ApplyModifiedPropertiesWithoutUndo();
        report.AppendLine("TMP Settings default font -> " + vcr.name);
    }

    private static int Swap(GameObject root)
    {
        int changed = 0;
        foreach(TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            Material oldMaterial = text.fontSharedMaterial;
            if(text.font == vcr && oldMaterial != null && oldMaterial.GetTexture("_MainTex") == vcr.atlasTexture) continue;
            Material newMaterial = MapMaterial(oldMaterial, text.font);
            Undo.RecordObject(text, "Swap font to VCR");
            text.font = vcr;
            text.fontSharedMaterial = newMaterial;
            EditorUtility.SetDirty(text);
            changed++;
        }
        foreach(UnityEngine.UI.Text text in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            if(text.font == ttf) continue;
            Undo.RecordObject(text, "Swap font to VCR");
            text.font = ttf;
            EditorUtility.SetDirty(text);
            changed++;
        }
        foreach(TextMesh mesh in root.GetComponentsInChildren<TextMesh>(true))
        {
            if(mesh.font == ttf) continue;
            Undo.RecordObject(mesh, "Swap font to VCR");
            mesh.font = ttf;
            var renderer = mesh.GetComponent<MeshRenderer>();
            if(renderer != null) renderer.sharedMaterial = ttf.material;
            EditorUtility.SetDirty(mesh);
            changed++;
        }
        return changed;
    }

    //the old font's default material maps to VCR's default (which copied LiberationSans's style); any other
    //material (a preset with an outline, a different face color...) gets its own VCR copy with that style
    private static Material MapMaterial(Material old, TMP_FontAsset oldFont)
    {
        if(old == null) return vcr.material;
        if(old == vcr.material || old.GetTexture("_MainTex") == vcr.atlasTexture) return old;
        if(materialMap.TryGetValue(old, out Material mapped)) return mapped;

        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
        if(fallback != null && old == fallback.material)
        {
            materialMap[old] = vcr.material;
            return vcr.material;
        }

        string path = FontFolder + "/" + vcr.name + " - " + old.name.Replace("/", "_") + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if(existing != null)
        {
            materialMap[old] = existing;
            return existing;
        }
        var preset = new Material(old) { name = Path.GetFileNameWithoutExtension(path) };
        foreach(string property in AtlasProperties)
        {
            if(!preset.HasProperty(property) || !vcr.material.HasProperty(property)) continue;
            if(property == "_MainTex") preset.SetTexture(property, vcr.material.GetTexture(property));
            else preset.SetFloat(property, vcr.material.GetFloat(property));
        }
        AssetDatabase.CreateAsset(preset, path);
        materialMap[old] = preset;
        report.AppendLine("  new material preset " + path + " (style from " + old.name + ")");
        return preset;
    }

    //style only: face/outline/underlay/glow/keywords; leaves the atlas and its dimensions alone
    private static void CopyStyle(Material from, Material to)
    {
        var atlas = AtlasProperties.Where(to.HasProperty).ToDictionary(p => p,
            p => p == "_MainTex" ? (object)to.GetTexture(p) : to.GetFloat(p));
        Shader shader = to.shader;
        to.CopyPropertiesFromMaterial(from);
        to.shader = shader;
        to.shaderKeywords = from.shaderKeywords;
        foreach(var pair in atlas)
        {
            if(pair.Value is Texture texture) to.SetTexture(pair.Key, texture);
            else to.SetFloat(pair.Key, (float)pair.Value);
        }
        EditorUtility.SetDirty(to);
    }
}
