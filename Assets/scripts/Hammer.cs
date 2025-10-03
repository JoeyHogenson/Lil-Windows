using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeaponInterfaces;

public class Hammer : MonoBehaviour, IMeleeWeapon
{
    public float attackCooldown = 1.0f; // Cooldown time between hammer swings
    private float lastAttackTime = 0.0f;
    public float Range = 2.0f;
    public int Damage = 10;
    private Animator animator;
    public SphereCollider hitbox;
    float IMeleeWeapon.Range => Range;

    int IWeapon.Damage => Damage;


    void Start()
    {
        animator = GetComponent<Animator>();
     
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void Attack()
    {

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            // Play the attack animation
            animator.Play("swing");
            lastAttackTime = Time.time; // Reset the last attack time
        }
    }


}