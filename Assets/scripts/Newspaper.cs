using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using TMPro;
namespace StarterAssets
{

public class Newspaper : MonoBehaviour
{
    public GameObject player;
    public GameObject[] newspaper = new GameObject[5];
    private int count;
    private int finalPage;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        count = 0;
        finalPage = newspaper.Length;
        Debug.Log(finalPage);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void NextPage()
    {
        if(count < finalPage - 1)
        {
            count++;
            newspaper[count].SetActive(true);
            newspaper[count - 1].SetActive(false);
            
        }
        if(count > finalPage)
        {
            for(int i = 0; i < finalPage; i++)
            {
                newspaper[i].SetActive(false);
                player.GetComponent<StarterAssetsInputs>().cursorInputForLook = true;
            }
        }
    }
    public void PrevPage()
    {
        if(count > 0)
        {
            count = count-1;
            newspaper[count].SetActive(true);
            newspaper[count + 1].SetActive(false);
            
        }
        else if(count == 0)
        {
            for(int i = 0; i < finalPage; i++)
            {
                newspaper[i].SetActive(false);
                player.GetComponent<StarterAssetsInputs>().cursorInputForLook = true;
            }
        }
    }
}
}
