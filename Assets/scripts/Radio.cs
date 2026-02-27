using UnityEngine;

public class Radio : MonoBehaviour
{
    public AudioClip[] audioClips;
    public AudioSource audioSource;
    private int count;

    void Start()
    {
        count = 0;
        audioSource = GetComponent<AudioSource>();
    }
    public void InteractWithRadio()
    {
        audioSource.Stop();
        if(count >= audioClips.Length-1)
        {
            count = 0;
        }
        else{
           count ++; 
        }
        audioSource.PlayOneShot(audioClips[count]);
        
        
        
    }
}
