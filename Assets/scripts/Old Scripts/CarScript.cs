using UnityEngine;
using UnityEngine.UI;

public class CarScript : MonoBehaviour, IInteractable
{
    public MovementController player;
    public MovementController rider;

    public GameObject spawn;
    public HUDManager hudManager;
    public Button[] travelButtons;

    private int selectedButtonIndex;
    private bool travelUIActive = false;

    void Start()
    {
        hudManager = FindObjectOfType<HUDManager>();
        player = FindObjectOfType<MovementController>();

        hudManager.killTravel();
        selectedButtonIndex = 0;

        travelButtons = hudManager.travelButtons;

        for (int i = 0; i < travelButtons.Length; i++)
        {
            int index = i;
            travelButtons[i].onClick.AddListener(() => OnTravelButtonClick(index));
        }

        SetSelectedButton(selectedButtonIndex);
    }

    void Update()
    {
        RiderTP();

        // Escape closes UI
        if (travelUIActive && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseTravelUI();
        }
       
    }
    
    void RiderTP()
    {
        // Reserved for future use
    }

    void OnTravelButtonClick(int index)
    {
        Debug.Log("Travel button clicked: " + index);
        // Implement travel logic here
    }

    void SetSelectedButton(int index)
    {
        for (int i = 0; i < travelButtons.Length; i++)
        {
            travelButtons[i].interactable = true;
        }

        if (index >= 0 && index < travelButtons.Length)
        {
            travelButtons[index].interactable = false;
        }
        else
        {
            Debug.LogError("Invalid selected button index: " + index);
        }
        
    }

    public void Interact()
    {
        Debug.Log("Car interaction triggered");

        if (hudManager == null || player == null)
        {
            Debug.LogError("HUDManager or Player not assigned.");
            return;
        }

        hudManager.drawTravel();
        travelUIActive = true;

        foreach (var btn in travelButtons)
        {
            btn.gameObject.SetActive(true);
            btn.interactable = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        player.SetInputEnabled(false); // Freeze look + movement

        Debug.Log("Travel menu active, input frozen, cursor unlocked");
    }

    public void CloseTravelUI()
    {
        if (!travelUIActive) return;

        hudManager.killTravel();
        travelUIActive = false;

        foreach (var btn in travelButtons)
        {
            btn.gameObject.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        player.SetInputEnabled(true); // Restore look + movement

        Debug.Log("Travel menu closed, input restored, cursor locked");
    }

    public void SetRider(MovementController fare)
    {
        rider = fare;
    }

    // Cleaned up mouse-based prompt logic to remove flicker/leak
    void OnMouseOver()
    {
        if (travelUIActive) return;

        if (Vector3.Distance(player.transform.position, transform.position) <= 3f)
        {
            hudManager.ShowInteractPrompt();
        }
        else
        {
            hudManager.HideInteractPrompt();
        }
    }

    void OnMouseExit()
    {
        if (!travelUIActive)
        {
            hudManager.HideInteractPrompt();
        }
    }
}
