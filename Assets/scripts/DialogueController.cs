using UnityEngine;
using TMPro;

public class DialogueController : MonoBehaviour
{
    public TextMeshProUGUI dialogueText;
    public NPCDialogue1 NPC;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
        public void onClick()
        {
            //handle all dialogue
            dialogueText.text = NPC.idleDialogueLines[0];
        }
}
