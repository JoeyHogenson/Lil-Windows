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
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if(Physics.Raycast(ray, out hit))
        {
            if(hit.distance <= 2f)
            {
                Collider collider;
                collider = hit.collider; 
                if(collider.GetComponent<DialogueController>())
                {

                    fToTalkButton.SetActive(true);
                    if(_input.interact)
                    {
                        fToTalkButton.SetActive(false);
                        dialoguePanel.SetActive(true);
                        hit.collider.gameObject.GetComponent<DialogueController>().onClick();
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
