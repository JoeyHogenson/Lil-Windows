using UnityEngine;

public class Artwork : MonoBehaviour
{
    public GameObject pens;
    public GameObject drawableObject;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void EnableArtwork()
    {
       pens.SetActive(true);
       drawableObject.SetActive(true); 
    }
}
