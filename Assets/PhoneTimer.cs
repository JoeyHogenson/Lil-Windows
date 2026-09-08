using UnityEngine;
using PixelCrushers.DialogueSystem;

public class PhoneTimer : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void StartPhoneTimer()
    {
        Invoke("TurnPhoneOff", 90f);
    }
    public void TurnPhoneOff()
    {
        PixelCrushers.DialogueSystem.DialogueManager.StopConversation();
    }
}
