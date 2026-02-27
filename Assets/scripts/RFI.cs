using UnityEngine;

public class RFI : MonoBehaviour
{
    public GameObject lawLibraryCheck;
    public GameObject mailCheck;
    public GameObject VisitationCheck;
    public GameObject commissaryCheck;
    public GameObject LawLibraryDoor;
    public GameObject commissaryDoor;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void Submit()
    {
        if(lawLibraryCheck)
        {
            //access to the law library
            LawLibraryDoor.transform.localRotation = Quaternion.Euler(-90f,0,180f);
        }
        if(commissaryCheck)
        {
            commissaryDoor.transform.localRotation = Quaternion.Euler(-90f,0,180f);
        }
    }
}
