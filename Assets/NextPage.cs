using UnityEngine;

public class NextPage : MonoBehaviour
{
    public GameObject[] pages;
    public int pageNumber;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pageNumber = 0;
    }
    public void GoToNextPage()
    {
        pages[pageNumber].SetActive(false);
        pages[pageNumber+1].SetActive(true);
        pageNumber++;

        Debug.Log("Next page Clicked");
        
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
