
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System;
using System.IO;

namespace StarterAssets
{


public class DialogueController : MonoBehaviour
{
    public TextMeshProUGUI dialogueText;
    public int count;
    public NPCDialogue1 NPC;
    public string Character;
    public GameObject PlayerLogic;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(Character == "NPC")
        {
            NPC.idleDialogueLines = File.ReadAllLines(Path.Combine(Application.streamingAssetsPath, "IdleDialogue.txt"));
        }
        else if(Character == "Elder")
        {
            NPC.idleDialogueLines = File.ReadAllLines(Path.Combine(Application.streamingAssetsPath, "ElderDialogue.txt"));
        }
        else if(Character == "Riley")
        {
            NPC.idleDialogueLines = File.ReadAllLines(Path.Combine(Application.streamingAssetsPath, "RileyDialogue.txt"));
        }
        else if(Character == "DC")
        {
            NPC.idleDialogueLines = File.ReadAllLines(Path.Combine(Application.streamingAssetsPath, "DCDialogue.txt"));
        }
        
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
    public void nextLineOption1()
    {
        dialogueText.text = NPC.idleDialogueLines[count];
        count++;
    }
    public void Goodbye()
    {
        PlayerLogic.GetComponent<PlayerLogic>().Menu();
    }
    public void IdleDialogue()
    {
            
    }
}
}
