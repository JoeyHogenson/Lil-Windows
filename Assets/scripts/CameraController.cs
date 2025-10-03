using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float smoothSpeed = 0.125f;  // The smoothness of camera movement. Adjust as needed.
    public Vector3 offset;              // The offset from the player's position.

    private Transform player;           // Reference to the player's Transform.

    private void Start()
    {
        // Find and cache the player's Transform. You can also assign this in the Inspector.
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    private void LateUpdate()
    {
        if (player == null)
            return;

        // Calculate the desired position for the camera.
        Vector3 desiredPosition = player.position + offset;

        // Use SmoothDamp to smoothly move the camera to the desired position.
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
        transform.rotation = player.rotation;
    
    }
}