
using UnityEngine;
using UnityEngine.UI;

public class HUDManager : MonoBehaviour
{
    public Canvas canvas;
    public Image segment1;
    public Image segment2;
    public Image segment3;
    public Image segment4;
    public Image segment5;
    public Image segment6;
    public Image segment7;
    public Image segment8;
    public Image genmenu;
    public Image depositbutton;
    public Image ignitebutton;
    public Text fuellevel;
    public Image[] bar;
    public CanvasGroup travel1;
    public Text carriedfuel;
    public Text ignition;
    public Text deposit;// Reference to the travel menu buttons
    public Button[] travelButtons;

    // Index to keep track of the currently selected button
    private int selectedButtonIndex;
    public int hp;
    public GameObject interact;

    public void Start()
    {
        killGen();
        killTravel();
        bar = new Image[8] { segment1, segment2, segment3, segment4, segment5, segment6, segment7, segment8 };
        canvas = FindObjectOfType<Canvas>();
        // Initialize the selected button index
        selectedButtonIndex = 0;

        // Add event listeners to the travel buttons
        for (int i = 0; i < travelButtons.Length; i++)
        {
            int index = i; // Capture the current index in a closure
            travelButtons[i].onClick.AddListener(() => OnTravelButtonClick(index));
        }

        // Set the initial button as selected
        SetSelectedButton(selectedButtonIndex);
    }

    public void UpdateHealthBar()
    {
        for (int i = 0; i < bar.Length; i++)
        {
            if (bar[i] != null)
            {
                bar[i].gameObject.SetActive(hp > i);
            }
        }
    }

    public void drawGen()
    {
        SetGenMenuVisibility(true);
    }

    public void killGen()
    {
        SetGenMenuVisibility(false);
    }

    private void SetGenMenuVisibility(bool isVisible)
    {
        genmenu.gameObject.SetActive(isVisible);
        depositbutton.gameObject.SetActive(isVisible);
        ignitebutton.gameObject.SetActive(isVisible);
        fuellevel.gameObject.SetActive(isVisible);
        carriedfuel.gameObject.SetActive(isVisible);
        ignition.gameObject.SetActive(isVisible);
        deposit.gameObject.SetActive(isVisible);
    }

    public void drawTravel()
    {
        if (travel1 != null)
        {
            travel1.gameObject.SetActive(true);
            travel1.enabled = true;
            travel1.blocksRaycasts = true;
            travel1.interactable = true;
            travel1.alpha = 1f; // Ensure the CanvasGroup is fully visible
           // Debug.Log("Travel menu should now be visible");
        }
        else
        {
            Debug.LogWarning("travel1 CanvasGroup is not assigned in HUDManager");
        }
    }

    public void killTravel()
    {
        if (travel1 != null)
        {
            travel1.gameObject.SetActive(false);
            travel1.enabled = false;
            travel1.blocksRaycasts = false;
            travel1.interactable = false;
            travel1.alpha = 0f; // Ensure the CanvasGroup is fully invisible
            //Debug.Log("Travel menu should now be hidden");
        }
        else
        {
            Debug.LogWarning("travel1 CanvasGroup is not assigned in HUDManager");
        }
    }

    public void HideInteractPrompt()
    {
        SetInteractPromptActive(false);
    }

    public void ShowInteractPrompt()
    {
        SetInteractPromptActive(true);
    }

    private void SetInteractPromptActive(bool isActive)
    {
        if (interact != null)
        {
            interact.SetActive(isActive);
            //Debug.Log(isActive ? "Showing Interact Prompt" : "Hiding Interact Prompt");
        }
        else
        {
            //Debug.LogWarning("interact GameObject is not assigned in HUDManager");
        }
    }
    void OnTravelButtonClick(int index)
    {
        // Handle button click event
        Debug.Log("Travel button clicked: " + index);
        // Add your travel logic here
    }

    void SetSelectedButton(int index)
    {
        // Deselect all buttons
        for (int i = 0; i < travelButtons.Length; i++)
        {
            travelButtons[i].interactable = true;
        }

        // Select the specified button
        travelButtons[index].interactable = false;

        // Perform any additional actions when a button is selected
        // For example, highlight the button or play a sound effect
    }
    public void Generator()
    {
        // Implement generator functionality here
    }

    public void UpdateScore()
    {
        // Implement score update functionality here
    }

    public void UpdateWeaponIcon()
    {
        // Implement weapon icon update functionality here
    }

    private void Update()
    {
        UpdateHealthBar();
    }
}