using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// Switches every MakeHuman ("Game_engine" skeleton) character model and animation file from Generic to
/// Humanoid, so clips retarget onto each body's own proportions instead of copying another body's absolute
/// bone rotations (the cause of the sitting lean).
///
/// Per model it: lets Unity auto-generate an avatar, checks every mapped bone against an explicit table
/// (the MakeHuman Game Engine skeleton uses Unreal mannequin names), writes the verified table in explicitly,
/// enforces a true T-pose reference on the arms if the rest pose isn't one, reimports and re-verifies.
/// Then it gives each Animator in the open scenes its own model's avatar, and measures torso lean/hip height
/// on the real sitting/idle clips before and after. Everything is written to Library/LilWindows.humanoid-report.txt.
/// </summary>
public static class HumanoidConversion
{
    private const string ModelsFolder = "Assets/Character Models";
    public const string RequestPath = "Library/LilWindows.humanoid-convert.request";
    public const string ReportPath = "Library/LilWindows.humanoid-report.txt";
    private const float SampleTime = 0.5f;

    //MakeHuman "Game engine" skeleton -> Unity human bone (HumanTrait.BoneName spelling)
    private static readonly (string bone, string human)[] BoneTable =
    {
        ("pelvis", "Hips"), ("spine_01", "Spine"), ("spine_02", "Chest"), ("spine_03", "UpperChest"),
        ("neck_01", "Neck"), ("head", "Head"),
        ("thigh_l", "LeftUpperLeg"), ("calf_l", "LeftLowerLeg"), ("foot_l", "LeftFoot"), ("ball_l", "LeftToes"),
        ("thigh_r", "RightUpperLeg"), ("calf_r", "RightLowerLeg"), ("foot_r", "RightFoot"), ("ball_r", "RightToes"),
        ("clavicle_l", "LeftShoulder"), ("upperarm_l", "LeftUpperArm"), ("lowerarm_l", "LeftLowerArm"), ("hand_l", "LeftHand"),
        ("clavicle_r", "RightShoulder"), ("upperarm_r", "RightUpperArm"), ("lowerarm_r", "RightLowerArm"), ("hand_r", "RightHand"),
        ("thumb_01_l", "Left Thumb Proximal"), ("thumb_02_l", "Left Thumb Intermediate"), ("thumb_03_l", "Left Thumb Distal"),
        ("index_01_l", "Left Index Proximal"), ("index_02_l", "Left Index Intermediate"), ("index_03_l", "Left Index Distal"),
        ("middle_01_l", "Left Middle Proximal"), ("middle_02_l", "Left Middle Intermediate"), ("middle_03_l", "Left Middle Distal"),
        ("ring_01_l", "Left Ring Proximal"), ("ring_02_l", "Left Ring Intermediate"), ("ring_03_l", "Left Ring Distal"),
        ("pinky_01_l", "Left Little Proximal"), ("pinky_02_l", "Left Little Intermediate"), ("pinky_03_l", "Left Little Distal"),
        ("thumb_01_r", "Right Thumb Proximal"), ("thumb_02_r", "Right Thumb Intermediate"), ("thumb_03_r", "Right Thumb Distal"),
        ("index_01_r", "Right Index Proximal"), ("index_02_r", "Right Index Intermediate"), ("index_03_r", "Right Index Distal"),
        ("middle_01_r", "Right Middle Proximal"), ("middle_02_r", "Right Middle Intermediate"), ("middle_03_r", "Right Middle Distal"),
        ("ring_01_r", "Right Ring Proximal"), ("ring_02_r", "Right Ring Intermediate"), ("ring_03_r", "Right Ring Distal"),
        ("pinky_01_r", "Right Little Proximal"), ("pinky_02_r", "Right Little Intermediate"), ("pinky_03_r", "Right Little Distal"),
    };

    //the clips characters actually sit/stand in, measured before and after
    private static readonly (string path, string clip, string label)[] TestClips =
    {
        ("Assets/Character Models/anim/Sitting Idle.fbx", "Sitting Idle", "Sitting Idle (prisoners)"),
        ("Assets/Character Models/Old Guy Sitting.blend", "Idle Sitting", "Idle Sitting (DC/Chico/OG)"),
        ("Assets/Character Models/anim/Idle (1).fbx", "New Idle Anim", "Standing idle"),
        ("Assets/Animations/Quaternius/UAL1_Standard.fbx", "Sitting_Idle_Loop", "Quaternius sit (front desk)"),
    };

    private struct Measurement
    {
        public bool ok;
        public string error;
        public float leanBack;     // degrees; + = torso tilted back, - = forward
        public float hipsHeight;   // in character units above its own origin
        public float hipsDrift;    // horizontal distance of the hips from the character origin
    }

    // ---------- Triggers ----------

    [MenuItem("Lil Windows/Convert Characters To Humanoid (checks every model)")]
    private static void RunFromMenu()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Humanoid conversion", "Leave Play mode first.", "OK");
            return;
        }
        Run();
    }

    //lets the conversion be queued from outside the editor: it runs once the editor is idle and out of Play mode
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
        catch(Exception e)
        {
            File.AppendAllText(ReportPath, "\n\nFAILED WITH EXCEPTION:\n" + e);
            Debug.LogException(e);
        }
    }

    // ---------- Conversion ----------

    public static void Run()
    {
        var report = new StringBuilder();
        report.AppendLine("Lil Windows humanoid conversion  " + DateTime.Now);
        report.AppendLine(new string('=', 78));

        List<string> rigged = FindRiggedModels(report);
        List<string> characters = rigged.Where(IsCharacterModel).ToList();
        report.AppendLine();

        // 0. before
        report.AppendLine("BEFORE (current setup; Generic clips, with CharacterRigStabilizer emulated):");
        var before = MeasureAll(characters, humanoid: false, report);

        // 1. Humanoid + let Unity auto-generate an avatar to compare against
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(string path in rigged)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.autoGenerateAvatarMappingIfUnspecified = true;
                HumanDescription cleared = importer.humanDescription;
                cleared.human = new HumanBone[0];
                cleared.skeleton = new SkeletonBone[0];
                importer.humanDescription = cleared;
                ConfigureClips(importer);
                importer.SaveAndReimport();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        // 2. check each model's mapping by hand, write the explicit table, fix the T-pose, reimport, re-verify
        report.AppendLine();
        report.AppendLine("PER-MODEL AVATAR CHECK:");
        var failed = new List<string>();
        foreach(string path in rigged)
        {
            if(!VerifyAndPin(path, report)) failed.Add(path);
        }

        // 3. scene Animators get their own model's avatar
        report.AppendLine();
        report.AppendLine("SCENE ANIMATORS:");
        AssignSceneAvatars(rigged, report);

        // 4. after
        report.AppendLine();
        report.AppendLine("AFTER (Humanoid, each character with its own avatar, CharacterRigStabilizer emulated):");
        var after = MeasureAll(characters, humanoid: true, report);

        report.AppendLine();
        report.AppendLine("LEAN COMPARISON (deg, + = leaning back)  |  hips height  |  hips drift from origin:");
        foreach(string path in characters)
        {
            report.AppendLine("  " + Path.GetFileName(path));
            foreach(var test in TestClips)
            {
                string key = path + "|" + test.label;
                before.TryGetValue(key, out Measurement b);
                after.TryGetValue(key, out Measurement a);
                report.AppendLine(string.Format("    {0,-30} lean {1,7} -> {2,-7}  height {3,7} -> {4,-7}  drift {5,7} -> {6,-7}",
                    test.label, Fmt(b, m => m.leanBack), Fmt(a, m => m.leanBack), Fmt(b, m => m.hipsHeight), Fmt(a, m => m.hipsHeight),
                    Fmt(b, m => m.hipsDrift), Fmt(a, m => m.hipsDrift)));
            }
        }

        report.AppendLine();
        report.AppendLine(failed.Count == 0 ? "RESULT: every model has a valid, verified Humanoid avatar."
            : "RESULT: " + failed.Count + " model(s) need attention: " + string.Join(", ", failed.Select(Path.GetFileName)));

        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log("[HumanoidConversion] Done. Report: " + Path.GetFullPath(ReportPath) + "\n" + report);
    }

    private static string Fmt(Measurement m, Func<Measurement, float> pick)
    {
        if(!m.ok) return m.error != null ? "n/a" : "-";
        return pick(m).ToString("0.00");
    }

    private static List<string> FindRiggedModels(StringBuilder report)
    {
        var found = new List<string>();
        report.AppendLine("Models with the MakeHuman skeleton under " + ModelsFolder + ":");
        foreach(string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if(!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(model == null) continue;
            var names = new HashSet<string>(model.GetComponentsInChildren<Transform>(true).Select(t => t.name));
            bool hasRig = names.Contains("pelvis") && names.Contains("head") && names.Contains("upperarm_l") && names.Contains("thigh_r");
            if(!hasRig) continue;
            found.Add(path);
            report.AppendLine(string.Format("  {0,-52} was {1}{2}", path.Substring(ModelsFolder.Length + 1), importer.animationType,
                IsCharacterModel(path) ? "  [character]" : "  [animation only]"));
        }
        return found;
    }

    //animation-only files live in anim/; everything else has a body someone plays as or talks to
    private static bool IsCharacterModel(string path)
    {
        return !path.Replace('\\', '/').Contains("/anim/");
    }

    //bake root rotation and height into the pose (keeps sitting heights, no sinking); bake XZ too except for
    //walks, which stay in place so NPCController/NavMesh move the character instead of the clip
    private static void ConfigureClips(ModelImporter importer)
    {
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        //an empty list means Unity's defaults; filling it in could change clip IDs the controllers point at
        if(clips == null || clips.Length == 0) return;
        foreach(ModelImporterClipAnimation clip in clips)
        {
            bool walk = clip.name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = !walk;
            clip.keepOriginalPositionXZ = true;
        }
        importer.clipAnimations = clips;
    }

    private static bool VerifyAndPin(string path, StringBuilder report)
    {
        string label = path.Substring(ModelsFolder.Length + 1);
        report.AppendLine("  " + label);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Dictionary<string, Transform> transforms = model.GetComponentsInChildren<Transform>(true)
            .GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());

        Avatar auto = LoadAvatar(path);
        HumanDescription description;
        if(auto != null && auto.isValid && auto.isHuman)
        {
            description = auto.humanDescription;
            var autoMap = description.human.ToDictionary(h => h.humanName, h => h.boneName);
            int agree = 0;
            var disagreements = new List<string>();
            foreach(var (bone, humanName) in BoneTable)
            {
                autoMap.TryGetValue(humanName, out string autoBone);
                if(autoBone == bone) agree++;
                else if(transforms.ContainsKey(bone)) disagreements.Add(humanName + ": auto=" + (autoBone ?? "none") + " expected=" + bone);
            }
            report.AppendLine("    auto-mapping agrees on " + agree + "/" + BoneTable.Length + " bones"
                + (disagreements.Count > 0 ? "; overriding: " + string.Join("; ", disagreements) : ""));
        }
        else
        {
            report.AppendLine("    Unity's auto-mapping failed (" + (auto == null ? "no avatar" : "invalid/not human")
                + "); building the avatar from the explicit table and the model's rest pose");
            description = importer.humanDescription;
            description.skeleton = BuildSkeleton(model);
        }

        // explicit table, only bones this model has
        var human = new List<HumanBone>();
        var missingRequired = new List<string>();
        string[] traitNames = HumanTrait.BoneName;
        foreach(var (bone, humanName) in BoneTable)
        {
            if(transforms.ContainsKey(bone))
            {
                human.Add(new HumanBone { boneName = bone, humanName = humanName, limit = new HumanLimit { useDefaultValues = true } });
            }
            else
            {
                int index = Array.IndexOf(traitNames, humanName);
                if(index >= 0 && HumanTrait.RequiredBone(index)) missingRequired.Add(humanName + " (" + bone + ")");
            }
        }
        if(missingRequired.Count > 0) report.AppendLine("    MISSING REQUIRED BONES: " + string.Join(", ", missingRequired));
        description.human = human.ToArray();

        // T-pose reference
        description.skeleton = EnforceArmTPose(description.skeleton, transforms, report);

        importer.humanDescription = description;
        importer.SaveAndReimport();

        Avatar pinned = LoadAvatar(path);
        bool ok = pinned != null && pinned.isValid && pinned.isHuman && missingRequired.Count == 0;
        report.AppendLine("    final avatar: " + (pinned == null ? "MISSING" : (pinned.isValid ? "valid" : "INVALID") + ", " + (pinned.isHuman ? "humanoid" : "NOT HUMANOID"))
            + ", " + human.Count + " bones mapped" + (ok ? "  OK" : "  NEEDS ATTENTION"));
        return ok;
    }

    private static Avatar LoadAvatar(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
    }

    private static SkeletonBone[] BuildSkeleton(GameObject model)
    {
        return model.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
        {
            name = t == model.transform ? model.name + "(Clone)" : t.name,
            position = t.localPosition,
            rotation = t.localRotation,
            scale = t.localScale,
        }).ToArray();
    }

    // Humanoid retargeting assumes the reference pose is a T-pose. If the rest pose has the arms angled down
    // (A-pose), rotate upper and lower arms in the reference skeleton so each points straight out sideways.
    private static SkeletonBone[] EnforceArmTPose(SkeletonBone[] skeleton, Dictionary<string, Transform> transforms, StringBuilder report)
    {
        var bones = skeleton.ToList();
        int IndexOf(string name) => bones.FindIndex(b => b.name == name);

        Matrix4x4 World(string name)
        {
            Transform t = transforms.TryGetValue(name, out Transform found) ? found : null;
            int i = IndexOf(name);
            if(t == null || i < 0) return Matrix4x4.identity;
            Matrix4x4 local = Matrix4x4.TRS(bones[i].position, bones[i].rotation, bones[i].scale);
            Transform parent = t.parent;
            return parent == null || IndexOf(parent.name) < 0 ? local : World(parent.name) * local;
        }
        Vector3 Pos(string name) => World(name).GetColumn(3);

        if(!transforms.ContainsKey("upperarm_l") || !transforms.ContainsKey("upperarm_r")) return skeleton;

        //sideways = the horizontal axis the shoulders lie along
        Vector3 shoulders = Pos("upperarm_r") - Pos("upperarm_l");
        Vector3 up = Vector3.up;
        Vector3 sideways = Vector3.ProjectOnPlane(shoulders, up).normalized;

        foreach(string side in new[] { "l", "r" })
        {
            string upper = "upperarm_" + side, lower = "lowerarm_" + side, hand = "hand_" + side;
            float outwardSign = side == "r" ? 1f : -1f;
            Vector3 target = sideways * outwardSign;

            float before = Vector3.Angle(Pos(lower) - Pos(upper), target);
            foreach(var (bone, child) in new[] { (upper, lower), (lower, hand) })
            {
                int i = IndexOf(bone);
                if(i < 0 || !transforms.ContainsKey(child)) continue;
                Vector3 dir = Pos(child) - Pos(bone);
                Quaternion delta = Quaternion.FromToRotation(dir.normalized, target);
                Matrix4x4 world = World(bone);
                Quaternion worldRot = world.rotation;
                Transform parent = transforms[bone].parent;
                Quaternion parentRot = parent != null && IndexOf(parent.name) >= 0 ? World(parent.name).rotation : Quaternion.identity;
                SkeletonBone b = bones[i];
                b.rotation = Quaternion.Inverse(parentRot) * (delta * worldRot);
                bones[i] = b;
            }
            float after = Vector3.Angle(Pos(lower) - Pos(upper), target);
            float elbow = Vector3.Angle(Pos(hand) - Pos(lower), target);
            report.AppendLine(string.Format("    {0} arm reference: {1:0.0} deg off T-pose -> {2:0.0} (forearm {3:0.0})",
                side == "l" ? "left " : "right", before, after, elbow));
        }
        return bones.ToArray();
    }

    private static void AssignSceneAvatars(List<string> rigged, StringBuilder report)
    {
        var riggedSet = new HashSet<string>(rigged);
        for(int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if(!scene.isLoaded) continue;
            bool dirty = false;
            foreach(GameObject root in scene.GetRootGameObjects())
            {
                foreach(Animator animator in root.GetComponentsInChildren<Animator>(true))
                {
                    GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(animator.gameObject);
                    string path = source != null ? AssetDatabase.GetAssetPath(source) : null;
                    if(path == null || !riggedSet.Contains(path)) continue;
                    Avatar avatar = LoadAvatar(path);
                    if(avatar == null || animator.avatar == avatar) continue;
                    Undo.RecordObject(animator, "Assign humanoid avatar");
                    animator.avatar = avatar;
                    EditorUtility.SetDirty(animator);
                    dirty = true;
                    report.AppendLine("  " + scene.name + ": " + animator.name + " -> " + avatar.name);
                }
            }
            if(dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                report.AppendLine("  (" + scene.name + " marked dirty; save it to keep the avatars)");
            }
        }
    }

    // ---------- Measuring ----------

    private static Dictionary<string, Measurement> MeasureAll(List<string> characters, bool humanoid, StringBuilder report)
    {
        var results = new Dictionary<string, Measurement>();
        foreach(string path in characters)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Avatar avatar = humanoid ? LoadAvatar(path) : null;
            foreach(var test in TestClips)
            {
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(test.path).OfType<AnimationClip>()
                    .FirstOrDefault(c => c.name == test.clip && !c.name.StartsWith("__preview__"));
                Measurement m;
                if(clip == null) m = new Measurement { error = "clip not found" };
                else if(clip.isHumanMotion && (avatar == null || !avatar.isHuman)) m = new Measurement { error = "humanoid clip needs a humanoid avatar" };
                //the stabilizer runs in-game either way, so both measurements include it
                else m = Measure(model, avatar, clip, emulateStabilizer: true);
                results[path + "|" + test.label] = m;
            }
        }
        report.AppendLine("  measured " + characters.Count + " characters x " + TestClips.Length + " clips");
        return results;
    }

    private static Measurement Measure(GameObject model, Avatar avatar, AnimationClip clip, bool emulateStabilizer)
    {
        var instance = (GameObject)UnityEngine.Object.Instantiate(model);
        instance.hideFlags = HideFlags.HideAndDontSave;
        PlayableGraph graph = default;
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Dictionary<string, Transform> bones = instance.GetComponentsInChildren<Transform>(true)
                .GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            if(!bones.TryGetValue("pelvis", out Transform hips) || !bones.TryGetValue("head", out Transform head))
            {
                return new Measurement { error = "no pelvis/head" };
            }

            bones.TryGetValue("Game_engine", out Transform armature);
            bones.TryGetValue("Root", out Transform root);
            Vector3 armaturePos = armature ? armature.localPosition : Vector3.zero;
            Quaternion armatureRot = armature ? armature.localRotation : Quaternion.identity;
            Vector3 armatureScale = armature ? armature.localScale : Vector3.one;
            Vector3 rootPos = root ? root.localPosition : Vector3.zero;

            Animator animator = instance.GetComponent<Animator>();
            if(animator == null) animator = instance.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();

            graph = PlayableGraph.Create("HumanoidConversion.Measure");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "out", animator);
            var playable = AnimationClipPlayable.Create(graph, clip);
            output.SetSourcePlayable(playable);
            playable.SetTime(Mathf.Min(SampleTime, clip.length * 0.5f));
            graph.Evaluate();

            if(emulateStabilizer)
            {
                if(armature)
                {
                    armature.localPosition = armaturePos;
                    armature.localRotation = armatureRot;
                    armature.localScale = armatureScale;
                }
                if(root) root.localPosition = rootPos;
            }

            //facing = where the toes point (model forward conventions differ between exports)
            Vector3 up = Vector3.up;
            Vector3 forward = Vector3.forward;
            if(bones.TryGetValue("foot_l", out Transform foot) && bones.TryGetValue("ball_l", out Transform ball))
            {
                Vector3 toes = Vector3.ProjectOnPlane(ball.position - foot.position, up);
                if(toes.sqrMagnitude > 1e-8f) forward = toes.normalized;
            }
            Vector3 torso = head.position - hips.position;
            float lean = Mathf.Atan2(Vector3.Dot(torso, -forward), Vector3.Dot(torso, up)) * Mathf.Rad2Deg;
            Vector3 hipsFlat = Vector3.ProjectOnPlane(hips.position, up);
            return new Measurement { ok = true, leanBack = lean, hipsHeight = Vector3.Dot(hips.position, up), hipsDrift = hipsFlat.magnitude };
        }
        catch(Exception e)
        {
            return new Measurement { error = e.Message };
        }
        finally
        {
            if(graph.IsValid()) graph.Destroy();
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}
