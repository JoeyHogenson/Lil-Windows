using UnityEngine;
using TMPro;
using System;
using System.IO;

public class TransDialogue : MonoBehaviour
{
    public TextMeshProUGUI dialogueText;
    public int count;
    public NPCDialogue1 NPC;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        NPC.idleDialogueLines = File.ReadAllLines(Path.Combine(Application.streamingAssetsPath, "TransDialogue.txt"));
        /*
        for(int i = 0; i < NPC.idleDialogueLines.Length; i++)
        {
            Debug.Log(NPC.idleDialogueLines[i][0]);
        }
        */
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void onClick()
    {
        //handle all dialogue
        
    }

    public void nextLine()
    {
        if(count < NPC.idleDialogueLines.Length)
        {
            dialogueText.text = NPC.idleDialogueLines[count];
            count++;
        }
        
    }
    public void IdleDialogue()
    {
            
    }
}
