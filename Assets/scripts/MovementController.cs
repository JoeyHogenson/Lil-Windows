using System.Collections;
using UnityEngine;
using WeaponInterfaces;

/// <summary>
/// Controls player movement, interaction, weapons, HUD updates, and environment states.
/// </summary>
public class MovementController : MonoBehaviour
{
    [Header("References")]
    public HUDManager hudManager;
    public Hammer hammer;
    //public PickAxe pickAxe;
    public CarScript currentCar;
    public CanvasGroup fade;
    public GameObject rustArm;
    public GameObject clockHand;
    public GameObject daySpawn;
    public GameObject interactPromptUI;
    public GameObject underwaterVolume;
    public GameObject[] allWeapons;
    public Light dayLight;

    [Header("Player Stats")]
    public int hp = 8;
    public int energy = 8;
    public int currency = 0;
    public int carriedFuel = 0;
    public int oil = 8;
    public int fuel = 8;
    public int wood = 0;
    public int maxWood = 3;

    [Header("Progression")]
    public int taskMax = 100;
    private int dayTasks = 0;
    private int disrepair = 0;
    public float rust = 0f;
    public float time = 0f;

    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 10f;
    public float jumpForce = 5f;
    public float gravity = -9.81f;
    public float sensitivity = 2f;

    [Header("Water Settings")]
    public Transform waterSurface;
    public float waterHeightOverride = -999f;
    public float transitionTime = 0.5f;

    private CharacterController characterController;
    private Vector3 moveDirection;
    private bool isGrounded;
    private bool inputEnabled = true;
    private bool isUnderwater = false;
    private float transitionTimer = 0f;

    private float rotationX = 0f;
    private float rotationY = 0f;
    private int timer = 0;

    public IMeleeWeapon currentWeapon;
    private IInteractable currentTarget;

    private string[] inventory;

    // ---------------- Unity Methods ---------------- //

    private void Start()
    {
        Initialize();
        //SetWeapon(pickAxe);
    }

    private void Update()
    {
        CheckUnderwaterState();
        ApplyGravity();

        if (!inputEnabled) return;

        HandleInteractionRaycast();
        timer = (timer + 1) % 300;

        if (time > 1f) time = 1f;

        Move();
        HealthUpdate();
        RotateRustArm();
        RotateClockHand();

        rust = Mathf.Clamp(rust + 0.0000001f, 0f, 8f);
        time += 0.00005f;
    }

    // ---------------- Initialization ---------------- //

    private void Initialize()
    {
        timer = 0;
        transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        inventory = new string[10];
    }

    // ---------------- Input & Interaction ---------------- //

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !enabled;
    }

    private void HandleInteractionRaycast()
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
        if (Physics.Raycast(ray, out RaycastHit hit, 3f))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                currentTarget = interactable;
                interactPromptUI.SetActive(true);

                if (Input.GetKeyDown(KeyCode.F))
                {
                    currentTarget.Interact();
                    interactPromptUI.SetActive(false);
                }
            }
            else
            {
                ClearCurrentTarget();
            }
        }
        else
        {
            ClearCurrentTarget();
        }
    }

    private void ClearCurrentTarget()
    {
        currentTarget = null;
        interactPromptUI.SetActive(false);
    }

    // ---------------- Movement ---------------- //

    private void Move()
    {
        HandleMouseLook();

        float moveSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 forward = transform.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 move = transform.right * horizontal + forward * vertical;
        moveDirection.x = move.x * moveSpeed;
        moveDirection.z = move.z * moveSpeed;

        HandleJump();
        HandleShooting();
        HandleGroundedCheck();

        characterController.Move(moveDirection * Time.deltaTime);
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        rotationX = Mathf.Clamp(rotationX - mouseY, -85f, 85f);
        rotationY += mouseX;

        transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0);
    }

    private void HandleJump()
    {
        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            moveDirection.y = Mathf.Sqrt(2 * jumpForce);
        }
    }

    private void HandleGroundedCheck()
    {
        isGrounded = characterController.isGrounded;
        if (isGrounded && moveDirection.y > 0) moveDirection.y = -1f;
    }

    private void ApplyGravity()
    {
        moveDirection.y += gravity * Time.deltaTime;
    }

    // ---------------- Weapons ---------------- //

    public void SetWeapon(IMeleeWeapon weapon)
    {
        currentWeapon = weapon;

        foreach (GameObject w in allWeapons)
            if (w != null) w.SetActive(false);

        if (weapon is MonoBehaviour mb) mb.gameObject.SetActive(true);
    }

    private void HandleShooting()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            dayTasks++;
            currentWeapon.Attack();
            Debug.Log($"{dayTasks} exertion (out of {taskMax})");

            if (dayTasks > taskMax)
            {
                if (dayTasks % 10 == 0)
                    Debug.Log("You are overexerting yourself");

                for (int i = 0; i <= dayTasks - taskMax; i++)
                {
                    if (Random.Range(0, dayTasks) == dayTasks - 1)
                    {
                        disrepair++;
                        Debug.Log($"{disrepair} stack of disrepair");
                    }
                }
            }
        }
    }

    // ---------------- Environment & Day Cycle ---------------- //

    private void RotateRustArm()
    {
        if (rustArm != null)
            rustArm.transform.rotation = Quaternion.Euler(0, 0, rust * -20);
    }

    private void RotateClockHand()
    {
        if (clockHand != null)
        {
            clockHand.transform.rotation = Quaternion.Euler(0, 0, time * -360);
            dayLight.intensity = 3f * (1f - time);
        }
    }

    public void EndDay()
    {
        TeleportTo(daySpawn);
        time = 0f;
        hp = 8;
        dayTasks = 0;
        rust = Mathf.Min(rust + 0.5f, 8f);
    }

    private void CheckUnderwaterState()
    {
        float camY = Camera.main.transform.position.y;
        float waterY = underwaterVolume.transform.position.y + underwaterVolume.transform.localScale.y / 2f;
        bool belowWater = camY < waterY;

        if (belowWater != isUnderwater)
        {
            transitionTimer += Time.deltaTime;
            if (transitionTimer >= transitionTime)
            {
                isUnderwater = belowWater;
                underwaterVolume?.SetActive(isUnderwater);
                transitionTimer = 0f;
            }
        }
        else
        {
            transitionTimer = 0f;
        }
    }

    // ---------------- HUD & Health ---------------- //

    private void HealthUpdate()
    {
        hudManager.hp = hp;
    }

    // ---------------- Teleport & Fading ---------------- //

    public void TeleportTo(GameObject target)
    {
        if (target != null)
            StartCoroutine(TeleportWithFade(target));
        else
            Debug.LogError("Target GameObject is null!");
    }

    private IEnumerator TeleportWithFade(GameObject target)
    {
        SetInputEnabled(false);
        yield return StartCoroutine(FadeCanvas(fade, 0f, 1f, 0.1f));

        characterController.enabled = false;
        transform.position = target.transform.position;
        transform.rotation = target.transform.rotation;
        characterController.enabled = true;

        yield return new WaitForSeconds(3);
        yield return StartCoroutine(FadeCanvas(fade, 1f, 0f, 0.1f));

        SetInputEnabled(true);
        Debug.Log($"Teleported to: {target.name}");
    }

    private IEnumerator FadeCanvas(CanvasGroup cg, float startAlpha, float endAlpha, float duration)
    {
        float time = 0f;
        cg.alpha = startAlpha;
        cg.blocksRaycasts = true;
        cg.interactable = true;

        while (time < duration)
        {
            time += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, endAlpha, time / duration);
            yield return null;
        }

        cg.alpha = endAlpha;
        if (endAlpha == 0f)
        {
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }
    }

    // ---------------- Collisions & Triggers ---------------- //

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Fire") && timer is > 295 and < 300)
            hp--;

        if (other.CompareTag("Water") && timer is > 295 and < 300)
            rust += 0.5f;
    }

    // ---------------- Utility ---------------- //

    public void AddCurrency(int amount) => currency += amount;
    public void AddCarry(int amount) => carriedFuel += amount;

    private void DeathCheck()
    {
        if (hp <= 0)
        {
            Debug.Log("Player is dead.");
        }
    }
}
