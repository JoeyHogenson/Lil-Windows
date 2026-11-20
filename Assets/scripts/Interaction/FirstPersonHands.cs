using System;
using System.Collections;
using StarterAssets;
using UnityEngine;

/// <summary>
/// The player's own body in first person: a copy of the chosen character's model (DC or Riley, whoever you play
/// as) standing where the player stands, with the head and legs hidden, arms posed by humanoid IK relative to the
/// camera. Holds items in the right hand and hands them over: both people's hands meet between them, the item
/// changes hands, and the arms go back (see the HandOver / ReceiveItem sequencer commands).
///
/// Created automatically once the playthrough is chosen. Items come from CharacterAnimationDefaults.heldItems.
/// </summary>
[RequireComponent(typeof(Animator))]
public class FirstPersonHands : MonoBehaviour
{
    public static FirstPersonHands Instance { get; private set; }

    [Tooltip("Seconds for the arm to reach out (and again to come back).")]
    public float reachDuration = 0.55f;
    [Tooltip("Seconds the hands stay together while the item changes hands.")]
    public float exchangePause = 0.3f;

    private Animator animator;
    private Transform cameraTransform;
    private Transform rightShoulder, rightHand, rightMiddle, head, leftThigh, rightThigh;
    private float armLength;
    private float holdWeight;
    private bool reaching;
    private Vector3 reachPoint;
    private GameObject held;
    private string heldName;

    public bool IsBusy { get; private set; }
    public string HeldItemName => held != null ? heldName : null;

    // ---------- Creation ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        PlaythroughDirector director = PlaythroughDirector.Instance;
        if(director == null)
        {
            Create(null);
            return;
        }
        if(director.HasSelected) Create(PlaythroughDirector.Current.ToString());
        else director.Selected += character => Create(character.ToString());
    }

    /// <summary>Builds the first-person body from the scene model named <paramref name="characterName"/>.</summary>
    public static FirstPersonHands Create(string characterName)
    {
        if(Instance != null) return Instance;
        PlayerLogic player = FindFirstObjectByType<PlayerLogic>();
        Camera camera = Camera.main;
        if(player == null || camera == null) return null;

        Animator source = FindModel(characterName);
        if(source == null)
        {
            Debug.LogWarning("[FirstPersonHands] No humanoid model named \"" + characterName + "\" in the scene; no first-person body.");
            return null;
        }

        //copy inactive, strip everything but the visuals, then switch it on
        bool wasActive = source.gameObject.activeSelf;
        source.gameObject.SetActive(false);
        GameObject body = Instantiate(source.gameObject);
        source.gameObject.SetActive(wasActive);
        body.name = "First Person Body (" + source.name + ")";
        Strip(body);

        Vector3 sourceScale = source.transform.lossyScale;
        body.transform.SetParent(player.transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = new Vector3(sourceScale.x / player.transform.lossyScale.x,
            sourceScale.y / player.transform.lossyScale.y, sourceScale.z / player.transform.lossyScale.z);

        var hands = body.AddComponent<FirstPersonHands>();
        hands.cameraTransform = camera.transform;
        //Awake (animator setup) only runs once the object is active, and Fit needs it
        body.SetActive(true);
        hands.Fit(player.transform);
        return hands;
    }

    private static Animator FindModel(string characterName)
    {
        Animator fallback = null;
        foreach(Animator animator in FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if(animator.avatar == null || !animator.avatar.isHuman || animator.GetComponent<FirstPersonHands>() != null) continue;
            if(!string.IsNullOrEmpty(characterName) && animator.name == characterName) return animator;
            if(fallback == null && animator.name == "DC") fallback = animator;
        }
        return string.IsNullOrEmpty(characterName) ? fallback : null;
    }

    //everything that would make the copy act like an NPC (talking, walking, colliding, being saved) goes
    private static void Strip(GameObject body)
    {
        //twice: a component another one [RequireComponent]s can only go once that one has
        for(int pass = 0; pass < 2; pass++)
        {
            foreach(Component component in body.GetComponentsInChildren<Component>(true))
            {
                if(component == null) continue;
                if(component is Transform || component is Animator || component is Renderer || component is MeshFilter
                    || component is CharacterRigStabilizer) continue;
                if(pass == 0 && component is Collider == false && component is Behaviour == false) continue;
                DestroyImmediate(component);
            }
        }
    }

    void Awake()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var defaults = Resources.Load<CharacterAnimationDefaults>(CharacterAnimationDefaults.ResourcePath);
        if(defaults != null && defaults.humanoidIdle != null) animator.runtimeAnimatorController = defaults.humanoidIdle;
        Instance = this;
    }

    void OnDestroy()
    {
        if(Instance == this) Instance = null;
    }

    //scale so the eyes are at camera height, face the player's forward, and stand a little behind the camera
    private void Fit(Transform player)
    {
        head = animator.GetBoneTransform(HumanBodyBones.Head);
        rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        rightMiddle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        leftThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        rightThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
        if(head == null || rightShoulder == null || rightHand == null || leftFoot == null) return;

        //face forward: rotate so the toes point along the player's forward
        if(leftToes != null)
        {
            Vector3 toes = Vector3.ProjectOnPlane(leftToes.position - leftFoot.position, Vector3.up);
            if(toes.sqrMagnitude > 1e-8f) transform.rotation = Quaternion.FromToRotation(toes.normalized, player.forward) * transform.rotation;
        }

        float feet = Mathf.Min(leftFoot.position.y, transform.position.y);
        float eyeHeight = (head.position.y - feet) * 1.06f;
        float cameraHeight = cameraTransform.position.y - player.position.y;
        if(eyeHeight > 1e-4f) transform.localScale *= cameraHeight / eyeHeight;

        armLength = Vector3.Distance(rightShoulder.position, animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position)
            + Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position, rightHand.position);
        //the camera sits at the eyes, in front of the head bone; step the body back so the chest isn't in view
        transform.position -= player.forward * armLength * 0.18f;
    }

    // ---------- Posing ----------

    private Vector3 HoldPoint
    {
        get
        {
            Transform cam = cameraTransform != null ? cameraTransform : Camera.main.transform;
            return cam.position + cam.forward * armLength * 0.6f + cam.right * armLength * 0.22f - cam.up * armLength * 0.3f;
        }
    }

    void OnAnimatorIK(int layerIndex)
    {
        if(layerIndex != 0 || rightShoulder == null) return;
        float target = reaching || held != null ? 1f : 0f;
        holdWeight = Mathf.MoveTowards(holdWeight, target, Time.deltaTime * 4f);

        Vector3 point = reaching ? reachPoint : HoldPoint;
        animator.SetIKPosition(AvatarIKGoal.RightHand, point);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, reaching ? 1f : holdWeight);
        //elbow down and out, like holding something in front of you
        Vector3 elbow = rightShoulder.position - transform.up * armLength * 0.5f + transform.right * armLength * 0.35f;
        animator.SetIKHintPosition(AvatarIKHint.RightElbow, elbow);
        animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, reaching ? 1f : holdWeight);
    }

    void LateUpdate()
    {
        //first person: no head in front of the lens, no legs standing still while walking
        if(head != null) head.localScale = Vector3.zero;
        if(leftThigh != null) leftThigh.localScale = Vector3.zero;
        if(rightThigh != null) rightThigh.localScale = Vector3.zero;

        if(held != null && held.transform.parent == null)
        {
            held.transform.position = Palm(rightHand, rightMiddle);
            held.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized + Vector3.forward * 1e-4f, Vector3.up);
        }
    }

    private static Vector3 Palm(Transform hand, Transform middle)
    {
        return middle != null ? Vector3.Lerp(hand.position, middle.position, 0.6f) : hand.position;
    }

    // ---------- Items ----------

    /// <summary>Puts <paramref name="itemName"/> in the right hand (replacing whatever was there).</summary>
    public void Hold(string itemName)
    {
        if(held != null && heldName == itemName) return;
        Drop();
        held = MakeItem(itemName, armLength);
        heldName = itemName;
    }

    public void Drop()
    {
        if(held != null) Destroy(held);
        held = null;
        heldName = null;
    }

    /// <summary>Hands the held item to <paramref name="receiver"/>; calls <paramref name="done"/> when the arm is back.</summary>
    public void HandOver(Transform receiver, Action done)
    {
        StartCoroutine(Exchange(receiver, true, done));
    }

    /// <summary><paramref name="giver"/> hands the player <paramref name="itemName"/>; calls <paramref name="done"/> when it's in hand.</summary>
    public void ReceiveFrom(string itemName, Transform giver, Action done)
    {
        Drop();
        Animator other = giver != null ? giver.GetComponentInParent<Animator>() : null;
        Transform otherHand = other != null && other.isHuman ? other.GetBoneTransform(HumanBodyBones.RightHand) : null;
        float otherArm = otherHand != null ? armLength * (other.transform.lossyScale.y / transform.lossyScale.y) : armLength;
        held = MakeItem(itemName, otherArm);
        heldName = itemName;
        if(otherHand != null)
        {
            held.transform.SetParent(otherHand, true);
            held.transform.position = Palm(otherHand, other.GetBoneTransform(HumanBodyBones.RightMiddleProximal));
        }
        StartCoroutine(Exchange(giver, false, done));
    }

    private IEnumerator Exchange(Transform other, bool giving, Action done)
    {
        IsBusy = true;
        Animator otherAnimator = other != null ? other.GetComponentInParent<Animator>() : null;
        HumanoidHandGoals otherGoals = null;
        Vector3 otherChest = other != null ? other.position + Vector3.up * armLength : HoldPoint;
        if(otherAnimator != null && otherAnimator.isHuman)
        {
            otherGoals = otherAnimator.GetComponent<HumanoidHandGoals>();
            if(otherGoals == null) otherGoals = otherAnimator.gameObject.AddComponent<HumanoidHandGoals>();
            Transform chest = otherAnimator.GetBoneTransform(HumanBodyBones.Chest);
            if(chest != null) otherChest = chest.position;
        }

        //meet halfway, but never further than this arm can reach
        Vector3 shoulder = rightShoulder.position;
        Vector3 meet = Vector3.Lerp(shoulder, otherChest, 0.5f);
        if(Vector3.Distance(shoulder, meet) > armLength * 0.9f) meet = shoulder + (meet - shoulder).normalized * armLength * 0.9f;

        reaching = true;
        for(float t = 0f; t < 1f; t += Time.deltaTime / reachDuration)
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            reachPoint = Vector3.Lerp(HoldPoint, meet, s);
            if(otherGoals != null) otherGoals.SetReach(AvatarIKGoal.RightHand, meet, s);
            yield return null;
        }
        reachPoint = meet;

        //the item changes hands
        Transform otherHand = otherAnimator != null && otherAnimator.isHuman ? otherAnimator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if(held != null)
        {
            if(giving)
            {
                if(otherHand != null) held.transform.SetParent(otherHand, true);
                else Destroy(held);
                held = null;
                heldName = null;
            }
            else
            {
                held.transform.SetParent(null, true);
            }
        }
        yield return new WaitForSeconds(exchangePause);

        for(float t = 0f; t < 1f; t += Time.deltaTime / reachDuration)
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            reachPoint = Vector3.Lerp(meet, HoldPoint, s);
            if(otherGoals != null) otherGoals.SetReach(AvatarIKGoal.RightHand, meet, 1f - s);
            yield return null;
        }
        if(otherGoals != null) otherGoals.SetReach(AvatarIKGoal.RightHand, meet, 0f);
        reaching = false;
        IsBusy = false;
        done?.Invoke();
    }

    //the item's model scaled to a fraction of the arm, or a plain box if none is set up
    private static GameObject MakeItem(string itemName, float arm)
    {
        var defaults = Resources.Load<CharacterAnimationDefaults>(CharacterAnimationDefaults.ResourcePath);
        CharacterAnimationDefaults.HeldItem item = defaults != null ? defaults.FindItem(itemName) : null;
        float size = arm * (item != null ? item.sizeOfArm : 0.2f);

        GameObject go;
        if(item != null && item.prefab != null)
        {
            go = Instantiate(item.prefab);
            foreach(Collider c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            renderer.material.color = item != null ? item.fallbackColor : new Color(0.9f, 0.55f, 0.15f);
        }
        go.name = "Held " + itemName;

        //normalize: longest side = size
        Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
        bool any = false;
        foreach(Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if(!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        float longest = any ? Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) : 1f;
        if(longest > 1e-5f) go.transform.localScale *= size / longest;
        return go;
    }
}
