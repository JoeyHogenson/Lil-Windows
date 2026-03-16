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

    // Update is called once per frame
    void Update()
    {
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
        PixelCrushers.DialogueSystem.DialogueManager.StopConversation();
        _input.cursorInputForLook = true;
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

