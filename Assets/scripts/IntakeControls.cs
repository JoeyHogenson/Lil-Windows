using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
public class IntakeControls : MonoBehaviour
{
    //Frozen in place for the duration of the audio
    //until you get the number
    //fade to black
    public GameObject GameController;
    public GameObject blackSquare;
    public GameObject player;
    public Transform startingPosition;

    public AudioSource audioObject;
    public AudioClip audioClip1;
    public AudioClip audioClip2;

    //fade into cell
    void Start()
    {
        StartCoroutine(WaitForFirstAudio(23));
    }

    void Update()
    {
        
    }
    IEnumerator WaitForFirstAudio(float firstLength)
    {
        yield return new WaitForSeconds(firstLength);
        audioObject.PlayOneShot(audioClip2); 
        StartCoroutine(WaitForSecondAudio(22));       
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

        GameController.SetActive(true);

    }
}
