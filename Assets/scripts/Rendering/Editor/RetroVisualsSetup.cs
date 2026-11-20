using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Adds the Retro Stylize renderer feature to every URP renderer in the project (all quality levels), creates
/// three look presets as Volume Profiles, and puts a global Volume using one of them in the open scene.
/// Swap the Volume's profile (or tweak its Retro Stylize override) to compare looks. Safe to run again.
/// </summary>
public static class RetroVisualsSetup
{
    private const string ProfileFolder = "Assets/Settings/Retro Visuals";
    private const string VolumeObjectName = "Retro Visuals Volume";

    private enum Preset { PsxAndCel, Psx, Cel }

    [MenuItem("Lil Windows/Set Up Retro Visuals")]
    private static void SetUp()
    {
        int added = AddFeatureToAllRenderers();

        VolumeProfile psxAndCel = GetOrCreateProfile("Retro - PSX + Cel", Preset.PsxAndCel);
        GetOrCreateProfile("Retro - PSX Only", Preset.Psx);
        GetOrCreateProfile("Retro - Cel Only", Preset.Cel);
        AssetDatabase.SaveAssets();

        Volume volume = GetOrCreateSceneVolume(psxAndCel);
        Selection.activeObject = volume.gameObject;

        string message = "Retro Stylize feature added to " + added + " renderer(s).\n\n"
            + "\"" + VolumeObjectName + "\" in this scene uses \"" + volume.sharedProfile.name + "\". Swap its profile for the PSX-only "
            + "or Cel-only presets in " + ProfileFolder + ", or edit the Retro Stylize override directly.\n\nSave the scene to keep the volume.";
        Debug.Log("[RetroVisualsSetup] " + message);
        EditorUtility.DisplayDialog("Retro visuals", message, "OK");
    }

    private static int AddFeatureToAllRenderers()
    {
        Shader shader = Shader.Find(RetroStylizeFeature.ShaderName);
        if(shader == null) Debug.LogError("[RetroVisualsSetup] Shader \"" + RetroStylizeFeature.ShaderName + "\" not found; has it imported?");

        int added = 0;
        foreach(string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if(data == null || data.rendererFeatures.Exists(f => f is RetroStylizeFeature)) continue;

            var feature = ScriptableObject.CreateInstance<RetroStylizeFeature>();
            feature.name = "RetroStylize";
            Undo.RegisterCreatedObjectUndo(feature, "Add Retro Stylize");
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            var featureSO = new SerializedObject(feature);
            featureSO.FindProperty("shader").objectReferenceValue = shader;
            featureSO.ApplyModifiedPropertiesWithoutUndo();

            //the renderer keeps a parallel list of local file ids; both must grow together
            var dataSO = new SerializedObject(data);
            SerializedProperty features = dataSO.FindProperty("m_RendererFeatures");
            SerializedProperty map = dataSO.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            dataSO.ApplyModifiedProperties();

            data.SetDirty();
            EditorUtility.SetDirty(data);
            added++;
        }
        AssetDatabase.SaveAssets();
        return added;
    }

    private static VolumeProfile GetOrCreateProfile(string name, Preset preset)
    {
        string path = ProfileFolder + "/" + name + ".asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if(profile != null) return profile;

        Directory.CreateDirectory(ProfileFolder);
        AssetDatabase.Refresh();
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        RetroStylize retro = profile.Add<RetroStylize>(true);
        retro.name = nameof(RetroStylize);
        ApplyPreset(retro, preset);
        AssetDatabase.AddObjectToAsset(retro, profile);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void ApplyPreset(RetroStylize retro, Preset preset)
    {
        bool psx = preset != Preset.Cel;
        bool cel = preset != Preset.Psx;

        retro.celStrength.Override(cel ? 0.65f : 0f);
        retro.celBands.Override(4);
        retro.celSoftness.Override(0.12f);

        retro.outlineStrength.Override(cel ? 0.85f : 0f);
        retro.outlineThickness.Override(1f);
        retro.depthSensitivity.Override(0.08f);
        retro.normalSensitivity.Override(0.5f);

        //PS1: 240 lines, 15-bit color with ordered dither
        retro.psxVerticalResolution.Override(psx ? 240 : 0);
        retro.psxColorBits.Override(psx ? 5 : 8);
        retro.psxDither.Override(1f);
    }

    private static Volume GetOrCreateSceneVolume(VolumeProfile profile)
    {
        foreach(Volume existing in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if(existing.name == VolumeObjectName) return existing;
        }

        var go = new GameObject(VolumeObjectName);
        Undo.RegisterCreatedObjectUndo(go, "Add Retro Visuals Volume");
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
        EditorSceneManager.MarkSceneDirty(go.scene);
        return volume;
    }
}
