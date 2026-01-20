using UnityEngine;

public class Radio : MonoBehaviour
{
    public AudioClip audioClip;
    AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }
}
