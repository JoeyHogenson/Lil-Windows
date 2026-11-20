using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PixelCrushers;
using PixelCrushers.DialogueSystem;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Applies the dialogue/quest/scene changes for: the medicine quest (fixed flow, nurse hands you the pills, you hand
/// them to OG), auto-paged long NPC lines, click-to-finish/click-to-continue dialogue, the nurse's pocket pose, the
/// clock's day length, held-item setup, and Infirmary clutter. Each part is safe to run again.
/// Report: Library/LilWindows.session-report.txt
/// </summary>
public static class SessionSetup
{
    private const string DatabasePath = "Assets/Lil Windows Dialogue Database.asset";
    private const string DialogueUIPath = "Assets/UI/Lil Windows Dialogue UI.prefab";
    private const string DefaultsPath = "Assets/Resources/CharacterAnimationDefaults.asset";
    public const string RequestPath = "Library/LilWindows.session.request";
    public const string ReportPath = "Library/LilWindows.session-report.txt";

    //about two lines of VCR Mono in the subtitle box; longer NPC lines become pages
    private const int MaxLineChars = 220;
    private const string Perm = "Get Written Permission for OG's Medicine";
    private const string Quest = "Get Medicine";

    private static StringBuilder report;

    [MenuItem("Lil Windows/Apply Session Setup (quest, dialogue, nurse, clutter)")]
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
        bool redress = File.Exists(RedressRequestPath);
        if(!File.Exists(RequestPath) && !redress) return;
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if(redress)
        {
            File.Delete(RedressRequestPath);
            try { Redress(); }
            catch(Exception e) { File.AppendAllText(ReportPath, "\nFAILED:\n" + e); Debug.LogException(e); }
            return;
        }
        File.Delete(RequestPath);
        try { Run(); }
        catch(Exception e) { File.AppendAllText(ReportPath, "\nFAILED:\n" + e); Debug.LogException(e); }
    }

    public static void Run()
    {
        report = new StringBuilder("Session setup  " + DateTime.Now + "\n");
        File.WriteAllText(ReportPath, report.ToString());

        var database = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DatabasePath);
        if(database != null)
        {
            Undo.RecordObject(database, "Session setup");
            Step("Medicine quest", () => FixMedicineQuest(database));
            Step("Long lines", () => SplitLongLines(database));
            EditorUtility.SetDirty(database);
        }
        else report.AppendLine("Dialogue database not found at " + DatabasePath);

        Step("Continue / fast-forward", SetUpContinue);
        Step("Held items", SetUpHeldItems);
        Step("Scene", SetUpScene);
        Step("Infirmary clutter", DressInfirmary);

        AssetDatabase.SaveAssets();
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log("[SessionSetup]\n" + report);
    }

    //one failing part shouldn't stop the others
    private static void Step(string name, Action action)
    {
        report.AppendLine("\n== " + name);
        try { action(); }
        catch(Exception e) { report.AppendLine("  FAILED: " + e); }
    }

    // ---------- Medicine quest ----------

    private static void FixMedicineQuest(DialogueDatabase db)
    {
        Template template = Template.FromDefault();

        //quest: a second step, and urgency in the description
        Item quest = db.GetItem(Quest);
        if(quest != null)
        {
            Field.SetValue(quest.fields, "Description", "OG's angina is getting worse. Get his pills before it's too late.");
            Field.SetValue(quest.fields, "Entry Count", "2", FieldType.Number);
            Field.SetValue(quest.fields, "Entry 1", "Get OG's angina pills from the infirmary");
            Field.SetValue(quest.fields, "Entry 2", "Bring the pills to OG before he collapses");
            Field.SetValue(quest.fields, "Entry 2 State", "unassigned");
            report.AppendLine("  quest '" + Quest + "': 2 steps (get pills, bring them to OG), urgent description");
        }

        //nurse
        Conversation nurse = db.GetConversation("Infirmary Dialogue");
        if(nurse != null)
        {
            DialogueEntry start = nurse.GetDialogueEntry(0);
            DialogueEntry greet = nurse.GetDialogueEntry(1);
            DialogueEntry ask = nurse.GetDialogueEntry(2);
            DialogueEntry confirm = nurse.GetDialogueEntry(6);
            DialogueEntry give = nurse.GetDialogueEntry(9);
            string hasPermission = "(CurrentQuestState(\"" + Perm + "\") == \"active\" or Variable[\"hasWrittenPerms\"] == true)";
            string needsPills = "CurrentQuestState(\"" + Quest + "\") == \"active\" and Variable[\"hasOGMedicine\"] ~= true";

            //the bug: both openings were valid when you came back with the permission, and the first one listed
            //(the greeting) won, which only leads back to "go see the CO". The check for permission now goes first,
            //and the greeting only shows when you don't have it yet.
            confirm.conditionsString = needsPills + " and " + hasPermission;
            greet.conditionsString = needsPills + " and not " + hasPermission;
            ask.conditionsString = needsPills;
            MoveLinkFirst(start, 6);

            give.userScript = "SetQuestState(\"" + Perm + "\", \"success\"); SetQuestEntryState(\"" + Quest + "\", 1, \"success\"); "
                + "SetQuestEntryState(\"" + Quest + "\", 2, \"active\"); Variable[\"hasOGMedicine\"] = true; "
                + "ShowAlert(\"Get the pills to OG. Fast.\")";
            give.Sequence = "ReceiveItem(Medicine, speaker)";

            //talking to her again with the pills in hand
            if(!nurse.dialogueEntries.Any(e => e.DialogueText.StartsWith("What are you still doing here")))
            {
                DialogueEntry hurry = NewEntry(template, nurse, greet.ActorID, greet.ConversantID,
                    "What are you still doing here? Get those to him. Angina doesn't wait.",
                    "CurrentQuestState(\"" + Quest + "\") == \"active\" and Variable[\"hasOGMedicine\"] == true");
                start.outgoingLinks.Add(new Link(nurse.id, start.id, nurse.id, hurry.id));
            }
            report.AppendLine("  nurse: permission check before the greeting (the loop that kept the quest from completing), "
                + "pills handed over on screen (ReceiveItem), hurry line while you carry them");
        }

        //CO
        Conversation co = db.GetConversation("Correctional Officer");
        if(co != null)
        {
            DialogueEntry permit = co.GetDialogueEntry(3);
            if(permit != null && !permit.userScript.Contains("ShowAlert"))
            {
                permit.userScript = "Variable[\"hasWrittenPerms\"] = true; ShowAlert(\"You have written permission. Take it to the nurse.\")";
            }
            DialogueEntry askCo = co.GetDialogueEntry(2);
            if(askCo != null) askCo.conditionsString = "CurrentQuestState(\"" + Quest + "\") == \"active\" and Variable[\"hasWrittenPerms\"] ~= true";
            report.AppendLine("  CO: permission alert; won't offer the permission twice");
        }

        //OG: waiting, hand-over, thanks
        Conversation og = db.GetConversation("OG");
        if(og != null && !og.dialogueEntries.Any(e => e.DialogueText.StartsWith("Is that... my pills")))
        {
            DialogueEntry start = og.GetDialogueEntry(0);
            DialogueEntry ask = og.GetDialogueEntry(1);
            int ogId = ask.ActorID;
            int playerId = ask.ConversantID;

            DialogueEntry waiting = NewEntry(template, og, ogId, playerId,
                "Did you get them? My chest... it keeps getting tighter.",
                "CurrentQuestState(\"" + Quest + "\") == \"active\" and Variable[\"hasOGMedicine\"] ~= true");
            DialogueEntry notYet = NewEntry(template, og, playerId, ogId, "Not yet. I'm on it.", "");
            Chain(og, waiting, notYet);

            DialogueEntry sees = NewEntry(template, og, ogId, playerId, "Is that... my pills?",
                "CurrentQuestState(\"" + Quest + "\") == \"active\" and Variable[\"hasOGMedicine\"] == true");
            DialogueEntry handOver = NewEntry(template, og, playerId, ogId, "Here. Take them.", "");
            handOver.Sequence = "HandOver(Medicine, listener)";
            handOver.userScript = "Variable[\"hasOGMedicine\"] = false; SetQuestEntryState(\"" + Quest + "\", 2, \"success\"); "
                + "SetQuestState(\"" + Quest + "\", \"success\"); ShowAlert(\"Quest Completed: Get Medicine\")";
            DialogueEntry thanks = NewEntry(template, og, ogId, playerId,
                "Thank you. I mean it. Most people in here wouldn't have bothered.", "");
            Chain(og, sees, handOver);
            Chain(og, handOver, thanks);

            DialogueEntry after = NewEntry(template, og, ogId, playerId, "Pills are working. I owe you one.",
                "CurrentQuestState(\"" + Quest + "\") == \"success\"");

            foreach(DialogueEntry opener in new[] { sees, waiting, after })
            {
                start.outgoingLinks.Add(new Link(og.id, start.id, og.id, opener.id));
            }
            report.AppendLine("  OG: 'did you get them?' while waiting, the hand-over (HandOver sequence completes the quest), thanks after");
        }
    }

    private static DialogueEntry NewEntry(Template template, Conversation conversation, int actorId, int conversantId, string text, string conditions)
    {
        int id = conversation.dialogueEntries.Count == 0 ? 0 : conversation.dialogueEntries.Max(e => e.id) + 1;
        DialogueEntry entry = template.CreateDialogueEntry(id, conversation.id, string.Empty);
        entry.ActorID = actorId;
        entry.ConversantID = conversantId;
        entry.DialogueText = text;
        entry.conditionsString = conditions ?? string.Empty;
        DialogueEntry last = conversation.dialogueEntries.LastOrDefault();
        if(last != null) entry.canvasRect = new Rect(last.canvasRect.x + 30f, last.canvasRect.y + 60f, last.canvasRect.width, last.canvasRect.height);
        conversation.dialogueEntries.Add(entry);
        return entry;
    }

    private static void Chain(Conversation conversation, DialogueEntry from, DialogueEntry to)
    {
        from.outgoingLinks.Add(new Link(conversation.id, from.id, conversation.id, to.id));
    }

    private static void MoveLinkFirst(DialogueEntry entry, int destinationId)
    {
        Link link = entry.outgoingLinks.FirstOrDefault(l => l.destinationDialogueID == destinationId);
        if(link == null) return;
        entry.outgoingLinks.Remove(link);
        entry.outgoingLinks.Insert(0, link);
    }

    // ---------- Long lines ----------

    private static void SplitLongLines(DialogueDatabase db)
    {
        Template template = Template.FromDefault();
        int split = 0, pages = 0, skipped = 0;
        foreach(Conversation conversation in db.conversations)
        {
            foreach(DialogueEntry entry in conversation.dialogueEntries.ToList())
            {
                if(entry.id == 0 || entry.isGroup) continue;
                string text = entry.DialogueText;
                if(string.IsNullOrEmpty(text) || text.Length <= MaxLineChars) continue;
                //player lines are menu choices; splitting would change what the menu shows
                if(db.IsPlayerID(entry.ActorID)) continue;
                //markup could be cut in half
                if(text.Contains("<") || text.Contains("[") || text.Contains("|")) { skipped++; continue; }

                List<string> chunks = Paginate(text, MaxLineChars);
                if(chunks.Count < 2) continue;

                //first page keeps the conditions and incoming links; the last gets the script, sequence and exits
                List<Link> exits = entry.outgoingLinks;
                string script = entry.userScript;
                string sequence = entry.Sequence;
                entry.DialogueText = chunks[0];
                entry.outgoingLinks = new List<Link>();
                entry.userScript = string.Empty;
                entry.Sequence = string.Empty;

                DialogueEntry previous = entry;
                for(int i = 1; i < chunks.Count; i++)
                {
                    DialogueEntry page = NewEntry(template, conversation, entry.ActorID, entry.ConversantID, chunks[i], string.Empty);
                    page.canvasRect = new Rect(entry.canvasRect.x, entry.canvasRect.y + 50f * i, entry.canvasRect.width, entry.canvasRect.height);
                    previous.outgoingLinks.Add(new Link(conversation.id, previous.id, conversation.id, page.id));
                    previous = page;
                }
                previous.userScript = script;
                previous.Sequence = sequence;
                foreach(Link exit in exits)
                {
                    exit.originDialogueID = previous.id;
                    previous.outgoingLinks.Add(exit);
                }
                split++;
                pages += chunks.Count;
            }
        }
        report.AppendLine("  " + split + " long NPC line(s) split into " + pages + " pages of <= " + MaxLineChars + " chars"
            + (skipped > 0 ? "; " + skipped + " skipped (contain markup)" : ""));
    }

    //greedy by sentence; a sentence longer than a page is broken at the last comma or space that fits
    private static List<string> Paginate(string text, int max)
    {
        var sentences = new List<string>();
        int startIndex = 0;
        for(int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool end = (c == '.' || c == '!' || c == '?' || c == '\n') && (i + 1 == text.Length || char.IsWhiteSpace(text[i + 1]));
            if(end)
            {
                sentences.Add(text.Substring(startIndex, i + 1 - startIndex).Trim());
                startIndex = i + 1;
            }
        }
        if(startIndex < text.Length) sentences.Add(text.Substring(startIndex).Trim());
        sentences.RemoveAll(string.IsNullOrEmpty);

        var pages = new List<string>();
        var current = new StringBuilder();
        foreach(string sentence in sentences)
        {
            string rest = sentence;
            while(rest.Length > max)
            {
                if(current.Length > 0) { pages.Add(current.ToString()); current.Clear(); }
                int cut = rest.LastIndexOf(", ", max, StringComparison.Ordinal);
                if(cut < max / 2) cut = rest.LastIndexOf(' ', max);
                if(cut <= 0) cut = max;
                pages.Add(rest.Substring(0, cut + (rest[cut] == ',' ? 1 : 0)).Trim());
                rest = rest.Substring(cut + 1).Trim();
            }
            if(current.Length > 0 && current.Length + 1 + rest.Length > max) { pages.Add(current.ToString()); current.Clear(); }
            if(current.Length > 0) current.Append(' ');
            current.Append(rest);
        }
        if(current.Length > 0) pages.Add(current.ToString());
        return pages;
    }

    // ---------- Continue ----------

    private static void SetUpContinue()
    {
        //dialogue waits for the player instead of auto-advancing on a timer
        for(int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if(!scene.isLoaded) continue;
            foreach(DialogueSystemController controller in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DialogueSystemController>(true)))
            {
                var so = new SerializedObject(controller);
                SerializedProperty mode = so.FindProperty("displaySettings.subtitleSettings.continueButton");
                if(mode == null) continue;
                mode.enumValueIndex = (int)DisplaySettings.SubtitleSettings.ContinueButtonMode.Always;
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(scene);
                report.AppendLine("  " + scene.name + "/" + controller.name + ": continue mode -> Always (waits for the player)");
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(DialogueUIPath);
        try
        {
            StandardDialogueUI ui = root.GetComponentInChildren<StandardDialogueUI>(true);
            int wired = 0;
            foreach(Button button in root.GetComponentsInChildren<Button>(true).Where(b => b.name == "Continue Button"))
            {
                var fastForward = button.GetComponent<StandardUIContinueButtonFastForward>();
                if(fastForward == null) fastForward = button.gameObject.AddComponent<PixelCrushers.DialogueSystem.Wrappers.StandardUIContinueButtonFastForward>();
                fastForward.dialogueUI = ui;
                Transform panel = button.transform.parent;
                fastForward.typewriterEffect = panel != null ? panel.GetComponentInChildren<AbstractTypewriterEffect>(true) : null;

                //first press finishes the typing, the next one continues
                for(int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(button.onClick, i);
                UnityEventTools.AddVoidPersistentListener(button.onClick, fastForward.OnFastForward);

                //anywhere-click and keys press it (its own image stops taking clicks so one click isn't two presses)
                if(button.targetGraphic != null) button.targetGraphic.raycastTarget = false;
                foreach(PixelCrushers.UIButtonKeyTrigger old in button.GetComponents<PixelCrushers.UIButtonKeyTrigger>()) Object.DestroyImmediate(old, true);
                foreach(KeyCode key in new[] { KeyCode.Mouse0, KeyCode.Space, KeyCode.Return })
                {
                    var trigger = button.gameObject.AddComponent<PixelCrushers.Wrappers.UIButtonKeyTrigger>();
                    trigger.key = key;
                    trigger.buttonName = string.Empty;
                }
                wired++;
                report.AppendLine("  " + AnimationUtility.CalculateTransformPath(button.transform, root.transform)
                    + ": fast-forward (typewriter " + (fastForward.typewriterEffect != null ? "found" : "none") + "), click/Space/Enter");
            }
            PrefabUtility.SaveAsPrefabAsset(root, DialogueUIPath);
            report.AppendLine("  " + wired + " continue button(s) in " + DialogueUIPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------- Held items ----------

    private static void SetUpHeldItems()
    {
        var defaults = AssetDatabase.LoadAssetAtPath<CharacterAnimationDefaults>(DefaultsPath);
        var pills = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/models/Pills.blend");
        if(defaults == null) { report.AppendLine("  defaults asset missing"); return; }
        var items = defaults.heldItems?.ToList() ?? new List<CharacterAnimationDefaults.HeldItem>();
        CharacterAnimationDefaults.HeldItem medicine = items.FirstOrDefault(i => i.name == "Medicine");
        if(medicine == null) items.Add(medicine = new CharacterAnimationDefaults.HeldItem { name = "Medicine" });
        medicine.prefab = pills;
        medicine.sizeOfArm = 0.18f;
        defaults.heldItems = items.ToArray();
        EditorUtility.SetDirty(defaults);
        report.AppendLine("  Medicine -> " + (pills != null ? "Assets/models/Pills.blend" : "plain box (Pills.blend not found)"));
    }

    // ---------- Scene ----------

    private static void SetUpScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameController game = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
        if(game != null && !Mathf.Approximately(game.dayLengthSeconds, 960f))
        {
            Undo.RecordObject(game, "Day length");
            report.AppendLine("  day length " + game.dayLengthSeconds + "s -> 960s (one in-game hour per real minute, 6 AM to 10 PM)");
            game.dayLengthSeconds = 960f;
            EditorUtility.SetDirty(game);
        }

        Animator nurse = FindCharacter("Nurse");
        if(nurse != null && nurse.GetComponent<PocketHandsPose>() == null)
        {
            Undo.AddComponent<PocketHandsPose>(nurse.gameObject);
            report.AppendLine("  Nurse: hands-in-pockets pose (PocketHandsPose)");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        report.AppendLine("  (" + scene.name + " marked dirty; save to keep)");
    }

    private static Animator FindCharacter(string name)
    {
        return Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(a => a.name == name);
    }

    // ---------- Clutter ----------

    private class Prop
    {
        public string path;
        public string kind;     //floor, wall, onTop
        public float meters;    //floor: height; wall: width; onTop: longest side
        public float mountHeight;
        public int count = 1;
    }

    private static readonly Prop[] InfirmaryProps =
    {
        new Prop { path = "Assets/Prefabs/Cell/cell bed.prefab", kind = "floor", meters = 0.55f, count = 2 },
        new Prop { path = "Assets/models/Shelves.blend", kind = "floor", meters = 1.8f },
        new Prop { path = "Assets/Prefabs/Cell/Desk (2).prefab", kind = "floor", meters = 0.75f },
        new Prop { path = "Assets/models/Basic Chair.fbx", kind = "floor", meters = 0.9f },
        new Prop { path = "Assets/Prefabs/Cell/Jail sink (2).prefab", kind = "floor", meters = 0.9f },
        new Prop { path = "Assets/models/trashcan.blend", kind = "floor", meters = 0.6f },
        new Prop { path = "Assets/models/wallshelf.blend", kind = "wall", meters = 0.9f, mountHeight = 1.4f },
        new Prop { path = "Assets/models/Bulletin Board.blend", kind = "wall", meters = 1.0f, mountHeight = 1.5f },
        new Prop { path = "Assets/models/Clock.blend", kind = "wall", meters = 0.3f, mountHeight = 2.0f },
        new Prop { path = "Assets/models/Pills.blend", kind = "onTop", meters = 0.12f, count = 5 },
        new Prop { path = "Assets/models/tray.obj", kind = "onTop", meters = 0.45f },
        new Prop { path = "Assets/models/RFI Paper.blend", kind = "onTop", meters = 0.28f, count = 2 },
    };

    private const string ClutterParent = "Infirmary Clutter (generated)";
    public const string RedressRequestPath = "Library/LilWindows.redress.request";

    /// <summary>Deletes the generated clutter and places it again.</summary>
    [MenuItem("Lil Windows/Redo Infirmary Clutter")]
    public static void Redress()
    {
        report = new StringBuilder("Infirmary clutter (redo)  " + DateTime.Now + "\n");
        GameObject old = GameObject.Find(ClutterParent);
        if(old != null) Undo.DestroyObjectImmediate(old);
        Step("Infirmary clutter", DressInfirmary);
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log("[SessionSetup]\n" + report);
    }

    private static void DressInfirmary()
    {
        const string parentName = ClutterParent;
        Animator nurse = FindCharacter("Nurse");
        if(nurse == null) { report.AppendLine("  no Nurse in the scene"); return; }
        if(GameObject.Find(parentName) != null) { report.AppendLine("  already dressed (Lil Windows > Redo Infirmary Clutter to redo)"); return; }

        //scale from the nurse: her height is ~1.7 m
        Bounds body = Encapsulate(nurse.gameObject);
        float unitsPerMeter = body.size.y / 1.7f;
        Vector3 origin = new Vector3(nurse.transform.position.x, body.min.y, nurse.transform.position.z);
        Vector3 chest = origin + Vector3.up * body.size.y * 0.6f;
        report.AppendLine(string.Format("  nurse height {0:0.0} units -> {1:0.00} units per meter", body.size.y, unitsPerMeter));

        //the room: walls by casting out from her chest in 24 directions
        var walls = new List<RaycastHit>();
        var ignore = new HashSet<Collider>(Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).SelectMany(a => a.GetComponentsInChildren<Collider>()));
        for(int i = 0; i < 24; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 15f, 0f) * Vector3.forward;
            RaycastHit[] hits = Physics.RaycastAll(chest, dir, 12f * unitsPerMeter, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit? nearest = hits.Where(h => !ignore.Contains(h.collider)).OrderBy(h => h.distance).Cast<RaycastHit?>().FirstOrDefault();
            if(nearest.HasValue) walls.Add(nearest.Value);
        }
        if(walls.Count < 6) { report.AppendLine("  couldn't find the room's walls around the nurse; nothing placed"); return; }
        //openings (doors, corridors) read as far hits; only use the near walls
        float median = walls.Select(w => w.distance).OrderBy(d => d).ElementAt(walls.Count / 2);
        List<RaycastHit> slots = walls.Where(w => w.distance < median * 1.6f && Mathf.Abs(w.normal.y) < 0.3f).ToList();
        report.AppendLine(string.Format("  room: {0} wall hits, typical distance {1:0.0} m", slots.Count, median / unitsPerMeter));

        var parent = new GameObject(parentName);
        Undo.RegisterCreatedObjectUndo(parent, "Dress infirmary");
        var placed = new List<Bounds>();
        var surfaces = new List<Bounds>();
        int slot = 0;
        float keepClear = 0.9f * unitsPerMeter;

        foreach(Prop prop in InfirmaryProps)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prop.path);
            if(asset == null) { report.AppendLine("  missing " + prop.path); continue; }
            for(int n = 0; n < prop.count; n++)
            {
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent.scene);
                go.transform.SetParent(parent.transform, true);
                //the Blender props come without colliders: give them solid ones, so the player can't walk through
                //them and items can be set down on their actual surfaces
                foreach(MeshFilter filter in go.GetComponentsInChildren<MeshFilter>())
                {
                    if(filter.sharedMesh != null && filter.GetComponent<Collider>() == null) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                }
                Undo.RegisterCreatedObjectUndo(go, "Place prop");
                bool ok = prop.kind == "onTop"
                    ? PlaceOnTop(go, prop, unitsPerMeter, surfaces, placed)
                    : PlaceAgainstWall(go, prop, unitsPerMeter, slots, ref slot, origin, keepClear, placed, surfaces);
                if(!ok)
                {
                    Object.DestroyImmediate(go);
                    report.AppendLine("  no room for " + Path.GetFileNameWithoutExtension(prop.path));
                    continue;
                }
                report.AppendLine(string.Format("  {0,-16} at {1}", Path.GetFileNameWithoutExtension(prop.path), go.transform.position.ToString("0.0")));
            }
        }
        EditorSceneManager.MarkSceneDirty(parent.scene);
        report.AppendLine("  all under \"" + parentName + "\": move/delete freely");
    }

    private static bool PlaceAgainstWall(GameObject go, Prop prop, float unitsPerMeter, List<RaycastHit> slots, ref int slot,
        Vector3 origin, float keepClear, List<Bounds> placed, List<Bounds> surfaces)
    {
        //measure the model upright and unrotated, then scale to its real-world size
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        Bounds raw = Encapsulate(go);
        if(raw.size.sqrMagnitude < 1e-8f) return false;
        float measure = prop.kind == "wall" ? Mathf.Max(raw.size.x, raw.size.z) : raw.size.y;
        go.transform.localScale = Vector3.one * (prop.meters * unitsPerMeter / Mathf.Max(measure, 1e-5f));
        raw = Encapsulate(go);
        //the thin horizontal side faces the wall
        bool thinIsX = raw.size.x < raw.size.z;

        for(int tries = 0; tries < slots.Count; tries++)
        {
            RaycastHit wall = slots[(slot + tries * 5) % slots.Count];
            Vector3 normal = new Vector3(wall.normal.x, 0f, wall.normal.z).normalized;
            Quaternion facing = Quaternion.LookRotation(normal, Vector3.up) * (thinIsX ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity);
            go.transform.rotation = facing;
            Bounds b = Encapsulate(go);
            float depth = Vector3.Dot(b.size, new Vector3(Mathf.Abs(normal.x), 0f, Mathf.Abs(normal.z)));
            Vector3 point = wall.point + normal * (depth * 0.5f + 0.03f * unitsPerMeter);
            point.y = prop.kind == "wall" ? origin.y + prop.mountHeight * unitsPerMeter : FloorBelow(point, origin.y);
            go.transform.position = point;
            b = Encapsulate(go);
            //sit on the floor / hang at height: line the bottom (or centre) up with the target
            go.transform.position += Vector3.up * ((prop.kind == "wall" ? point.y - b.center.y : point.y - b.min.y));
            b = Encapsulate(go);

            bool blocksNurse = Vector2.Distance(new Vector2(b.center.x, b.center.z), new Vector2(origin.x, origin.z)) < keepClear + b.extents.magnitude * 0.5f;
            bool overlaps = placed.Any(p => Shrunk(p, 0.9f).Intersects(b));
            if(blocksNurse || overlaps) continue;
            placed.Add(b);
            if(prop.kind == "floor" && b.size.y < 1.3f * unitsPerMeter || prop.path.Contains("Shelves") || prop.path.Contains("wallshelf")) surfaces.Add(b);
            slot = (slot + tries * 5 + 3) % slots.Count;
            return true;
        }
        return false;
    }

    private static bool PlaceOnTop(GameObject go, Prop prop, float unitsPerMeter, List<Bounds> surfaces, List<Bounds> placed)
    {
        if(surfaces.Count == 0) return false;
        go.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        go.transform.localScale = Vector3.one;
        Bounds raw = Encapsulate(go);
        float longest = Mathf.Max(raw.size.x, Mathf.Max(raw.size.y, raw.size.z));
        go.transform.localScale = Vector3.one * (prop.meters * unitsPerMeter / Mathf.Max(longest, 1e-5f));
        for(int tries = 0; tries < 12; tries++)
        {
            Bounds top = surfaces[UnityEngine.Random.Range(0, surfaces.Count)];
            Vector3 spot = new Vector3(UnityEngine.Random.Range(top.min.x + top.size.x * 0.2f, top.max.x - top.size.x * 0.2f), top.max.y + top.size.y,
                UnityEngine.Random.Range(top.min.z + top.size.z * 0.2f, top.max.z - top.size.z * 0.2f));
            //drop onto whatever is really there (the top of a shelf mesh isn't its bounds)
            if(!Physics.Raycast(spot, Vector3.down, out RaycastHit hit, top.size.y * 2f, ~0, QueryTriggerInteraction.Ignore)) continue;
            //must land on that piece of furniture (its upper part), not fall through a gap to the floor
            if(!top.Contains(hit.point + Vector3.up * 0.01f) || hit.point.y < top.min.y + top.size.y * 0.3f) continue;
            go.transform.position = hit.point;
            Bounds b = Encapsulate(go);
            go.transform.position += Vector3.up * (hit.point.y - b.min.y);
            b = Encapsulate(go);
            if(placed.Any(p => p.size.y < top.size.y * 0.5f && Shrunk(p, 0.9f).Intersects(b))) continue;
            placed.Add(b);
            return true;
        }
        return false;
    }

    private static float FloorBelow(Vector3 point, float fallback)
    {
        return Physics.Raycast(point + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y - fallback) < 3f
            ? hit.point.y : fallback;
    }

    private static Bounds Encapsulate(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if(renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach(Renderer r in renderers.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }

    private static Bounds Shrunk(Bounds b, float factor)
    {
        return new Bounds(b.center, b.size * factor);
    }
}
