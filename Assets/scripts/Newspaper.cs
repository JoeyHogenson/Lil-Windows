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

    public bool IsOpen { get { return Buttons != null && Buttons.activeSelf; } }

    //there are several newspaper props in the scene; this tracks whichever one is
    //currently open so Escape (PlayerLogic.Menu) can close it without needing a
    //reference to every individual newspaper
    public static Newspaper CurrentlyOpen;

    private float originalMoveSpeed;
    private float originalSprintSpeed;
    private bool wasOpen;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        count = 0;
        finalPage = newspaper.Length;
        FirstPersonController controller = player.GetComponent<FirstPersonController>();
        originalMoveSpeed = controller.MoveSpeed;
        originalSprintSpeed = controller.SprintSpeed;
    }
    // Update is called once per frame
    void Update()
    {
        //reasserted every frame (same pattern as PlayerLogic's cursor handling) so opening/
        //closing the newspaper reliably freezes/restores movement and cursor look, regardless
        //of whatever UnityEvent actually toggled Buttons/newspaper[] active
        bool isOpen = IsOpen;
        if(isOpen != wasOpen)
        {
            FirstPersonController controller = player.GetComponent<FirstPersonController>();
            controller.MoveSpeed = isOpen ? 0f : originalMoveSpeed;
            controller.SprintSpeed = isOpen ? 0f : originalSprintSpeed;
            player.GetComponent<StarterAssetsInputs>().cursorInputForLook = !isOpen;
            wasOpen = isOpen;
            CurrentlyOpen = isOpen ? this : null;
        }
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
