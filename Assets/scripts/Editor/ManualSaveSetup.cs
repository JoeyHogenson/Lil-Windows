using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click switch from Easy Save auto-saving (which fires every time Play mode stops) to saving only from
/// the pause menu: turns off the Easy Save 3 Manager's Save Event, points "Save and quit" at
/// GameController.SaveAndQuit, and adds a "Save button" above it that calls GameController.SaveGame.
/// Safe to run again; it only adds the Save button once.
/// </summary>
public static class ManualSaveSetup
{
    private const string SaveAndQuitButtonName = "Save and quit button";
    private const string SaveButtonName = "Save button";
    private const float SaveButtonSpacing = 40f;

    [MenuItem("Lil Windows/Set Up Manual Saving In This Scene")]
    private static void SetUp()
    {
        var saveManager = Object.FindFirstObjectByType<ES3AutoSaveMgr>(FindObjectsInactive.Include);
        var game = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
        Button saveAndQuit = FindButton(SaveAndQuitButtonName);
        if(saveManager == null || game == null || saveAndQuit == null)
        {
            EditorUtility.DisplayDialog("Manual saving", "This scene needs an Easy Save 3 Manager, a GameController, and a button named \""
                + SaveAndQuitButtonName + "\".\n\nFound: manager " + (saveManager != null) + ", GameController " + (game != null)
                + ", button " + (saveAndQuit != null), "OK");
            return;
        }

        Undo.RecordObject(saveManager, "Turn off auto-save");
        saveManager.saveEvent = ES3AutoSaveMgr.SaveEvent.None;
        EditorUtility.SetDirty(saveManager);

        Rewire(saveAndQuit, game.SaveAndQuit);

        Button save = FindButton(SaveButtonName);
        if(save == null) save = CreateSaveButton(saveAndQuit);
        Rewire(save, game.SaveGame);

        EditorSceneManager.MarkSceneDirty(saveManager.gameObject.scene);
        Selection.activeGameObject = save.gameObject;
        Debug.Log("[ManualSaveSetup] Auto-save off; \"" + SaveButtonName + "\" calls SaveGame and \"" + SaveAndQuitButtonName
            + "\" calls SaveAndQuit. Save the scene to keep it.");
    }

    private static Button CreateSaveButton(Button template)
    {
        Transform parent = template.transform.parent;
        GameObject copy = Object.Instantiate(template.gameObject, parent);
        Undo.RegisterCreatedObjectUndo(copy, "Add Save button");
        copy.name = SaveButtonName;
        copy.transform.SetSiblingIndex(template.transform.GetSiblingIndex());

        //the menu has no layout group, so stack it just above "Save and quit"
        var rect = (RectTransform)copy.transform;
        rect.anchoredPosition += new Vector2(0f, SaveButtonSpacing);

        TMP_Text tmpLabel = copy.GetComponentInChildren<TMP_Text>(true);
        if(tmpLabel != null) tmpLabel.text = "Save";
        Text legacyLabel = copy.GetComponentInChildren<Text>(true);
        if(legacyLabel != null) legacyLabel.text = "Save";

        return copy.GetComponent<Button>();
    }

    private static void Rewire(Button button, UnityEngine.Events.UnityAction action)
    {
        Undo.RecordObject(button, "Rewire " + button.name);
        for(int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        }
        UnityEventTools.AddPersistentListener(button.onClick, action);
        EditorUtility.SetDirty(button);
    }

    private static Button FindButton(string name)
    {
        foreach(Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if(button.name == name) return button;
        }
        return null;
    }
}
