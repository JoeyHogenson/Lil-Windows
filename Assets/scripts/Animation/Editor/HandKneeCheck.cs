using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Measures how far each character's hands sink into their thighs on the sitting clips, three ways:
/// plain retargeting (FK), hands solved to the clip's own IK goals, and goals plus the thigh-clearance push
/// that <see cref="HumanoidHandGoals"/> does at runtime. The clip on its own source body is the reference.
/// Report: Library/LilWindows.hand-knee-report.txt
/// </summary>
public static class HandKneeCheck
{
    public const string RequestPath = "Library/LilWindows.hand-knee.request";
    public const string ReportPath = "Library/LilWindows.hand-knee-report.txt";

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
        report.AppendLine("(negative = inside the leg). Worst of left/right, worst over 8 samples across the clip.\n");

        foreach(var sit in SitClips)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(sit.path).OfType<AnimationClip>().FirstOrDefault(c => c.name == sit.clip);
            if(clip == null) { report.AppendLine(sit.label + ": clip missing"); continue; }
            report.AppendLine(sit.label);

            string sourcePath = sit.path;
            float reference = Measure(sourcePath, clip, Mode.FK);
            report.AppendLine(string.Format("  {0,-34} {1,8}   <- reference: clip on its own body", Path.GetFileName(sourcePath) + " (source)", Pct(reference)));

            foreach(string character in Characters)
            {
                float fk = Measure(character, clip, Mode.FK);
                float goals = Measure(character, clip, Mode.ClipGoals);
                float push = Measure(character, clip, Mode.GoalsPlusClearance);
                report.AppendLine(string.Format("  {0,-34} FK {1,8}   clip goals {2,8}   goals+clearance {3,8}",
                    Path.GetFileName(character), Pct(fk), Pct(goals), Pct(push)));
            }
            report.AppendLine();
        }

        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log("[HandKneeCheck]\n" + report);
    }

    private static string Pct(float v) => float.IsNaN(v) ? "n/a" : (v * 100f).ToString("0.0") + "%";

    private static float Measure(string modelPath, AnimationClip clip, Mode mode)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
        if(model == null || avatar == null || !avatar.isHuman) return float.NaN;

        var instance = (GameObject)UnityEngine.Object.Instantiate(model);
        instance.hideFlags = HideFlags.HideAndDontSave;
        PlayableGraph graph = default;
        try
        {
            Animator animator = instance.GetComponent<Animator>();
            if(animator == null) animator = instance.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.Rebind();

            graph = PlayableGraph.Create("HandKneeCheck");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var clipPlayable = AnimationClipPlayable.Create(graph, clip);
            var job = new HandGoalJob
            {
                mode = (int)mode,
                leftThigh = animator.BindStreamTransform(animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg)),
                leftKnee = animator.BindStreamTransform(animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg)),
                rightThigh = animator.BindStreamTransform(animator.GetBoneTransform(HumanBodyBones.RightUpperLeg)),
                rightKnee = animator.BindStreamTransform(animator.GetBoneTransform(HumanBodyBones.RightLowerLeg)),
            };
            var scriptPlayable = AnimationScriptPlayable.Create(graph, job, 1);
            graph.Connect(clipPlayable, 0, scriptPlayable, 0);
            scriptPlayable.SetInputWeight(0, 1f);
            AnimationPlayableOutput.Create(graph, "out", animator).SetSourcePlayable(scriptPlayable);

            float worst = float.MaxValue;
            for(int i = 0; i < 8; i++)
            {
                clipPlayable.SetTime(clip.length * i / 8f);
                graph.Evaluate();
                foreach(bool left in new[] { true, false })
                {
                    Transform thigh = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                    Transform knee = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                    float thighLength = Vector3.Distance(thigh.position, knee.position);
                    var points = new List<Vector3> { animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand).position };
                    Transform knuckle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
                    if(knuckle != null) points.Add(knuckle.position);
                    //either hand can rest on either knee
                    foreach(bool otherLeft in new[] { true, false })
                    {
                        Transform t = animator.GetBoneTransform(otherLeft ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                        Transform k = animator.GetBoneTransform(otherLeft ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                        foreach(Vector3 p in points)
                        {
                            float clearance = HumanoidHandGoals.ThighClearance(p, t.position, k.position, thighLength);
                            worst = Mathf.Min(worst, clearance / thighLength);
                        }
                    }
                }
            }
            return worst;
        }
        finally
        {
            if(graph.IsValid()) graph.Destroy();
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private struct HandGoalJob : IAnimationJob
    {
        public int mode;
        public TransformStreamHandle leftThigh, leftKnee, rightThigh, rightKnee;

        public void ProcessRootMotion(AnimationStream stream) { }

        public void ProcessAnimation(AnimationStream stream)
        {
            if(mode == (int)Mode.FK) return;
            AnimationHumanStream human = stream.AsHuman();
            foreach(AvatarIKGoal goal in new[] { AvatarIKGoal.LeftHand, AvatarIKGoal.RightHand })
            {
                if(mode == (int)Mode.GoalsPlusClearance)
                {
                    Vector3 target = human.GetGoalPosition(goal);
                    target = HumanoidHandGoals.PushOutOfThigh(target, leftThigh.GetPosition(stream), leftKnee.GetPosition(stream));
                    target = HumanoidHandGoals.PushOutOfThigh(target, rightThigh.GetPosition(stream), rightKnee.GetPosition(stream));
                    human.SetGoalPosition(goal, target);
                }
                human.SetGoalWeightPosition(goal, 1f);
                human.SetGoalWeightRotation(goal, 1f);
            }
            human.SolveIK();
        }
    }
}
