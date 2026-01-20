using UnityEngine;
using System.Collections;

public class Cutscenecontroller : MonoBehaviour
{
    public Animator animator;

    public GameObject DC;
    public GameObject door;

    private bool moveDC;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(TurnRightWait());
        
    }

    // Update is called once per frame
    void Update()
    {
        if(moveDC == true)
        {
            DC.transform.position += new Vector3(0.01f,0,0);
        }
    }
    IEnumerator TurnRightWait()
    {
        yield return new WaitForSeconds(20.5f);
        animator.SetBool("rightTurn",true);
        animator.SetBool("headShake",false);
        StartCoroutine(StartWalking());

    }
    IEnumerator StartWalking()
    {
        yield return new WaitForSeconds(1f);
        DC.transform.Rotate(0,90,0);
        animator.SetBool("rightTurn",false);
        animator.SetBool("startWalking",true);
        Debug.Log("This happened");
        moveDC = true;
        door.transform.Rotate(0,0,-90);
        
    }

}
