using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class slimecontroller : MonoBehaviour
{
    public float distanceToPlayer;
    public Animator animator;
    public GameObject player;
    float rotationSpeed = 20f; // Speed at which the object rotates towards the player
    public float detectionDistance = 10f; // Distance at which the object starts rotating towards the player
    System.Random rand;
    public GameObject childModel;

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
        player = GameObject.FindGameObjectWithTag("Player");
        rand = new System.Random(3);
    }

    // Update is called once per frame
    void Update()
    {
        findplayer();
   
    }

    private void move()
    {
        animator.Play("move");
    }

    private void findplayer()
    {
         distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

        //if player is within certain distance turn to it, otherwise, one in 4 chance it moves to a random location 
        if (distanceToPlayer <= 30)
        {
            turntoplayer();
        }
        else if (rand.Next(0,2000)==0)
        {
            move();
        }
    }
    void turntoplayer()
    {
        Vector3 target = player.transform.position;

        // Calculate the direction from the current object to the target
        Vector3 directionToTarget = target - transform.position;

        // Calculate the angle between the forward vector of the current object and the direction to the target
        float angle = Vector3.Angle(transform.forward, directionToTarget);
        float thresholdAngle = 10f;
        if (angle < thresholdAngle)
        {

            animator.Play("turn");
        }
    }
}
