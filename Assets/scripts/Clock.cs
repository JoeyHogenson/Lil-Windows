using UnityEngine;

/// <summary>Rotates the clock hand one full turn over each in-game day.</summary>
public class Clock : MonoBehaviour
{
    public Transform clock;
    public GameObject gameController;
    private GameController controller;

    void Start()
    {
        controller = gameController.GetComponent<GameController>();
    }

    void Update()
    {
        //holds at 12 o'clock until the day starts (day 1 waits for intake to finish)
        float progress = 0f;
        if(controller.IsDayStarted)
        {
            float dayLength = controller.end - controller.DayStartTime;
            progress = dayLength > 0f ? Mathf.Clamp01((Time.time - controller.DayStartTime) / dayLength) : 0f;
        }
        clock.rotation = Quaternion.Euler(0, 0, progress * -360f);
    }
}
