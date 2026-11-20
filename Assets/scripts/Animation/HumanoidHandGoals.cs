using UnityEngine;

/// <summary>
/// Humanoid retargeting copies joint angles, so on a body with different arm/leg proportions the hands of a
/// sitting clip can end up inside the knees. This puts each hand on the clip's own hand IK goal (where the
/// hand was on the source body, rescaled to this one), then nudges it out of either thigh if it would still
/// be inside. Needs "IK Pass" on the controller's base layer (the character controllers have it on).
/// Added automatically to Humanoid characters by <see cref="CharacterAnimationBootstrap"/>.
/// </summary>
[RequireComponent(typeof(Animator))]
public class HumanoidHandGoals : MonoBehaviour
{
    //thigh modelled as a capsule from hip to knee; radius and clearance as fractions of thigh length
    public const float ThighRadius = 0.15f;
    public const float ClearanceMargin = 0.03f;

    [Range(0f, 1f)] public float handGoalWeight = 1f;
    public bool keepHandsOutOfThighs = true;

    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if(layerIndex != 0 || animator == null || !animator.isHuman || handGoalWeight <= 0f) return;

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
                target = PushOutOfThigh(target, leftHip.position, leftKnee.position);
                target = PushOutOfThigh(target, rightHip.position, rightKnee.position);
                animator.SetIKPosition(goal, target);
            }
            animator.SetIKPositionWeight(goal, handGoalWeight);
            animator.SetIKRotationWeight(goal, handGoalWeight);
        }
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
