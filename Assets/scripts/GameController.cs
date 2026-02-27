using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;
using PixelCrushers.DialogueSystem;
public class GameController : MonoBehaviour
{
    public float daystart;
    public float[] events = {0.0f};
    public Collider countzone;

    public GameObject[] Characters;
    public GameObject player;
    public Transform playerStartingPosition;

    public Animator lockdownPrisonerAnimator;

    private float mail;
    private float commisary;
    public float end;
    private float count;
    private float thoughts;
    private float bookBan;
    private float lockdown;

    public int dayCount;

    public GameObject blackSquare;

    public GameObject thoughtsObject;
    public TextMeshProUGUI ThoughtsText;

    public TextMeshProUGUI eventText;
    public GameObject eventTextObject;

    public GameObject bookBanNewspaper;
    public GameObject intakeNewspaper;
    //public GameObject lockdownNewspaper;
    public GameObject[] Books;

    public GameObject mailObject;

    public bool isCommissary;
    public bool isCount;
    public bool isLockdown;

    public bool daystarted;

    // runtime flags so each event fires once per day
    private bool _countEventFired;
    private bool _countDeadlineFired;
    private bool _mailFired;
    private bool _commisaryFired;
    private bool _endFired;
    private bool _thoughtsFired;
    private bool _bookBanFired;
    private bool _lockdownFired;

    ///game days should be 10min
    void Update()
    {
        if(daystarted == true)
        {
            CheckEvents();
        }
        
    }
    void Start()
    {
        if(dayCount != 1)
        {
            StartDay();
        }
    }
    public void StartDay()
    {
        // called only when the player goes to bed, sets time for next morning, and events for the next day. 
        daystart = Time.time;
        ResetDayFlags();
        SetEvents();

        if (eventTextObject != null) eventTextObject.SetActive(false);
        if (mailObject != null) mailObject.SetActive(false);
        blackSquare.SetActive(false);
        daystarted = true;

        Debug.Log("[GameController] StartDay: daystart=" + daystart + " count=" + count);

    }

    void ResetDayFlags()
    {
        _countEventFired = false;
        _countDeadlineFired = false;
        _mailFired = false;
        _commisaryFired = false;
        _endFired = false;
        _thoughtsFired = false;
        _bookBanFired = false;
        _lockdownFired = false;
        isCommissary = false;
        isCount = false;
        DialogueLua.SetVariable("bookBan", false);
        DialogueLua.SetVariable("lockdown", false);
    }

    void CheckEvents()
    {
        // Count "incoming" window: trigger once when Time.time enters the 5s before deadline
        if (!_countEventFired && Time.time >= count - 5f && Time.time < count)
        {
            CountEvent();
            _countEventFired = true;
        }

        // Count deadline: trigger once when time reaches or passes count
        if (!_countDeadlineFired && Time.time >= count)
        {
            CountDeadline();
            _countDeadlineFired = true;
        }

        // Commissary event
        if (!_commisaryFired && Time.time >= commisary)
        {
            CommisaryEvent();
            _commisaryFired = true;
        }

        // Mail event
        if (!_mailFired && Time.time >= mail)
        {
            MailEvent();
            _mailFired = true;
        }

        // End of day
        if (!_endFired && Time.time >= end)
        {
            EndOfDayEvent();
            _endFired = true;
        }
        if (!_thoughtsFired && Time.time >= thoughts)
        {
            Thoughts();
            _thoughtsFired = true;
        }
        if(!_bookBanFired&&Time.time>=bookBan && (dayCount == 2|| dayCount == 4))
        {
            //BookBan();
            _bookBanFired = true;
        }
        if(!_lockdownFired && Time.time >= lockdown && dayCount == 2)
        {
            Lockdown();
            _lockdownFired = true;
        }
    }
    void SetEvents()
    {
        ///sets events to occur over the next ten minutes.
        SetCount();
        SetMail();
        SetCommisary();
        SetEnd();
        SetThoughts();
        SetBookBan();
        SetLockdown();
    }
    void CountDeadline()
    {
        CountZoneCheck();

            if (eventTextObject != null) eventTextObject.SetActive(true);

        //check if player is in cell at door for count
        if (isCount == true)
        {
            //proceed with count
            if (eventText != null) eventText.text = "Count complete";
            Debug.Log("[GameController] CountDeadline: player IN zone -> Count complete");
            //freeze text and after 5 seconds hide 
            Invoke(nameof(HideEventText), 5f);
        }
        else
        {
            //fail count, get punished
            if (eventText != null) eventText.text = "You failed count and have been punished";
            Debug.Log("[GameController] CountDeadline: player NOT in zone -> Failed count");
            Invoke(nameof(HideEventText), 5f);
        }
    }
    void CountEvent()
    {
        ///the player is forced to be in the 
        if (Characters != null && Characters.Length > 0 && Characters[0] != null)
        {
            var npc = Characters[0].GetComponent<NPCController>();
            if (npc != null) npc.NPCspeed = 0.03f;

            var anim = Characters[0].GetComponent<Animator>();
            if (anim != null) anim.SetBool("isWalking", true);
        }

        if (eventText != null) eventText.text = "Count is occurring. Stand at your cell door.";
        if (eventTextObject != null) eventTextObject.SetActive(true);

        Debug.Log("[GameController] CountEvent triggered at " + Time.time);
        StartCoroutine(StopWalkingAfterSeconds());
    }

    //script that checks if player is in the count zone
    void CountZoneCheck()
    {
        if (countzone == null || player == null)
        {
            isCount = false;
            Debug.LogWarning("[GameController] CountZoneCheck: countzone or player not assigned.");
            return;
        }

        // Bounds.Contains uses world-space bounds; ensure player's pivot is compared (may need offset)
        isCount = countzone.bounds.Contains(player.transform.position);
        Debug.Log("[GameController] CountZoneCheck: player position=" + player.transform.position + " countzone.bounds=" + countzone.bounds + " isCount=" + isCount);
    }

    void CommisaryEvent()
    {
        //if (eventText != null) eventText.text = "Get to the commissary for meal time";
        if (eventTextObject != null) eventTextObject.SetActive(true);
        isCommissary = true;
        Debug.Log("[GameController] CommisaryEvent triggered at " + Time.time);
        //path NPC characters to Commissary
    }
    void MailEvent()
    {
        if (mailObject != null) mailObject.SetActive(true);
        Debug.Log("[GameController] MailEvent triggered at " + Time.time);
    }
    void EndOfDayEvent()
    {
        eventTextObject.SetActive(true);
        eventText.text = "The end of the day is nearing. Get to your cell or you will be teleported there.";
        //teleport characters back to starting position
        Debug.Log("[GameController] EndOfDayEvent triggered at " + Time.time);
        StartCoroutine(FadeToBlack());
        dayCount++;
    }
    void BookBan()
    {
        if(dayCount == 2)
        {    
            bookBanNewspaper.SetActive(true);
            //lockdownNewspaper.SetActive(false);
            intakeNewspaper.SetActive(false);
            DialogueLua.SetVariable("bookBan", true);
            eventTextObject.SetActive(true);
            eventText.text = "A book ban has now been instated";
            for(int i = 0; i < Books.Length; i++)
            {
                Books[i].SetActive(false);
            }
            StartCoroutine(HideTextAfterSeconds());
        }
        else if(dayCount == 3)
        {
            eventTextObject.SetActive(true);
            bookBanNewspaper.SetActive(false);
            eventText.text = "A book ban has now been removed";
            DialogueLua.SetVariable("bookBan", false);
            StartCoroutine(HideTextAfterSeconds());
            for(int i = 0; i < Books.Length; i++)
            {
                Books[i].SetActive(true);
            }
        }
        

    }
    void Thoughts()
    {
        //thoughtsObject.SetActive(true);
        //ThoughtsText.text = "You think about your mother... and vow to see her again.";
        StartCoroutine(HideTextAfterSeconds());

    }
    void Lockdown()
    {
        isLockdown = true;
        //lockdownNewspaper.SetActive(true);
        bookBanNewspaper.SetActive(false);
        intakeNewspaper.SetActive(false);
        DialogueLua.SetVariable("lockdown", true);
        lockdownPrisonerAnimator.SetBool("getUp", true);
        lockdownPrisonerAnimator.SetBool("isSitting", false);
        eventTextObject.SetActive(true);
        //eventText.text = "There is now a lockdown. Get to your cell immediately.";
        StartCoroutine(HideTextAfterSeconds());
    }
    void SetMail()
    {
        mail = daystart + 60;
    }
    void SetCount()
    {
        count = daystart + 10;
    }
    void SetCommisary()
    {
        commisary = daystart + 180;
        isCommissary = false;
    }
    void SetEnd()
    {
        end = daystart + 600;
    }
    void SetThoughts()
    {
        thoughts = daystart + 20;
    }
    void SetBookBan()
    {
        bookBan = daystart + 20;
    }
    void SetLockdown()
    {
        lockdown = daystart + 20;
    }
    public void LoadScene()
    {
        SceneManager.LoadScene("New Jail Scene");
    }
    public void Quit()
    {
        Application.Quit();
    }

    void HideEventText()
    {
        if (eventTextObject != null) eventTextObject.SetActive(false);
    }
    IEnumerator StopWalkingAfterSeconds()
    {
        yield return new WaitForSeconds(8f);
        var anim = Characters[0].GetComponent<Animator>();
            if (anim != null) anim.SetBool("isWalking", false);
        var npc = Characters[0].GetComponent<NPCController>();
            if (npc != null) npc.NPCspeed = 0f;
        Characters[0].transform.Rotate(0f,180f,0f);
        

    }
    IEnumerator FadeToBlack()
    {
        yield return new WaitForSeconds(10f);
        eventTextObject.SetActive(false);
        blackSquare.SetActive(true);
        for (int i = 0; i < Characters.Length; i++)
        {
            var npc = Characters[i].GetComponent<NPCController>();
            if (npc != null)
            {
                Characters[i].transform.position = npc.startingPosition;
                npc.NPCspeed = 0;
            }
        }
        player.SetActive(false);
        player.transform.position = playerStartingPosition.position;
        player.SetActive(true);
        StartDay();
    }
    IEnumerator HideTextAfterSeconds()
    {
        yield return new WaitForSeconds(5f);
        thoughtsObject.SetActive(false);
    }
    IEnumerator HideEventTextAfterSeconds()
    {
        yield return new WaitForSeconds(5f);
        eventTextObject.SetActive(false);
    }
    
}

