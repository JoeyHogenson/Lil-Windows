using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using TMPro;
namespace StarterAssets
{

public class Newspaper : MonoBehaviour
{
    public GameObject player;
    public GameObject[] newspaper;
    private int count;
    private int finalPage;

    public GameObject NewspaperController;

    public GameObject Buttons;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        count = 0;
        finalPage = newspaper.Length;
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
                Buttons.SetActive(false);
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
                Buttons.SetActive(false);
                count = 0;
                player.GetComponent<StarterAssetsInputs>().cursorInputForLook = true;
            }
        }
    }
    public void ShutNewspaper()
    {
        for(int i = 0; i < finalPage; i++)
        {
            newspaper[i].SetActive(false);
            Buttons.SetActive(false);
        }
    }
}
}
