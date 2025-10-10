using UnityEngine;

public class SimpleDoor : MonoBehaviour
{
    public GameObject Door;
    private bool isOpen;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isOpen = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            if(isOpen)
            {
                Door.transform.localEulerAngles += new Vector3(0,90,0); 
                isOpen = !isOpen;
            }
            else 
            {
                Door.transform.localEulerAngles += new Vector3(0,-90,0); 
                isOpen = !isOpen;
            }
            
        }
    }
}
