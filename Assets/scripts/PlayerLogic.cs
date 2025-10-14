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

    public GameObject fToTalkButton;
    public GameObject dialoguePanel;

    public bool endDialogue;

    public string typeInteract;

    private StarterAssetsInputs _input;
    private PlayerInput _playerInput;

    public Ray ray;
    public RaycastHit hit;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _interactTimeoutDelta = InteractTimeout;
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
                //if the collider is an NPC
                Collider collider;
                collider = hit.collider; 
                if(collider.GetComponent<DialogueController>())
                {
                    typeInteract = "Dialogue";
                    fToTalkButton.SetActive(true);
                
                }
            }
            else
            {
                fToTalkButton.SetActive(false);
                dialoguePanel.SetActive(false);
            }
            if(!hit.collider.CompareTag("NPC"))
            {
                fToTalkButton.SetActive(false);
                dialoguePanel.SetActive(false);
            }
        }
    }
    public void Interact()
    {
        if(typeInteract == "Dialogue" && endDialogue == false)
        {
            fToTalkButton.SetActive(false);
            dialoguePanel.SetActive(true);
            GetComponent<FirstPersonController>().MoveSpeed = 0f;
            _input.cursorInputForLook = false;
            hit.collider.gameObject.GetComponent<DialogueController>().onClick();
            Debug.Log("This happened");
            endDialogue = true;
        }
        else if(typeInteract == "Dialogue" && endDialogue == true)
        {
            GetComponent<FirstPersonController>().MoveSpeed = 4f;
            _input.cursorInputForLook = true;
            fToTalkButton.SetActive(false);
            dialoguePanel.SetActive(false);
            Debug.Log("This happenbed too");
            endDialogue = false;

        }


    }
    

}
}
