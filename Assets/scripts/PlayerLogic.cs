using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace StarterAssets
{
       public class PlayerLogic : MonoBehaviour
    {
        public int socialCred;
        public int troubleMeter;
        public int mentalHealth;

        public GameObject eToTalkButton;
        public GameObject eToOpenButton;
        public GameObject eToCloseButton;
        public GameObject eToGrabButton;
        public GameObject menuPanel;

        public GameObject[] Newspaper;
        public int count;

        public bool isManualOpen;
        private string[] ManualText =
        {
            "Page 0 text",
            "Welcome to Lil' Windows! \n\nYour Current Quests are: Talk to OG about needing medication\n\nExplore media materials in law library",
            "Come back when you have completed your quests",
            "","","","","","","","","","","","","","","","","","","","","","","","","","","","",""
        };

        public TextMeshProUGUI leftPage;
        public TextMeshProUGUI rightPage;

        private int leftCount;
        public TextMeshProUGUI leftPageNumber;
        private int rightCount;
        public TextMeshProUGUI rightPageNumber;

        public bool startDialogue;

        private enum InteractType { None, Dialogue, Tele, Door, Newspaper, Grabbable }
        private InteractType currentInteract = InteractType.None;

        private StarterAssetsInputs _input;
        private PlayerInput _playerInput;
        private FirstPersonController _firstPersonController;
        private Camera _mainCamera;

        private Collider currentCollider;

        public string currentEvent;

        private Ray ray;
        private RaycastHit hit;

        private const float InteractionMaxDistance = 15f;

        void Awake()
        {
            _input = GetComponent<StarterAssetsInputs>();
            _playerInput = GetComponent<PlayerInput>();
            _firstPersonController = GetComponent<FirstPersonController>();
        }

        void Start()
        {
            startDialogue = true;
            isManualOpen = false;
            count = 0;
            leftCount = 1;
            rightCount = 2;

            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogWarning("Main Camera not found. Raycasts will use Camera.main dynamically.");
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (_input != null)
            {
                _input.cursorInputForLook = true;
                _input.cursorLocked = true;
            }

            Debug.Log($"ManualText length: {ManualText.Length}");
        }

        void Update()
        {
            DoInteractionRaycast();
        }

        private void DoInteractionRaycast()
        {
            // Choose cached camera if available
            var cam = _mainCamera ?? Camera.main;
            if (cam == null) return;

            ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));
            if (Physics.Raycast(ray, out hit, InteractionMaxDistance))
            {
                currentCollider = hit.collider;
                UpdateInteractStateFromCollider(currentCollider);
            }
            else
            {
                currentCollider = null;
                SetInteractNone();
            }

            // If the thing hit isn't an NPC, ensure talk button is hidden
            if (currentCollider == null || !currentCollider.CompareTag("NPC"))
            {
                eToTalkButton?.SetActive(false);
            }
        }

        private void UpdateInteractStateFromCollider(Collider col)
        {
            // Hide default open/close until determined
            eToOpenButton?.SetActive(false);
            eToCloseButton?.SetActive(false);
            eToGrabButton?.SetActive(false);

            if (col == null)
            {
                SetInteractNone();
                return;
            }

            // Dialogue
            if (col.TryGetComponent<Dialogue>(out _))
            {
                currentInteract = InteractType.Dialogue;
                eToTalkButton?.SetActive(true);
                return;
            }

            // Teleport interaction
            if (col.TryGetComponent<InteractTele>(out _))
            {
                currentInteract = InteractType.Tele;
                eToOpenButton?.SetActive(true);
                return;
            }

            // Door
            if (col.TryGetComponent<SimpleDoor>(out var door))
            {
                currentInteract = InteractType.Door;
                if (!door.isOpen) eToOpenButton?.SetActive(true);
                else eToCloseButton?.SetActive(true);
                return;
            }

            // Newspaper by tag
            if (col.CompareTag("Newspaper"))
            {
                currentInteract = InteractType.Newspaper;
                ShowNextNewspaper();
                return;
            }

            // Grabbable - tag or component can be adapted
            if (col.CompareTag("Grabbable") || col.TryGetComponent<Rigidbody>(out _))
            {
                currentInteract = InteractType.Grabbable;
                eToGrabButton?.SetActive(true);
                return;
            }

            SetInteractNone();
        }

        private void SetInteractNone()
        {
            currentInteract = InteractType.None;
            eToCloseButton?.SetActive(false);
            eToOpenButton?.SetActive(false);
            eToGrabButton?.SetActive(false);
        }

        public void Option1()
        {
            if (currentCollider == null) return;
            if (currentCollider.TryGetComponent<DialogueController>(out var dc))
            {
                dc.nextLineOption1();
            }
        }

        public void Interact()
        {
            if (currentCollider == null) return;

            switch (currentInteract)
            {
                case InteractType.Dialogue:
                    HandleDialogueInteract();
                    break;
                case InteractType.Door:
                    HandleDoorInteract();
                    break;
                case InteractType.Tele:
                    HandleTeleInteract();
                    break;
                case InteractType.Grabbable:
                    eToGrabButton?.SetActive(true);
                    break;
                case InteractType.Newspaper:
                    // Newspaper display handled when raycast discovered it.
                    break;
                case InteractType.None:
                default:
                    break;
            }
        }

        private void HandleDialogueInteract()
        {
            if (!startDialogue)
            {
                if (currentCollider.TryGetComponent<DialogueController>(out var dc))
                {
                    dc.nextLine();
                }
                return;
            }

            // Start dialogue
            eToTalkButton?.SetActive(false);

            if (currentCollider.TryGetComponent<Animator>(out var animator))
            {
                animator.SetBool("isWalking", false);
                animator.SetBool("sitTalkRight", true);
            }

            if (currentCollider.TryGetComponent<NPCController>(out var npc))
            {
                npc.NPCspeed = 0f;
            }

            if (_firstPersonController != null) _firstPersonController.MoveSpeed = 0f;
            if (_input != null)
            {
                _input.cursorInputForLook = false;
                _input.cursorLocked = true;
            }

            startDialogue = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void HandleDoorInteract()
        {
            if (currentCollider.TryGetComponent<SimpleDoor>(out var door))
            {
                if (!door.isOpen)
                {
                    door.Open();
                    eToOpenButton?.SetActive(false);
                }
                else
                {
                    door.Close();
                    eToCloseButton?.SetActive(false);
                }
            }
        }

        private void HandleTeleInteract()
        {
            if (currentCollider.TryGetComponent<InteractTele>(out var tele) && tele.targetLocation != null)
            {
                transform.position = tele.targetLocation.transform.position;
            }
        }

        public void Menu()
        {
            // If in dialogue, end dialogue and restore controls
            if (currentInteract == InteractType.Dialogue && !startDialogue)
            {
                if (_firstPersonController != null) _firstPersonController.MoveSpeed = 10f;
                if (_input != null)
                {
                    _input.cursorInputForLook = true;
                    _input.cursorLocked = true;
                }

                eToTalkButton?.SetActive(false);
                startDialogue = true;
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                return;
            }

            // If a newspaper is active, hide the last opened one
            if (count > 0 && count <= Newspaper.Length && Newspaper[count - 1].activeSelf)
            {
                Newspaper[count - 1].SetActive(false);
                return;
            }

            // Toggle menu panel
            if (menuPanel == null) return;

            var isActive = menuPanel.activeSelf;
            menuPanel.SetActive(!isActive);

            if (_firstPersonController != null)
            {
                _firstPersonController.MoveSpeed = isActive ? 10f : 0f;
            }

            if (_input != null)
            {
                _input.cursorInputForLook = isActive;
            }
        }

        private void ShowNextNewspaper()
        {
            if (Newspaper == null || Newspaper.Length == 0) return;

            // Ensure count is within bounds
            if (count < 0) count = 0;
            if (count >= Newspaper.Length) count = Newspaper.Length - 1;

            // Activate the current newspaper if not already active
            if (!Newspaper[count].activeSelf)
            {
                Newspaper[count].SetActive(true);
            }

            // increment for next time, but clamp
            count = Mathf.Clamp(count + 1, 0, Newspaper.Length);
        }
    }
}
