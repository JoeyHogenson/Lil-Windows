using UnityEngine;
using PixelCrushers.DialogueSystem;
using System.Collections;
using TMPro;
public class RFI : MonoBehaviour
{
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

    //this script sits on the form object itself (RFI Form, Indigent Form), so the form is open
    //exactly while this component is enabled; tracked so Escape (PlayerLogic.Menu) can close it
    public static RFI CurrentlyOpen;

    //the box the player checked last; any other box that becomes checked replaces it
    private GameObject selectedCheck;

    private float originalMoveSpeed;
    private float originalSprintSpeed;

    void OnEnable()
    {
        CurrentlyOpen = this;
        LockMovement(true);
    }

    //each box is a pair wired in the scene: clicking the empty box hides it and shows its X, clicking the X
    //does the reverse. LateUpdate runs after the UI click, so a newly checked box clears the others the same frame
    void LateUpdate()
    {
        GameObject[] checks = { lawLibraryCheck, propertyCheck, VisitationCheck, commissaryCheck, housingCheck };
        GameObject[] boxes = { lawLibraryButton, property, Visitation, commissaryButton, housing };

        GameObject newlyChecked = null;
        for(int i = 0; i < checks.Length; i++)
        {
            if(checks[i] != null && checks[i].activeSelf && checks[i] != selectedCheck)
            {
                newlyChecked = checks[i];
            }
        }
        if(newlyChecked == null) return;

        selectedCheck = newlyChecked;
        for(int i = 0; i < checks.Length; i++)
        {
            if(checks[i] == null || checks[i] == selectedCheck) continue;
            checks[i].SetActive(false);
            if(boxes[i] != null) boxes[i].SetActive(true);
        }
    }

    void OnDisable()
    {
        if(CurrentlyOpen == this)
        {
            CurrentlyOpen = null;
        }
        LockMovement(false);
    }

    //freezes walking/sprinting while the form is open (same approach as Newspaper); the speeds are saved
    //when it opens and put back when it closes, whether by Submit, Escape or a UnityEvent
    private void LockMovement(bool locked)
    {
        if(player == null) return;
        StarterAssets.FirstPersonController controller = player.GetComponent<StarterAssets.FirstPersonController>();
        if(controller == null) return;

        if(locked)
        {
            originalMoveSpeed = controller.MoveSpeed;
            originalSprintSpeed = controller.SprintSpeed;
            controller.MoveSpeed = 0f;
            controller.SprintSpeed = 0f;
        }
        else
        {
            controller.MoveSpeed = originalMoveSpeed;
            controller.SprintSpeed = originalSprintSpeed;
        }
    }

    //closes the form without submitting it; the checkmarks stay as they were for next time
    public void Close()
    {
        gameObject.SetActive(false);
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
        if(lawLibraryCheck.activeSelf)
        {
            //LawLibraryDoor.transform.localRotation = Quaternion.Euler(-90f,0,180f);
            TeleportAfterDelay("Law Library", lawLibrary);
        }
        else if(commissaryCheck.activeSelf)
        {
            TeleportAfterDelay("Commissary", commissary);
        }
        QuestLog.SetQuestState("Submit an RFI","success");

    }

    //the Submit button hides this form before calling Submit, and Invoke/coroutines don't run on an
    //inactive object, so the countdown runs on the player instead (it stays active)
    private void TeleportAfterDelay(string locationName, Transform destination)
    {
        eventText.text = "You will be teleported to the " + locationName + " in 5 seconds. Press 'E' on the door to teleport back to your cell.";
        //GameController hides this at the start of each day, so it has to be shown again here
        eventTextObject.SetActive(true);
        player.GetComponent<MonoBehaviour>().StartCoroutine(TeleportRoutine(destination));
    }

    private IEnumerator TeleportRoutine(Transform destination)
    {
        yield return new WaitForSeconds(5f);
        RemoveText();
        MovePlayer(destination);
    }

    public void RemoveText()
    {
        eventTextObject.SetActive(false);
    }
    public void TeleportLawLibrary()
    {
        MovePlayer(lawLibrary);
    }
    public void TeleportCommissary()
    {
        MovePlayer(commissary);
    }
    public void TeleportToSeg()
    {
        MovePlayer(segPosition);
    }

    //the CharacterController overwrites transform.position while enabled, so toggle it off around the move
    //(not player.SetActive, which would also stop the coroutine running on the player)
    private void MovePlayer(Transform destination)
    {
        if(destination == null)
        {
            Debug.LogWarning("[RFI] Cannot teleport player: destination not assigned.");
            return;
        }
        CharacterController controller = player.GetComponent<CharacterController>();
        if(controller != null) controller.enabled = false;
        player.transform.position = destination.position;
        if(controller != null) controller.enabled = true;
    }

}
