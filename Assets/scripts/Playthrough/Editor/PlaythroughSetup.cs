using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One click to turn the open scene into the shared all-playthroughs scene: adds the PlaythroughDirector,
/// gives DC and Riley their own spawn/count zone, adds Riley's segregation systems (Riley-only), and tags the
/// character-specific NPCs. Safe to run again; it only fills in what's missing.
/// </summary>
public static class PlaythroughSetup
{
    private const string SpawnRootName = "Playthrough Spawns";
    private const string DCSpawnName = "DC Spawn";
    private const string DCCountZoneName = "DC Count Zone";
    private const string SegSystemsName = "Segregation Systems";

    [MenuItem("Lil Windows/Set Up Unified Playthrough Scene")]
    private static void SetUp()
    {
        var game = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
        if(game == null)
        {
            EditorUtility.DisplayDialog("Unified playthrough", "This scene has no GameController.", "OK");
            return;
        }

        var report = new System.Text.StringBuilder();

        var director = game.GetComponent<PlaythroughDirector>();
        if(director == null)
        {
            director = Undo.AddComponent<PlaythroughDirector>(game.gameObject);
            report.AppendLine("Added PlaythroughDirector to " + game.name + ".");
        }

        var gameSO = new SerializedObject(game);
        var sharedStart = (Transform)gameSO.FindProperty("playerStartingPosition").objectReferenceValue;
        var sharedZone = (Collider)gameSO.FindProperty("countzone").objectReferenceValue;

        //the scene's current start is the segregation cell (next to the Solitary RFI/manual), i.e. Riley's
        Transform dcNpc = FindSceneTransform("DC");
        Transform dcSpawn = FindOrCreateDCSpawn(dcNpc, sharedStart, report);
        Collider dcZone = FindOrCreateDCCountZone(dcSpawn, sharedZone, report);

        var directorSO = new SerializedObject(director);
        SerializedProperty options = directorSO.FindProperty("options");
        for(int i = 0; i < options.arraySize; i++)
        {
            SerializedProperty option = options.GetArrayElementAtIndex(i);
            var character = (PlayableCharacter)option.FindPropertyRelative("character").enumValueIndex;
            SerializedProperty spawn = option.FindPropertyRelative("spawnPoint");
            SerializedProperty zone = option.FindPropertyRelative("countZone");
            if(character == PlayableCharacter.Riley)
            {
                if(spawn.objectReferenceValue == null) spawn.objectReferenceValue = sharedStart;
                if(zone.objectReferenceValue == null) zone.objectReferenceValue = sharedZone;
            }
            else if(character == PlayableCharacter.DC)
            {
                if(spawn.objectReferenceValue == null) spawn.objectReferenceValue = dcSpawn;
                if(zone.objectReferenceValue == null) zone.objectReferenceValue = dcZone;
            }
        }
        directorSO.ApplyModifiedProperties();

        AddSegregationSystems(report);

        //you don't meet yourself: hide the DC NPC when playing DC; Chico is DC's cellmate
        if(dcNpc != null) Tag(dcNpc.gameObject, PlayableCharacterMask.Riley, report);
        Transform chico = FindSceneTransform("Chico");
        if(chico != null) Tag(chico.gameObject, PlayableCharacterMask.DC, report);

        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        Selection.activeGameObject = director.gameObject;

        report.AppendLine();
        report.AppendLine("Check: move \"" + DCSpawnName + "\" and size \"" + DCCountZoneName + "\" to DC's actual cell door, then save the scene.");
        report.AppendLine("Tag any other character-specific objects with a PlaythroughOnly component.");
        Debug.Log("[PlaythroughSetup]\n" + report);
        EditorUtility.DisplayDialog("Unified playthrough", report.ToString(), "OK");
    }

    private static Transform FindOrCreateDCSpawn(Transform dcNpc, Transform fallback, System.Text.StringBuilder report)
    {
        Transform existing = FindSceneTransform(DCSpawnName);
        if(existing != null) return existing;

        Transform root = FindSceneTransform(SpawnRootName);
        if(root == null)
        {
            root = new GameObject(SpawnRootName).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create playthrough spawns");
        }

        var spawn = new GameObject(DCSpawnName).transform;
        Undo.RegisterCreatedObjectUndo(spawn.gameObject, "Create DC spawn");
        spawn.SetParent(root, false);
        Transform source = dcNpc != null ? dcNpc : fallback;
        if(source != null) spawn.SetPositionAndRotation(source.position, source.rotation);
        report.AppendLine("Created " + DCSpawnName + " at " + (dcNpc != null ? "the DC NPC's spot" : "the shared start (no DC NPC found)") + ".");
        return spawn;
    }

    private static Collider FindOrCreateDCCountZone(Transform dcSpawn, Collider template, System.Text.StringBuilder report)
    {
        Transform existing = FindSceneTransform(DCCountZoneName);
        if(existing != null && existing.TryGetComponent(out Collider found)) return found;

        var zone = new GameObject(DCCountZoneName);
        Undo.RegisterCreatedObjectUndo(zone, "Create DC count zone");
        zone.transform.SetParent(dcSpawn.parent, false);
        zone.transform.position = dcSpawn.position;
        var box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        //same world size as the existing zone so it fits a cell-door footprint at this scene's scale
        Vector3 size = template != null ? template.bounds.size : new Vector3(4f, 4f, 4f);
        box.size = size;
        report.AppendLine("Created " + DCCountZoneName + " (" + size + ") around DC's spawn.");
        return box;
    }

    private static void AddSegregationSystems(System.Text.StringBuilder report)
    {
        var existing = Object.FindFirstObjectByType<CommissarySystem>(FindObjectsInactive.Include);
        GameObject go;
        if(existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(SegSystemsName);
            Undo.RegisterCreatedObjectUndo(go, "Add Segregation Systems");
            //state first so the systems find it in their Awake
            Undo.AddComponent<RileySegState>(go);
            Undo.AddComponent<CommissarySystem>(go);
            Undo.AddComponent<LibrarySystem>(go);
            Undo.AddComponent<CommissaryFormUI>(go);
            Undo.AddComponent<LibraryPromptUI>(go);
            Undo.AddComponent<PropertyBoxUI>(go);
            Undo.AddComponent<SegNoticeUI>(go);
            report.AppendLine("Added " + SegSystemsName + ".");
        }
        Tag(go, PlayableCharacterMask.Riley, report);
        //starts off; the director turns it on only for Riley, so DC never gets seg notices or the RFI
        Undo.RecordObject(go, "Deactivate segregation systems");
        go.SetActive(false);
    }

    private static void Tag(GameObject go, PlayableCharacterMask characters, System.Text.StringBuilder report)
    {
        var only = go.GetComponent<PlaythroughOnly>();
        if(only == null)
        {
            only = Undo.AddComponent<PlaythroughOnly>(go);
            only.characters = characters;
            EditorUtility.SetDirty(only);
            report.AppendLine("Tagged " + go.name + " as " + characters + "-only.");
        }
    }

    private static Transform FindSceneTransform(string name)
    {
        foreach(Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if(t.name == name && t.gameObject.scene.IsValid()) return t;
        }
        return null;
    }
}
