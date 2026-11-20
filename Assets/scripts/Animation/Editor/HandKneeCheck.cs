using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Measures how far each character's hands sink into their thighs on the sitting clips, three ways, using the
/// real runtime setup (a controller with IK Pass on + <see cref="HumanoidHandGoals"/>): plain retargeting (FK),
/// hands solved to the clip's own IK goals, and goals plus the thigh-clearance push. The clip on its own source
/// body is the reference. Report: Library/LilWindows.hand-knee-report.txt
/// </summary>
public static class HandKneeCheck
{
    public const string RequestPath = "Library/LilWindows.hand-knee.request";
    public const string ReportPath = "Library/LilWindows.hand-knee-report.txt";
    private const int Samples = 8;

    private static readonly (string path, string clip, string label)[] SitClips =
    {
        ("Assets/Character Models/anim/Sitting Idle.fbx", "Sitting Idle", "Sitting Idle (prisoners)"),
        ("Assets/Character Models/Old Guy Sitting.blend", "Idle Sitting", "Idle Sitting (DC/Chico/OG)"),
        ("Assets/Animations/Quaternius/UAL1_Standard.fbx", "Sitting_Idle_Loop", "Quaternius sit (front desk)"),
    };

    private static readonly string[] Characters =
    {
        "Assets/Character Models/RandomChar.blend", "Assets/Character Models/DC.blend", "Assets/Character Models/Old Guy Sitting.blend",
        "Assets/Character Models/Police Guy.blend", "Assets/Character Models/Riley.blend", "Assets/Character Models/Nurse Character.blend",
        "Assets/Character Models/Prisoner 2.blend", "Assets/Character Models/Educational Coordinator.blend", "Assets/Character Models/Female Officer 1.blend",
    };

    private enum Mode { FK, ClipGoals, GoalsPlusClearance }

    [MenuItem("Lil Windows/Check Sitting Hands vs Knees")]
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
        catch(Exception e) { File.WriteAllText(ReportPath, "FAILED:\n" + e); Debug.LogException(e); }
    }

    public static void Run()
    {
        var report = new StringBuilder("Hands vs thighs on sitting clips  " + DateTime.Now + "\n");
        report.AppendLine("clearance = distance from hand/knuckles to the thigh axis minus thigh radius, as % of thigh length");
        report.AppendLine("(negative = inside the leg). Worst of both hands vs both thighs, over " + Samples + " samples across the clip.");
        report.AppendLine("'moved' = how far the hips moved from the bind pose (proves the clip actually played).\n");

        foreach(var sit in SitClips)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(sit.path).OfType<AnimationClip>().FirstOrDefault(c => c.name == sit.clip);
            if(clip == null) { report.AppendLine(sit.label + ": clip missing"); continue; }
            report.AppendLine(sit.label);

            Result reference = Measure(sit.path, clip, Mode.FK);
            report.AppendLine(string.Format("  {0,-34} {1}   <- clip on its own body", Path.GetFileName(sit.path) + " (source)", reference));

            foreach(string character in Characters)
            {
                report.AppendLine(string.Format("  {0,-34} FK {1}   clip goals {2}   goals+clearance {3}", Path.GetFileName(character),
                    Measure(character, clip, Mode.FK), Measure(character, clip, Mode.ClipGoals), Measure(character, clip, Mode.GoalsPlusClearance)));
            }
            report.AppendLine();
        }

        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log("[HandKneeCheck]\n" + report);
    }

    private struct Result
    {
        public bool ok;
        public float clearance;
        public float moved;
        public string error;

        public override string ToString()
        {
            if(!ok) return ("n/a (" + error + ")").PadRight(22);
            return string.Format("{0,7}% (moved {1:0.00})", (clearance * 100f).ToString("0.0"), moved);
        }
    }

    private static Result Measure(string modelPath, AnimationClip clip, Mode mode)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
        if(model == null || avatar == null || !avatar.isHuman) return new Result { error = "no humanoid avatar" };

        //an in-memory controller with IK Pass, like the game's character controllers
        var controller = new AnimatorController { name = "HandKneeCheck" };
        controller.AddLayer("Base");
        AnimatorControllerLayer[] layers = controller.layers;
        layers[0].iKPass = true;
        controller.layers = layers;
        AnimatorState state = controller.layers[0].stateMachine.AddState("Sit");
        state.motion = clip;

        var instance = (GameObject)UnityEngine.Object.Instantiate(model);
        instance.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            Animator animator = instance.GetComponent<Animator>();
            if(animator == null) animator = instance.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = controller;

            if(mode != Mode.FK)
            {
                var goals = instance.AddComponent<HumanoidHandGoals>();
                goals.handGoalWeight = 1f;
                goals.keepHandsOutOfThighs = mode == Mode.GoalsPlusClearance;
            }

            Transform hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 bindHips = hipsBone.position;
            float worst = float.MaxValue;
            float moved = 0f;
            for(int i = 0; i < Samples; i++)
            {
                animator.Play(state.nameHash, 0, (float)i / Samples);
                //three times: the clearance push reads hand/thigh positions the previous update left behind
                animator.Update(0f);
                animator.Update(0f);
                animator.Update(0f);
                moved = Mathf.Max(moved, Vector3.Distance(hipsBone.position, bindHips));

                float thighLength = Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position,
                    animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position);
                var points = new List<Vector3>();
                foreach(HumanBodyBones bone in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftIndexProximal, HumanBodyBones.RightIndexProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.LeftLittleProximal, HumanBodyBones.RightLittleProximal, HumanBodyBones.LeftMiddleDistal, HumanBodyBones.RightMiddleDistal })
                {
                    Transform t = animator.GetBoneTransform(bone);
                    if(t != null) points.Add(t.position);
                }
                foreach(bool left in new[] { true, false })
                {
                    Vector3 hip = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg).position;
                    Vector3 knee = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg).position;
                    foreach(Vector3 p in points)
                    {
                        worst = Mathf.Min(worst, HumanoidHandGoals.ThighClearance(p, hip, knee, thighLength) / thighLength);
                    }
                }
            }
            return new Result { ok = true, clearance = worst, moved = moved };
        }
        catch(Exception e)
        {
            return new Result { error = e.GetType().Name };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
            UnityEngine.Object.DestroyImmediate(controller);
        }
    }
}
