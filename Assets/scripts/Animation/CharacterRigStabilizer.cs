using UnityEngine;

/// <summary>
/// Keeps a character's body on its GameObject while it plays clips made on other characters.
///
/// The character clips are Generic and were exported from different .blend/.fbx files. A Generic clip writes
/// absolute local transforms by bone path, including the armature node ("Game_engine") and the "Root" bone,
/// so a clip whose source file had its armature somewhere else (or walked its Root forward) teleports the
/// whole body away from the character. After the Animator runs each frame, this puts the armature node back
/// to this character's own bind transform and pins the Root bone in place. Limb/spine animation is untouched.
///
/// Added automatically by <see cref="CharacterAnimationBootstrap"/>; add it by hand to override the settings.
/// </summary>
[DisallowMultipleComponent]
public class CharacterRigStabilizer : MonoBehaviour
{
    public enum PositionLock
    {
        [Tooltip("Animation moves it freely.")] None,
        [Tooltip("Pinned on the ground plane; animation can still move it up/down.")] Horizontal,
        [Tooltip("Pinned completely.")] Full,
    }

    [Tooltip("The armature node (MakeHuman: \"Game_engine\").")]
    [SerializeField] private Transform armature;
    [Tooltip("The skeleton's top bone (MakeHuman: \"Root\").")]
    [SerializeField] private Transform rootBone;
    [Tooltip("The hips (MakeHuman: \"pelvis\"). Only locked if Hips Lock is set.")]
    [SerializeField] private Transform hipsBone;

    [Tooltip("Restore the armature node's position/rotation/scale every frame. This is the main fix.")]
    public bool lockArmature = true;
    [Tooltip("Stops clips that walk or slide the Root bone from dragging the body off the character.")]
    public PositionLock rootBoneLock = PositionLock.Full;
    [Tooltip("Use Horizontal if a walk clip carries the hips forward and the body pops back when it loops. Leave None for sitting clips.")]
    public PositionLock hipsLock = PositionLock.None;

    private bool hasBindPose;
    private Vector3 armaturePosition;
    private Quaternion armatureRotation;
    private Vector3 armatureScale;
    private Vector3 rootBonePosition;
    private Vector3 hipsPosition;

    /// <summary>Assigns the rig and records its current (un-animated) pose as the pose to hold.</summary>
    public void Bind(Transform armatureNode, Transform root, Transform hips)
    {
        armature = armatureNode;
        rootBone = root;
        hipsBone = hips;
        CaptureBindPose();
    }

    void Awake()
    {
        if(!hasBindPose) CaptureBindPose();
    }

    private void CaptureBindPose()
    {
        if(armature != null)
        {
            armaturePosition = armature.localPosition;
            armatureRotation = armature.localRotation;
            armatureScale = armature.localScale;
        }
        if(rootBone != null) rootBonePosition = rootBone.localPosition;
        if(hipsBone != null) hipsPosition = hipsBone.localPosition;
        hasBindPose = true;
    }

    //LateUpdate runs after the Animator has written this frame's pose
    void LateUpdate()
    {
        if(lockArmature && armature != null)
        {
            armature.localPosition = armaturePosition;
            armature.localRotation = armatureRotation;
            armature.localScale = armatureScale;
        }
        Pin(rootBone, rootBonePosition, rootBoneLock);
        Pin(hipsBone, hipsPosition, hipsLock);
    }

    private void Pin(Transform bone, Vector3 bindPosition, PositionLock mode)
    {
        if(bone == null || mode == PositionLock.None) return;
        if(mode == PositionLock.Full || bone.parent == null)
        {
            bone.localPosition = bindPosition;
            return;
        }
        //measured against the character's up axis in world space: Blender's axis conversion means the
        //bone's local "up" may be Y or Z depending on the export
        Vector3 bindWorld = bone.parent.TransformPoint(bindPosition);
        Vector3 up = transform.up;
        bone.position = bindWorld + up * Vector3.Dot(bone.position - bindWorld, up);
    }
}
