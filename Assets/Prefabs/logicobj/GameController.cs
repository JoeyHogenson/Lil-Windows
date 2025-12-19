using UnityEngine;
using TMPro;
using Unity.VisualScripting;

public class GameController : MonoBehaviour
{
    public float daystart;
    public float[] events = {0.0f};
    public Collider countzone;

    public GameObject[] Characters;
    public GameObject player;
    private float mail;
    private float commisary;
    private float end;
    private float count;

    public TextMeshProUGUI eventText;
    public GameObject eventTextObject;

    public GameObject mailObject;

    public bool isCommissary;
    public bool isCount;
    ///game days should be 10min
    void Update()
    {
        CheckEvents();
    }
    void Start()
    {
        StartDay();
    }
    void Awake()
    {

    }
    void StartDay()
    {

        //called only when the player goes to bed, sets time for next morning, and events for the next day. 
        daystart = Time.time;
        SetEvents();
    }
    void CheckEvents()
    {
        if(Time.time <= count -5 && Time.time >=count)
        {
            CountEvent();
        }
        if (Time.time == count)
        {
            CountDeadline();
        }
        if(Time.time>=commisary && isCommissary == false)
        {
            CommisaryEvent();
        }
        if(Time.time ==mail)
        {
            MailEvent();
        }
        if(Time.time>= end)
        {
            EndOfDayEvent();
        }
    }
    void SetEvents()
    {
        ///sets events to occur over the next ten minutes.
        SetCount();
        SetMail();
        SetCommisary();
        SetEnd();
    }
    void CountDeadline()
    {
        CountZoneCheck();
        eventTextObject.SetActive(true);
        //check if player is in cell at door for count
        if (isCount == true)
        {
            //proceed with count
            eventText.text = "Count complete";
            eventTextObject.SetActive(true);
            //freeze text and after 5 seconds hide 
            Invoke("HideEventText", 5f);

        }
        else
        {
            //fail count, get punished
            eventText.text = "You failed count and have been punished";
            eventTextObject.SetActive(true);
        }
        

    }
    void CountEvent()
    {
     
        ///the player is forced to be in the 
        Characters[0].GetComponent<NPCController>().NPCspeed = 0.03f;
        Characters[0].GetComponent<Animator>().SetBool("isWalking", true);
        eventText.text = "Count is occuring";
        eventTextObject.SetActive(true);



    }

    //script that checks if player is in the count zone

    void CountZoneCheck()
    {
                if (countzone.bounds.Contains(player.transform.position))
        {
            isCount = true;
        }
        else
        {
            isCount = false;
        }
    }

    void CommisaryEvent()
    {
        eventText.text = "Get to the commissary for meal time";
        eventTextObject.SetActive(true);
        isCommissary = true;
        //path NPC characters to Commissary
    }
    void MailEvent()
    {
        mailObject.SetActive(true);
    }
    void EndOfDayEvent()
    {
        //teleport characters back to starting position
        for(int i = 0; i< Characters.Length; i++)
        {
            Characters[i].transform.position = Characters[i].GetComponent<NPCController>().startingPosition;
            Characters[i].GetComponent<NPCController>().NPCspeed = 0;
        }

        StartDay();

    }
    
    void SetMail()
    {
        mail = daystart + 60;
    }
    void SetCount()
    {
        count = daystart+10;
    }
    void SetCommisary()
    {
        commisary = daystart +20;
        isCommissary = false;
    }
    void SetEnd()
    {
        end = daystart+600;
    }
    
}

