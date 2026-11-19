using UnityEditor;
using UnityEngine;

/// <summary>Adds every segregation system to the open scene in one click.</summary>
public static class SegregationSetup
{
    [MenuItem("Lil Windows/Add Segregation Systems To Scene")]
    private static void AddToScene()
    {
        var existing = Object.FindFirstObjectByType<CommissarySystem>();
        if(existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("[SegregationSetup] This scene already has the segregation systems (selected).");
            return;
        }

        var go = new GameObject("Segregation Systems");
        Undo.RegisterCreatedObjectUndo(go, "Add Segregation Systems");
        //state first so the systems find it in their Awake
        Undo.AddComponent<RileySegState>(go);
        Undo.AddComponent<CommissarySystem>(go);
        Undo.AddComponent<LibrarySystem>(go);
        Undo.AddComponent<CommissaryFormUI>(go);
        Undo.AddComponent<LibraryPromptUI>(go);
        Undo.AddComponent<PropertyBoxUI>(go);
        Undo.AddComponent<SegNoticeUI>(go);
        Selection.activeGameObject = go;
    }
}
