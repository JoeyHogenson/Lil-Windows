using UnityEngine;
using System.Collections;
public class Sitting : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        
        if(other.CompareTag("NPC"))
        {
            Debug.Log("should be sitting now");
            other.GetComponent<Animator>().SetBool("isSitting", true);
            other.GetComponent<NPCController>().NPCspeed = 0f;
            other.GetComponent<Transform>().Rotate(0f,90f,0f);
            
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
}
