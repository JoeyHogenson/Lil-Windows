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
    }

    // Update is called once per frame
    void Update()
    {
        if(gameController.dayCount != displayedDay)
        {
            displayedDay = gameController.dayCount;
            calendarText.text = displayedDay.ToString();
        }
    }
}
