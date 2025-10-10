using UnityEngine;

public class GameController
{
    public float daystart =0.0f;
    public float[] events = {0.0f};
    private float mail;
    private float commisary;
    private float end;
    ///game days should be 10min
    void Update(){

    }
    void Start(){

    }
    void Awake(){

    }
    void StartDay(){

        //called only when the player goes to bed, sets time for next morning, and events for the next day. 
        daystart = Time.time();
        SetEvents();
    }
    void CheckEvents(){
        if(Time.time>=commisary){
            CommisaryEvent();
        }
        if(Time.time>=mail){
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
    void Count(){
        ///the player is forced to be in the 
    }
    void SetMail(){
        mail = daystart + 60;
    }
    void SetCount(){
        count = daystart+10;
    }
    void SetCommisary(){
        commisary = daystart +20
    }
    void SetEnd(){
        End = daystart+600
    }
}

