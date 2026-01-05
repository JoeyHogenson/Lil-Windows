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

    public GameObject eToTalkButton;
    public GameObject eToOpenButton;
    public GameObject eToCloseButton;
    public GameObject eToGrabButton;
    public GameObject menuPanel;

    public GameObject[] Newspaper;
    public int count;

    public bool isManualOpen;
    private string[] ManualText = 
    {"Page 0 text",
    "Welcome to Lil' Windows! \n\nYour Current Quests are: Talk to OG about needing medication\n\nExplore media materials in law library",
    "Come back when you have completed your quests",
    "","","","","","","","","","","","","","","","","","","","","","","","","","","","","","",""}; 

    public TextMeshProUGUI leftPage;
    public TextMeshProUGUI rightPage;

    private int leftCount;
    public TextMeshProUGUI leftPageNumber;
    private int rightCount;
    public TextMeshProUGUI rightPageNumber;

    public bool startDialogue;

    public string typeInteract;

    private StarterAssetsInputs _input;
    private PlayerInput _playerInput;

    Collider collider;

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
        isManualOpen = false;
        count = 0;
        leftCount = 1;
        rightCount = 2;
        Debug.Log(ManualText.Length);
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
                if(collider.GetComponent<Dialogue>())
                {
                    typeInteract = "Dialogue";
                }
                else if(collider.GetComponent<InteractTele>())
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
                else if (collider.CompareTag("Newspaper"))
                {
                    typeInteract = "Newspaper";
                    Newspaper[count].SetActive(true);
                    count++;
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
            if(!hit.collider.CompareTag("NPC"))
            {
                eToTalkButton.SetActive(false);
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
        if(typeInteract == "Dialogue" && startDialogue == true)
        {
            eToTalkButton.SetActive(false);         
            hit.collider.GetComponent<Animator>().SetBool("isWalking", false);
            hit.collider.GetComponent<Animator>().SetBool("sitTalkRight", true);
            hit.collider.GetComponent<NPCController>().NPCspeed = 0f;
            //GetComponent<FirstPersonController>().MoveSpeed = 0f;
            //_input.cursorInputForLook = false;
            _input.cursorLocked = true;
            startDialogue = false;
            Cursor.visible = true;
            // Unlock the cursor so it can move freely
            Cursor.lockState = CursorLockMode.None; 
        }
        else if(typeInteract == "Dialogue" && startDialogue == false)
        {
            hit.collider.gameObject.GetComponent<DialogueController>().nextLine();
        }
        else if(typeInteract == "Door" && !hit.collider.GetComponent<SimpleDoor>().isOpen)
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
        //Ends dialogue; handles if esc is pressed when dialogue is active 
        if(typeInteract == "Dialogue")
        {
            GetComponent<FirstPersonController>().MoveSpeed = 10f;
            //hit.collider.GetComponent<NPCController>().NPCspeed = 0.03f;
            //hit.collider.GetComponent<Animator>().SetBool("isWalking", true);
            _input.cursorInputForLook = true;
            _input.cursorLocked = true;
            eToTalkButton.SetActive(false);
            startDialogue = true;
             Cursor.visible = false;
            // Unlock the cursor so it can move freely
            Cursor.lockState = CursorLockMode.Locked; 
        }
        else if(Newspaper[count].activeSelf == true)
        {
            Newspaper[count].SetActive(false);

        }
        //Brings up Menu
        else if(menuPanel.activeSelf == false)
        {
            menuPanel.SetActive(true);
            GetComponent<FirstPersonController>().MoveSpeed = 0f;
            _input.cursorInputForLook = false;
        }
        else if(menuPanel.activeSelf == true)
        {
            menuPanel.SetActive(false);
            GetComponent<FirstPersonController>().MoveSpeed = 10f;
            _input.cursorInputForLook = true;
        }
    }
    
    

}
}
