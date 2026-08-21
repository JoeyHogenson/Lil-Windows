using UnityEngine;
using PixelCrushers.DialogueSystem;
using System.Collections;
using TMPro;
public class RFI : MonoBehaviour
{
    private bool visitedLibrary;

    public GameObject blackSquare;

    public Transform lawLibrary;
    public Transform commissary;
    public Transform segPosition;

    public GameObject player;
    public GameObject eventTextObject;
    public TextMeshProUGUI eventText;
    public GameObject lawLibraryCheck;
    public GameObject propertyCheck;
    public GameObject VisitationCheck;
    public GameObject commissaryCheck;
    public GameObject housingCheck;
    public GameObject lawLibraryButton;
    public GameObject property;
    public GameObject Visitation;
    public GameObject commissaryButton;
    public GameObject housing;
    public GameObject LawLibraryDoor;
    public GameObject commissaryDoor;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    /*public void ClearAllButtons()
    {
        for(int i = 0; i < checkmarks.Length; i++)
        {
            checkmarks[i].SetActive(false);
        }
        
    }*/
    public void Submit()
    {
        if(lawLibraryCheck.activeSelf && visitedLibrary == false)
        {
            visitedLibrary = true;
            //LawLibraryDoor.transform.localRotation = Quaternion.Euler(-90f,0,180f);
            eventText.text = "The law library is now open. Go there to request access for special commissary. Click 'E' on the door to teleport back";
            Invoke("RemoveText",5f);
            Invoke("TeleportLawLibrary",5f);
            
        }
        else if(commissaryCheck.activeSelf)
        {
            Invoke("TeleportCommissary",5f);
        }
    }
    public void RemoveText()
    {
        eventTextObject.SetActive(false);
    }
    public void TeleportLawLibrary()
    {
        player.SetActive(false);
        player.transform.position = lawLibrary.position;
        player.SetActive(true);
    }
    public void TeleportCommissary()
    {
        player.SetActive(false);
        player.transform.position = commissary.position;
        player.SetActive(true);
        Invoke("TeleportToSeg",60f);
    }
    public void TeleportToSeg()
    {
        player.SetActive(false);
        player.transform.position = segPosition.position;
        player.SetActive(true);
    }

}
