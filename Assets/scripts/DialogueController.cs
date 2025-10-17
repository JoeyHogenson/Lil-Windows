using UnityEngine;
using TMPro;

public class DialogueController : MonoBehaviour
{
    public TextMeshProUGUI dialogueText;
    public int count;
    public NPCDialogue1 NPC;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        count = 0;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
        public void onClick()
        {
            //handle all dialogue
            dialogueText.text = NPC.idleDialogueLines[count];
            count++;
        }

    public void nextLine()
    {
        if(count < NPC.idleDialogueLines.Length)
        {
            dialogueText.text = NPC.idleDialogueLines[count];
            count++;
        }
        
    }
}
