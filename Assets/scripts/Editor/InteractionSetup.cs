using System.IO;
using System.Linq;
using System.Text;
using PixelCrushers.DialogueSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes "use" mean "what you're looking at" in the open scenes.
///
/// The player had two Dialogue System selectors: a raycast Selector (E) and a ProximitySelector (E and right
/// click) that targets whatever usable trigger you stand in. The prisoners carry huge trigger boxes, so inside one
/// the ProximitySelector targeted the prisoner wherever you looked: right-clicking the solitary handbook started
/// IP's Lockdown Script, from far away. This turns the ProximitySelector off, moves right-click onto the raycast
/// Selector, measures use distance from the camera at the same 15-unit range doors use, and disables trigger
/// colliders that only existed for proximity selection (they would also catch the raycast). Safe to run again.
/// </summary>
public static class InteractionSetup
{
    //PlayerLogic.InteractRange: doors open from this far, so talking/reading works from the same distance
    private const float UseRange = 15f;
    public const string RequestPath = "Library/LilWindows.interaction.request";
    public const string ReportPath = "Library/LilWindows.interaction-report.txt";

    [MenuItem("Lil Windows/Fix Interaction (look-at selection)")]
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
        catch(System.Exception e) { File.WriteAllText(ReportPath, "FAILED:\n" + e); Debug.LogException(e); }
    }

    public static void Run()
    {
        var report = new StringBuilder("Interaction setup  " + System.DateTime.Now + "\n");
        for(int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if(!scene.isLoaded) continue;
            GameObject[] roots = scene.GetRootGameObjects();
            bool dirty = false;

            foreach(ProximitySelector proximity in roots.SelectMany(r => r.GetComponentsInChildren<ProximitySelector>(true)))
            {
                if(!proximity.enabled) continue;
                Undo.RecordObject(proximity, "Disable ProximitySelector");
                proximity.enabled = false;
                report.AppendLine("  ProximitySelector on " + proximity.name + ": disabled");
                dirty = true;
            }

            //swap the stock Selector for LookSelector (a subclass, so every serialized setting carries over)
            var lookScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/scripts/Interaction/LookSelector.cs");
            foreach(Selector selector in roots.SelectMany(r => r.GetComponentsInChildren<Selector>(true)))
            {
                if(selector is LookSelector || lookScript == null) continue;
                //read before the swap: changing m_Script replaces the component, destroying this reference
                string ownerName = selector.gameObject.name;
                var so = new SerializedObject(selector);
                so.FindProperty("m_Script").objectReferenceValue = lookScript;
                so.ApplyModifiedProperties();
                report.AppendLine("  Selector on " + ownerName + " -> LookSelector (nearest solid hit, all layers, triggers ignored)");
                dirty = true;
            }

            foreach(Selector selector in roots.SelectMany(r => r.GetComponentsInChildren<Selector>(true)))
            {
                if(selector.distanceFrom == Selector.DistanceFrom.Camera && Mathf.Approximately(selector.maxSelectionDistance, UseRange)
                    && selector.useButton == "Fire2") continue;
                Undo.RecordObject(selector, "Configure Selector");
                report.AppendLine(string.Format("  Selector on {0}: distanceFrom {1} -> Camera, range {2} -> {3}, button '{4}' -> 'Fire2' (right click; E still works)",
                    selector.name, selector.distanceFrom, selector.maxSelectionDistance, UseRange, selector.useButton));
                selector.distanceFrom = Selector.DistanceFrom.Camera;
                selector.maxSelectionDistance = UseRange;
                //"Interact" isn't an Input Manager button in this project, so only E reached this selector
                selector.useButton = "Fire2";
                EditorUtility.SetDirty(selector);
                dirty = true;
            }

            int ranged = 0;
            foreach(Usable usable in roots.SelectMany(r => r.GetComponentsInChildren<Usable>(true)))
            {
                if(usable.maxUseDistance < UseRange)
                {
                    Undo.RecordObject(usable, "Use range");
                    usable.maxUseDistance = UseRange;
                    EditorUtility.SetDirty(usable);
                    ranged++;
                    dirty = true;
                }

                Collider[] colliders = usable.GetComponents<Collider>();
                bool hasSolid = colliders.Any(c => !c.isTrigger);
                foreach(Collider trigger in colliders.Where(c => c.isTrigger && c.enabled))
                {
                    //only when a solid collider remains for the raycast to hit
                    if(!hasSolid) continue;
                    Undo.RecordObject(trigger, "Disable proximity trigger");
                    trigger.enabled = false;
                    EditorUtility.SetDirty(trigger);
                    report.AppendLine("  " + usable.name + ": disabled proximity trigger " + trigger.GetType().Name + " (size " + trigger.bounds.size + ")");
                    dirty = true;
                }
            }
            report.AppendLine("  " + ranged + " Usable(s) set to use range " + UseRange);

            //people: fit the capsule to the shoulders so the air beside someone isn't "them"
            report.AppendLine("  People (capsule radius in world units, vs shoulder width):");
            foreach(Usable usable in roots.SelectMany(r => r.GetComponentsInChildren<Usable>(true)))
            {
                Animator animator = usable.GetComponent<Animator>();
                CapsuleCollider capsule = usable.GetComponent<CapsuleCollider>();
                if(animator == null || capsule == null || animator.avatar == null || !animator.avatar.isHuman) continue;
                Transform leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                if(leftArm == null || rightArm == null) continue;

                Vector3 scale = capsule.transform.lossyScale;
                float axisScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                float worldRadius = capsule.radius * axisScale;
                float shoulders = Vector3.Distance(leftArm.position, rightArm.position);
                //half the shoulder span plus a little for the upper arms
                float fitted = shoulders * 0.6f;
                string line = string.Format("    {0,-26} radius {1:0.00}  shoulders {2:0.00}", usable.name, worldRadius, shoulders);
                if(worldRadius > fitted * 1.1f && axisScale > 1e-5f)
                {
                    Undo.RecordObject(capsule, "Fit capsule to body");
                    capsule.radius = fitted / axisScale;
                    EditorUtility.SetDirty(capsule);
                    line += string.Format("  -> radius {0:0.00}", fitted);
                    dirty = true;
                }
                report.AppendLine(line);
            }

            //for the record: what the ray could pass through before (layers outside the old Default-only mask, triggers)
            report.AppendLine("  Doors / usables (layer, trigger?):");
            foreach(Collider collider in roots.SelectMany(r => r.GetComponentsInChildren<Collider>(true)))
            {
                string path = collider.transform.parent != null ? collider.transform.parent.name + "/" + collider.name : collider.name;
                bool door = path.IndexOf("door", System.StringComparison.OrdinalIgnoreCase) >= 0;
                bool usableCollider = collider.GetComponentInParent<Usable>() != null;
                if(!door && !usableCollider) continue;
                report.AppendLine(string.Format("    {0,-40} {1,-10} layer {2,-14} {3}{4}", path, collider.GetType().Name,
                    LayerMask.LayerToName(collider.gameObject.layer), collider.isTrigger ? "TRIGGER " : "", collider.enabled ? "" : "(disabled)"));
            }

            if(dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                report.AppendLine("  (" + scene.name + " marked dirty; save to keep)");
            }
        }
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log("[InteractionSetup]\n" + report);
    }
}
