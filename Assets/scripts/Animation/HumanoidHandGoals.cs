using UnityEngine;

/// <summary>
/// Humanoid retargeting copies joint angles, so on a body with different arm/leg proportions the hands of a
/// sitting clip can end up inside the knees. This puts each hand on the clip's own hand IK goal (where the
/// hand was on the source body, rescaled to this one), then nudges it out of either thigh if it would still
/// be inside. Needs "IK Pass" on the controller's base layer (the character controllers have it on).
/// Added automatically to Humanoid characters by <see cref="CharacterAnimationBootstrap"/>.
/// </summary>
//ExecuteAlways so it also applies in Animation/Timeline previews and the editor's hand-vs-knee check
[ExecuteAlways]
[RequireComponent(typeof(Animator))]
public class HumanoidHandGoals : MonoBehaviour
{
    //thigh modelled as a capsule from hip to knee; radius and clearance as fractions of thigh length
    public const float ThighRadius = 0.15f;
    //5%: at 3% the IK solver's small reach error still left a few hands touching (-0.7% worst in HandKneeCheck)
    public const float ClearanceMargin = 0.05f;

    [Range(0f, 1f)] public float handGoalWeight = 1f;
    public bool keepHandsOutOfThighs = true;

    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    //a scripted reach (e.g. taking an item from the player) that blends over the clip goal; index 0 = left, 1 = right
    private readonly Vector3[] reachTarget = new Vector3[2];
    private readonly float[] reachWeight = new float[2];

    /// <summary>Pulls a hand toward <paramref name="worldPosition"/> by <paramref name="weight"/> (0 releases it).</summary>
    public void SetReach(AvatarIKGoal hand, Vector3 worldPosition, float weight)
    {
        int i = hand == AvatarIKGoal.LeftHand ? 0 : 1;
        reachTarget[i] = worldPosition;
        reachWeight[i] = Mathf.Clamp01(weight);
    }

    void OnAnimatorIK(int layerIndex)
    {
        if(layerIndex != 0 || animator == null || !animator.isHuman) return;
        ApplyReach(AvatarIKGoal.LeftHand, 0);
        ApplyReach(AvatarIKGoal.RightHand, 1);
        if(handGoalWeight <= 0f || reachWeight[0] > 0f || reachWeight[1] > 0f) return;

        Transform leftHip = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        Transform leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        Transform rightHip = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        Transform rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);

        foreach(AvatarIKGoal goal in new[] { AvatarIKGoal.LeftHand, AvatarIKGoal.RightHand })
        {
            //before anything is set, the goal is the one stored in the playing clip
            if(keepHandsOutOfThighs && leftHip && leftKnee && rightHip && rightKnee)
            {
                Vector3 target = animator.GetIKPosition(goal);
                target += ClearanceOffset(goal == AvatarIKGoal.LeftHand ? LeftHandPoints : RightHandPoints, target,
                    leftHip.position, leftKnee.position, rightHip.position, rightKnee.position);
                animator.SetIKPosition(goal, target);
            }
            animator.SetIKPositionWeight(goal, handGoalWeight);
            animator.SetIKRotationWeight(goal, handGoalWeight);
        }
    }

    private void ApplyReach(AvatarIKGoal goal, int i)
    {
        if(reachWeight[i] <= 0f) return;
        Vector3 clipGoal = animator.GetIKPosition(goal);
        animator.SetIKPosition(goal, Vector3.Lerp(clipGoal, reachTarget[i], reachWeight[i]));
        animator.SetIKPositionWeight(goal, 1f);
        animator.SetIKRotationWeight(goal, 1f - reachWeight[i]);
    }

    //the wrist plus the parts of the hand that rest on a knee
    private static readonly HumanBodyBones[] LeftHandPoints =
        { HumanBodyBones.LeftHand, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftMiddleDistal };
    private static readonly HumanBodyBones[] RightHandPoints =
        { HumanBodyBones.RightHand, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightLittleProximal, HumanBodyBones.RightMiddleDistal };

    /// <summary>
    /// How far to move a hand goal so the whole hand (not just the wrist) clears both thighs. Each point is placed
    /// where it will be once the wrist is at <paramref name="goal"/>, using its offset from the wrist (which depends
    /// on the hand's own pose, not on where IK put it last frame), so the correction doesn't feed back and jitter.
    /// </summary>
    private Vector3 ClearanceOffset(HumanBodyBones[] points, Vector3 goal, Vector3 leftHip, Vector3 leftKnee, Vector3 rightHip, Vector3 rightKnee)
    {
        Transform wrist = animator.GetBoneTransform(points[0]);
        if(wrist == null) return Vector3.zero;
        Vector3 offset = Vector3.zero;
        //two rounds: a push off one thigh can land a finger in the other
        for(int round = 0; round < 2; round++)
        {
            foreach(HumanBodyBones bone in points)
            {
                Transform point = animator.GetBoneTransform(bone);
                if(point == null) continue;
                Vector3 atGoal = goal + offset + (point.position - wrist.position);
                Vector3 pushed = PushOutOfThigh(PushOutOfThigh(atGoal, leftHip, leftKnee), rightHip, rightKnee);
                offset += pushed - atGoal;
            }
        }
        return offset;
    }

    /// <summary>Distance from <paramref name="point"/> to the thigh capsule's surface (negative = inside).</summary>
    public static float ThighClearance(Vector3 point, Vector3 hip, Vector3 knee, float thighLength)
    {
        Vector3 closest = ClosestOnSegment(point, hip, knee);
        return Vector3.Distance(point, closest) - ThighRadius * thighLength;
    }

    /// <summary>Moves <paramref name="target"/> to just outside the hip-knee capsule if it's inside it.</summary>
    public static Vector3 PushOutOfThigh(Vector3 target, Vector3 hip, Vector3 knee)
    {
        float length = Vector3.Distance(hip, knee);
        if(length < 1e-5f) return target;
        float minDistance = (ThighRadius + ClearanceMargin) * length;

        Vector3 closest = ClosestOnSegment(target, hip, knee);
        Vector3 away = target - closest;
        float distance = away.magnitude;
        if(distance >= minDistance) return target;

        //dead centre: lift straight off the top of the leg
        Vector3 direction = distance > 1e-5f ? away / distance : Vector3.up;
        return closest + direction * minDistance;
    }

    private static Vector3 ClosestOnSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-10f));
        return a + ab * t;
    }
}
