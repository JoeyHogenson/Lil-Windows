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
            StartCoroutine(WaitForSecondAudio(74));
            if(didFirstPlay == false)
            {
                
            //player.GetComponent<FirstPersonController>().MoveSpeed = 0;
            //player.GetComponent<FirstPersonController>().SprintSpeed = 0;
            //player.GetComponent<StarterAssetsInputs>().cursorLocked = false;
            audioObject.PlayOneShot(audioClip2);
            didFirstPlay = true;
            Debug.Log("I did this");
            }
        }
        
        
    }

    void Update()
    {
        
    }
    /*public void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player") && didAudioPlay == false)
        {
            audioObject.PlayOneShot(audioClip2); 
            moveText.SetActive(false);
            StartCoroutine(WaitForSecondAudio(22));
            didAudioPlay = true;
        }
    }*/
    IEnumerator WaitForFirstAudio(float firstLength)
    {
        yield return new WaitForSeconds(firstLength);
        moveText.SetActive(true);
        player.GetComponent<FirstPersonController>().MoveSpeed = 10;
        player.GetComponent<FirstPersonController>().SprintSpeed = 12;
        player.GetComponent<StarterAssetsInputs>().cursorLocked = true;
               
    }
    IEnumerator WaitForSecondAudio(float audioLength)
    {
        yield return new WaitForSeconds(audioLength);
        player.SetActive(false);
        player.transform.position = startingPosition.position;
        player.transform.rotation = startingPosition.rotation;
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
