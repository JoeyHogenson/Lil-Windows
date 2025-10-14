using UnityEngine;
using UnityEngine.InputSystem;
namespace StarterAssets
{
public class PlayerLogic : MonoBehaviour
{
    public int socialCred;
    public int troubleMeter;
    public int mentalHealth;

    public GameObject fToTalkButton;
    public GameObject dialoguePanel;

    private StarterAssetsInputs _input;
    private PlayerInput _playerInput;
   

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _input = GetComponent<StarterAssetsInputs>();
        _playerInput = GetComponent<PlayerInput>();
    }

    // Update is called once per frame
    void Update()
    {
        //shoot raycast every frame
        //Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
        RaycastHit hit;
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

                    fToTalkButton.SetActive(true);
                    if(_input.interact)
                    {
                        if(_input.cursorInputForLook)
                        {
                        fToTalkButton.SetActive(false);
                        dialoguePanel.SetActive(true);
                        _input.cursorInputForLook = false;
                        GetComponent<FirstPersonController>().MoveSpeed = 0f;
                        hit.collider.gameObject.GetComponent<DialogueController>().onClick();
                        }
                        else 
                        {
                            _input.cursorInputForLook = true;
                             GetComponent<FirstPersonController>().MoveSpeed = 4f;
                        }
                        
                    }
                
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
}
}
