using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class fakeload : MonoBehaviour
{    
    private Animator animator;
    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void fullload()
    {

    }
    void fadein() {
        animator.Play("fade in");
    }
    void fadeout() {
        animator.Play("fadeout");
    }  

}
