using UnityEngine;

/// <summary>
/// Hands-in-pockets on top of any standing clip: humanoid IK pins each wrist at the front pocket opening, a little
/// into the leg so the fingers disappear "inside", with the elbows hinted out and back. Works on any humanoid; the
/// controller's base layer needs IK Pass (the character controllers have it). Turns off HumanoidHandGoals on the
/// same character, which would otherwise pull the hands back out of the legs.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Animator))]
public class PocketHandsPose : MonoBehaviour
{
    [Range(0f, 1f)] public float weight = 1f;
    [Tooltip("How far out to the side the pocket is, as a fraction of hip width.")]
    public float pocketOut = 0.62f;
    [Tooltip("How far in front of the hip joint, as a fraction of hip width.")]
    public float pocketForward = 0.35f;
    [Tooltip("How far down the thigh the wrist sits, as a fraction of thigh length.")]
    public float pocketDown = 0.2f;
    [Tooltip("How far into the leg the wrist sinks so the fingers are hidden, as a fraction of hip width.")]
    public float pocketDepth = 0.12f;

    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        var goals = GetComponent<HumanoidHandGoals>();
        if(goals != null) goals.enabled = false;
    }

    void OnAnimatorIK(int layerIndex)
    {
        if(layerIndex != 0 || weight <= 0f || animator == null || !animator.isHuman) return;
        Transform leftHip = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        Transform rightHip = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        Transform leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        Transform rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        if(!leftHip || !rightHip || !leftKnee || !rightKnee) return;

        Vector3 right = rightHip.position - leftHip.position;
        float hipWidth = right.magnitude;
        if(hipWidth < 1e-5f) return;
        right /= hipWidth;
        Vector3 up = transform.up;
        Vector3 forward = Vector3.Cross(right, up).normalized;

        Place(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, HumanBodyBones.LeftUpperArm, leftHip, leftKnee, -right, forward, up, hipWidth);
        Place(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, HumanBodyBones.RightUpperArm, rightHip, rightKnee, right, forward, up, hipWidth);
    }

    private void Place(AvatarIKGoal goal, AvatarIKHint hint, HumanBodyBones shoulderBone, Transform hip, Transform knee,
        Vector3 outward, Vector3 forward, Vector3 up, float hipWidth)
    {
        Vector3 thigh = knee.position - hip.position;
        Vector3 pocket = hip.position
            + outward * hipWidth * (pocketOut - pocketDepth)
            + forward * hipWidth * pocketForward
            + thigh * pocketDown;
        animator.SetIKPosition(goal, pocket);
        animator.SetIKPositionWeight(goal, weight);

        Transform shoulder = animator.GetBoneTransform(shoulderBone);
        if(shoulder == null) return;
        float arm = Vector3.Distance(shoulder.position, pocket);
        Vector3 elbow = shoulder.position - up * arm * 0.45f + outward * arm * 0.45f - forward * arm * 0.35f;
        animator.SetIKHintPosition(hint, elbow);
        animator.SetIKHintPositionWeight(hint, weight);
    }
}
