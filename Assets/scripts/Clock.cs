using UnityEngine;
using System.Collections;

public class Clock : MonoBehaviour
{
    public Transform clock;
    public GameObject gameController;
    private float daystart;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
      daystart = Time.time;  
    }

    // Update is called once per frame
    void Update()
    {
        clock.rotation = Quaternion.Euler((daystart/gameController.GetComponent<GameController>().end)*-360,0,0);
        //-1*(daystart/gameController.GetComponent<GameController>().end)
    }
}
