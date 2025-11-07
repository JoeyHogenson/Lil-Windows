using UnityEngine;
using TMPro;

public class GameController : MonoBehaviour
{
    public float daystart =0.0f;
    public float[] events = {0.0f};
    private float mail;
    private float commisary;
    private float end;
    private float count;
    public TextMeshProUGUI eventText;
    public GameObject eventTextObject;

    public bool isCommissary;
    ///game days should be 10min
    void Update()
    {
        CheckEvents();
    }
    void Start()
    {
        StartDay();
    }
    void Awake(){

    }
    void StartDay(){

        //called only when the player goes to bed, sets time for next morning, and events for the next day. 
        daystart = Time.time;
        SetEvents();
    }
    void CheckEvents(){
        if(Time.time>=commisary && isCommissary == false)
        {
            CommisaryEvent();
        }
        if(Time.time>=mail)
        {
            MailEvent();
        }
    }
    void SetEvents(){
        ///sets events to occur over the next ten minutes.
        SetCount();
        SetMail();
        SetCommisary();
        SetEnd();
    }
    void CountEvent(){
        ///the player is forced to be in the 
    }
    void SetMail(){
        mail = daystart + 60;
    }
    void SetCount(){
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
    void CommisaryEvent()
    {
        eventText.text = "Get to the commissary for meal time";
        eventTextObject.SetActive(true);
        isCommissary = true;
        Debug.Log("Henlog");
    }
    void MailEvent()
    {
        
    }
}

