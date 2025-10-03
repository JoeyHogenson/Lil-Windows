using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.UIElements;
using UnityEngine;

public class rockcontroller : MonoBehaviour
{
    public Rigidbody rb;
    public Collider me;
    public string tool;
    public int hits;
    public int hp;
    public int currencyValue = 1;  // Amount of currency this rock is worth
    private MovementController player;  // Reference to the player
    public AudioSource audioSource;       // Rock hit sound
    public ParticleSystem hitParticles;   // Dust/spark effect

    // Start is called before the first frame update
    void Start()
    {

        rb = GetComponent<Rigidbody>();
        rb.linearDamping = Random.Range(0.01f, 0.05f);
        transform.localScale = transform.localScale* rb.linearDamping*10;
        hits = 0;
        player = GameObject.FindObjectOfType<MovementController>();  // Find the player in the scene
    }

    // Update is called once per frame
    void Update()
    {
        if (hits >= hp)
        {
            player.AddCurrency(1);  // Give the player currency
            Destroy(transform.parent.gameObject);
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(tool) == true)
        {
            
            if (audioSource != null)
            {
                audioSource.pitch = Random.Range(0.5f, .6f); // add some variation
                AudioSource.PlayClipAtPoint(audioSource.clip, transform.position, audioSource.volume);
            }
            hits++;
        }
    }
}