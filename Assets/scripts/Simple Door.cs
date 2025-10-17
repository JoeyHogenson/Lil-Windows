using UnityEngine;

public class SimpleDoor : MonoBehaviour
{
    public GameObject Door;
    public bool isOpen;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isOpen = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void Open()
    {
        Door.transform.localEulerAngles += new Vector3(0,-90,0);   
        isOpen = !isOpen; 
    }
    public void Close()
    {
        Door.transform.localEulerAngles += new Vector3(0,90,0); 
        isOpen = !isOpen;
    }
}
