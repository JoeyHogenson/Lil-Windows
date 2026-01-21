using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

namespace StarterAssets
{
public class IntakeControls : MonoBehaviour
{
    //Frozen in place for the duration of the audio
    //until you get the number
    //fade to black

    public GameObject GameController;
    public GameObject blackSquare;
    public GameObject player;
    public GameObject moveText;

    public GameObject Door;

    public Transform startingPosition;

    public AudioSource audioObject;
    public AudioClip audioClip1;
    public AudioClip audioClip2;

    private bool didAudioPlay;
    public bool didFirstPlay;

    //fade into cell
    void Start()
    {
        if(GameController.GetComponent<GameController>().dayCount == 1)
        {
            StartCoroutine(WaitForFirstAudio(23));
            if(didFirstPlay == false)
            {
                
            player.GetComponent<FirstPersonController>().MoveSpeed = 0;
            player.GetComponent<FirstPersonController>().SprintSpeed = 0;
            audioObject.PlayOneShot(audioClip1);
            didFirstPlay = true;
            Debug.Log("I did this");
            }
        }
        
        
    }

    void Update()
    {
        
    }
    public void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player") && didAudioPlay == false)
        {
            audioObject.PlayOneShot(audioClip2); 
            moveText.SetActive(false);
            StartCoroutine(WaitForSecondAudio(22));
            didAudioPlay = true;
        }
    }
    IEnumerator WaitForFirstAudio(float firstLength)
    {
        yield return new WaitForSeconds(firstLength);
        moveText.SetActive(true);
        player.GetComponent<FirstPersonController>().MoveSpeed = 10;
        player.GetComponent<FirstPersonController>().SprintSpeed = 12;
               
    }
    IEnumerator WaitForSecondAudio(float audioLength)
    {
        yield return new WaitForSeconds(audioLength);
        player.SetActive(false);
        player.transform.position = startingPosition.position;
        player.SetActive(true);
        blackSquare.SetActive(true);
        StartCoroutine(WaitForController(2));
        
        
    }
    IEnumerator WaitForController(float audioLengths)
    {
        yield return new WaitForSeconds(audioLengths);
        GameController.GetComponent<GameController>().StartDay();


    }
}
}
