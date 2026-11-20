using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Project-wide animation defaults for characters, loaded from Resources/CharacterAnimationDefaults.
/// At every scene load, <see cref="CharacterAnimationBootstrap"/> uses it to give each character
/// (anything with the MakeHuman "Game_engine" armature) an idle controller if it has none, and a
/// <see cref="CharacterRigStabilizer"/> so shared clips can't drag the body away from the character.
/// </summary>
[CreateAssetMenu(menuName = "Lil Windows/Character Animation Defaults")]
public class CharacterAnimationDefaults : ScriptableObject
{
    public const string ResourcePath = "CharacterAnimationDefaults";

    [Tooltip("Given to Generic-rig characters with no controller (most characters).")]
    public RuntimeAnimatorController genericIdle;
    [Tooltip("Given to Humanoid-rig characters with no controller (Police Guy.blend: front desk, jail staff, commissary).")]
    public RuntimeAnimatorController humanoidIdle;

    [Serializable]
    public class HeldItem
    {
        [Tooltip("Name used by the HandOver/ReceiveItem sequencer commands, e.g. Medicine.")]
        public string name;
        [Tooltip("Model shown in the hand. Empty = a plain box.")]
        public GameObject prefab;
        [Tooltip("Size as a fraction of the holder's arm length.")]
        public float sizeOfArm = 0.2f;
        public Color fallbackColor = new Color(0.9f, 0.55f, 0.15f);
    }

    [Header("Items people hand to each other")]
    public HeldItem[] heldItems = new HeldItem[0];

    public HeldItem FindItem(string itemName)
    {
        foreach(HeldItem item in heldItems)
        {
            if(item != null && string.Equals(item.name, itemName, StringComparison.OrdinalIgnoreCase)) return item;
        }
        return null;
    }

    [Header("Rig names (MakeHuman Game Engine skeleton)")]
    public string armatureName = "Game_engine";
    public string rootBoneName = "Root";
    public string hipsBoneName = "pelvis";
}

/// <summary>Applies <see cref="CharacterAnimationDefaults"/> to every character in each loaded scene.</summary>
public static class CharacterAnimationBootstrap
{
    //after Awake/OnEnable but before Start and the first Animator evaluation, so the bind pose is still clean
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        ApplyToLoadedScenes();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToLoadedScenes();
    }

    /// <summary>Safe to call repeatedly; characters that are already set up are skipped.</summary>
    public static void ApplyToLoadedScenes()
    {
        CharacterAnimationDefaults defaults = Resources.Load<CharacterAnimationDefaults>(CharacterAnimationDefaults.ResourcePath);
        if(defaults == null)
        {
            Debug.LogWarning("[CharacterAnimationBootstrap] Missing Resources/" + CharacterAnimationDefaults.ResourcePath + "; characters keep their current setup.");
            return;
        }

        int idled = 0;
        int stabilized = 0;
        foreach(Transform armature in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if(armature.name != defaults.armatureName || armature.parent == null) continue;

            Transform character = armature.parent;
            Animator animator = character.GetComponentInParent<Animator>(true);
            if(animator == null) animator = character.gameObject.AddComponent<Animator>();

            if(animator.runtimeAnimatorController == null)
            {
                bool humanoid = animator.avatar != null && animator.avatar.isHuman;
                RuntimeAnimatorController idle = humanoid ? defaults.humanoidIdle : defaults.genericIdle;
                if(idle != null)
                {
                    animator.runtimeAnimatorController = idle;
                    idled++;
                }
            }

            //hands to the clip's own hand goals, kept out of the thighs (sitting clips on other bodies)
            if(animator.avatar != null && animator.avatar.isHuman && !animator.TryGetComponent(out HumanoidHandGoals _))
            {
                animator.gameObject.AddComponent<HumanoidHandGoals>();
            }

            if(!animator.TryGetComponent(out CharacterRigStabilizer stabilizer))
            {
                stabilizer = animator.gameObject.AddComponent<CharacterRigStabilizer>();
                stabilizer.Bind(armature, FindDeep(armature, defaults.rootBoneName), FindDeep(armature, defaults.hipsBoneName));
                stabilized++;
            }
        }

        if(idled > 0 || stabilized > 0)
        {
            Debug.Log("[CharacterAnimationBootstrap] Gave " + idled + " character(s) an idle controller; stabilized " + stabilized + " rig(s).");
        }
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        if(string.IsNullOrEmpty(name)) return null;
        foreach(Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if(child.name == name) return child;
        }
        return null;
    }
}
