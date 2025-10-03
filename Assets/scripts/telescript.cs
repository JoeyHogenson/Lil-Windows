using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class telescript : MonoBehaviour
{
    private MovementController player;  // Reference to the player
    public GameObject teleout;
    // Start is called before the first frame update
    void Start()
    {
        player = GameObject.FindObjectOfType<MovementController>();
        if (player == null)
        {
            Debug.LogError("Player not found!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (teleout != null)
            {
                Debug.Log("Player entered");
                player.TeleportTo(teleout);
            }
            else
            {
                Debug.LogError("No exit port");
            }
        }
    }
    public void dosomeeffects()
    {
    }
}