using UnityEngine;

[CreateAssetMenu(fileName = "NPCDialogue1", menuName = "Scriptable Objects/NPCDialogue1")]
public class NPCDialogue1 : ScriptableObject
{
    public string[] idleDialogueLines; //lines of dialogue when idle
    public string[] commissaryDialogueLines; // lines of dialogue in the commissary

    public string[] lockdownDialogueLines; //lines of dialogue when in lockdown
    public string[] electionDialogueLines; //lines of dialogue when in election
    public string[] bookDialogueLines; //lines of dialogue when in book ban
    public void Awake()
    {
        //load all lines of dialogue here
    }
}

