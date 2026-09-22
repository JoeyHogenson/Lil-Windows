using UnityEngine;
using TMPro;

public class CalendarScript : MonoBehaviour
{
    public GameController gameController;
    public TMP_Text calendarText;

    private int displayedDay = -1;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(calendarText == null)
        {
            calendarText = GetComponentInChildren<TMP_Text>();
        }
        Debug.Log("[CalendarScript] Start on " + gameObject.name + ": gameController.dayCount=" + gameController.dayCount);
    }

    // Update is called once per frame
    void Update()
    {
        if(gameController.dayCount != displayedDay)
        {
            Debug.Log("[CalendarScript] " + gameObject.name + " displayed day changing " + displayedDay + " -> " + gameController.dayCount + " at Time.time=" + Time.time);
            displayedDay = gameController.dayCount;
            calendarText.text = displayedDay.ToString();
        }
    }
}
