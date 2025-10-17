using UnityEngine;
using UnityEngine.InputSystem;
namespace StarterAssets
{
public class PlayerLogic : MonoBehaviour
{
    public int socialCred;
    public int troubleMeter;
    public int mentalHealth;

    public float InteractTimeout = 0.1f;
    private float _interactTimeoutDelta;

    public GameObject eToTalkButton;
    public GameObject eToOpenButton;
    public GameObject eToCloseButton;
    public GameObject dialoguePanel;
    public GameObject menuPanel;

    public bool startDialogue;

    public string typeInteract;

    private StarterAssetsInputs _input;
    private PlayerInput _playerInput;

    public Ray ray;
    public RaycastHit hit;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //setting _input variable to starter assets script to get inputs
        startDialogue = true;
        _input = GetComponent<StarterAssetsInputs>();
        _playerInput = GetComponent<PlayerInput>();
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
            if(hit.distance <= 5f)
            {
                //if the collider is an NPC enable "E to talk" button
                Collider collider;
                collider = hit.collider; 
                if(collider.GetComponent<DialogueController>())
                {
                    typeInteract = "Dialogue";
                    eToTalkButton.SetActive(true);
                
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
            if(!hit.collider.CompareTag("NPC"))
            {
                eToTalkButton.SetActive(false);
                dialoguePanel.SetActive(false);
            }
        }
        
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
            dialoguePanel.SetActive(true);
            GetComponent<FirstPersonController>().MoveSpeed = 0f;
            _input.cursorInputForLook = false;
            hit.collider.gameObject.GetComponent<DialogueController>().onClick();
            startDialogue = false;
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
        


    }
    public void Menu()
    {
        //Ends dialogue; handles if esc is pressed when dialogue is active 
        if(typeInteract == "Dialogue")
        {
            GetComponent<FirstPersonController>().MoveSpeed = 10f;
            _input.cursorInputForLook = true;
            eToTalkButton.SetActive(false);
            dialoguePanel.SetActive(false);
            startDialogue = true;
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
