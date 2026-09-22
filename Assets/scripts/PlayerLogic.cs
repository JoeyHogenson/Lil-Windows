using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using TMPro;

namespace StarterAssets
{
public class PlayerLogic : MonoBehaviour
{
    public int socialCred;
    public int troubleMeter;
    public int mentalHealth;

    public GameObject DialogueManager;

    public GameObject eToOpenButton;
    public GameObject eToCloseButton;
    public GameObject eToGrabButton;
    public GameObject menuPanel;
    public GameObject[] CanvasObjects;
    public GameObject saveSlots;
    public GameObject loadSlots;
    public int count;

    public GameObject blueImage;

    public bool startDialogue;

    private bool canvasCleared;

    public string typeInteract;

    private StarterAssetsInputs _input;
    private PlayerInput _playerInput;

    new Collider collider;

    public string currentEvent;

    public Ray ray;
    public RaycastHit hit;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.visible = false;
        //setting _input variable to starter assets script to get inputs
        startDialogue = true;
        _input = GetComponent<StarterAssetsInputs>();
        _playerInput = GetComponent<PlayerInput>();
        count = 0;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _input.cursorInputForLook = true;
        _input.cursorLocked = true;

    }

    private void OnEnable()
    {
        PixelCrushers.DialogueSystem.DialogueManager.instance.conversationStarted += OnConversationStarted;
        PixelCrushers.DialogueSystem.DialogueManager.instance.conversationEnded += OnConversationEnded;
    }

    private void OnDisable()
    {
        if(PixelCrushers.DialogueSystem.DialogueManager.hasInstance)
        {
            PixelCrushers.DialogueSystem.DialogueManager.instance.conversationStarted -= OnConversationStarted;
            PixelCrushers.DialogueSystem.DialogueManager.instance.conversationEnded -= OnConversationEnded;
        }
    }

    //driven off the Dialogue System's own C# events instead of per-conversation Inspector
    //UnityEvents (OnConversationStart/End), since not every conversation/ending (e.g. walking
    //away, some dialogue options) has those wired up to call LockCursor()/UnlockCursor()
    private void OnConversationStarted(Transform actor)
    {
        _input.cursorInputForLook = false;
    }

    //fires regardless of how the conversation ended (completed, StopConversation(), interrupted
    //by another conversation, etc.) so look input can't get stuck disabled from the dialogue.
    //Update() derives the actual OS cursor state from cursorInputForLook every frame
    private void OnConversationEnded(Transform actor)
    {
        if(!menuPanel.activeSelf)
        {
            _input.cursorInputForLook = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        //reasserted every frame instead of only on menu toggle: in the Editor,
        //pressing Escape while locked triggers Unity's own auto-unlock, which
        //races with our toggle and otherwise leaves the cursor stuck until
        //something (e.g. alt-tab) forces Windows to resync it.
        //driven off cursorInputForLook (not menuPanel/dialogue state directly) since
        //that's the flag every UI in the scene already toggles via LockCursor()/
        //UnlockCursor() - menu, dialogue responses, the newspaper, etc. - so any of
        //them reliably gets a visible cursor instead of only the ones special-cased here
        if(!_input.cursorInputForLook)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        //shoot raycast every frame
        //Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
        //handles when raycast hits something
        if(Physics.Raycast(ray, out hit))
        {
            if(hit.distance <= 15f)
            {
                
                collider = hit.collider; 
                if(collider.GetComponent<InteractTele>())
                {
                    typeInteract = "Tele";
                    eToOpenButton.SetActive(true);
                }
                else if(collider.GetComponent<SimpleDoor>())
                {
                    if(!collider.GetComponent<SimpleDoor>().isOpen)
                    {
                        typeInteract = "Door";
                        eToOpenButton.SetActive(true);
                    }
                    else
                    {
                        typeInteract = "Door";
                        eToCloseButton.SetActive(true);
                    }

                }
                else
                {
                    typeInteract = "None";
                    eToCloseButton.SetActive(false);
                    eToOpenButton.SetActive(false);
                    
                }
            }
            else
            {
                typeInteract = "None";
                eToCloseButton.SetActive(false);
                eToOpenButton.SetActive(false);
            }
        }
        
    }
    public void Option1()
    {
        collider.GetComponent<DialogueController>().nextLineOption1();
    }
    /*
    public void InitiateDialougue()
    {
        dialoguePanel.SetActive(true);
        GetComponent<FirstPersonController>().MoveSpeed = 0f;
        _input.cursorInputForLook = false;
        hit.collider.gameObject.GetComponent<DialogueController>().onClick();
    }
    */
    public void Interact()
    {
        //handles starting dialogue when you press "E". Reads off of the NPC dialogue scriptable object
        if(typeInteract == "Door" && !hit.collider.GetComponent<SimpleDoor>().isOpen)
        {
            hit.collider.GetComponent<SimpleDoor>().Open();
            eToOpenButton.SetActive(false);
        }
        else if(typeInteract == "Door" && hit.collider.GetComponent<SimpleDoor>().isOpen)
        {
            hit.collider.GetComponent<SimpleDoor>().Close();
            eToCloseButton.SetActive(false);
        }
        else if(typeInteract == "grabbable")
        {
            eToGrabButton.SetActive(true);
        }
        else if(typeInteract == "Tele")
        {
                //teleport player to target location
                transform.position = hit.collider.GetComponentInParent<InteractTele>().targetLocation.transform.position;
        }
    }
    public void Menu()
    {
        //Escape closes whatever's on top (a newspaper) instead of opening the pause
        //menu underneath it - otherwise Escape only reset look/cursor state and left
        //the newspaper on screen
        if(Newspaper.CurrentlyOpen != null)
        {
            Newspaper.CurrentlyOpen.ShutNewspaper();
            return;
        }
        PixelCrushers.DialogueSystem.DialogueManager.StopConversation();
        _input.cursorInputForLook = true;
        blueImage.SetActive(false);
        if(menuPanel.activeSelf == false && canvasCleared == true)
        {
            menuPanel.SetActive(true);
            _input.cursorInputForLook = false;
        }
        else if(menuPanel.activeSelf == true)
        {
            menuPanel.SetActive(false);
            _input.cursorInputForLook = true;
        }
        //ClearCanvas();

    }
    public void LockCursor()
    {
         _input.cursorInputForLook = false;
    }
    public void UnlockCursor()
    {
         _input.cursorInputForLook = true;
    }
    public void ClearCanvas()
    {
        for(int i = 0; i < CanvasObjects.Length; i++)
        {
            CanvasObjects[i].SetActive(false);
            canvasCleared = true;
        }
    }
    
    
}
}

