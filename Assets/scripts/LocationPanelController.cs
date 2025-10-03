using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LocationPanelController : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Update logic if needed
// Example: Check if the player's location is within the panel's range
        if (IsPlayerWithinPanelRange())
        {
            // Update the panel's display based on the player's location
            UpdatePanelDisplay();
        }
    }
    private bool IsPlayerWithinPanelRange()
    {
        // Logic to determine if the player is within the panel's range
        // This is a placeholder; implement your own logic
        return true;
    }
    private void UpdatePanelDisplay()
    {
        // Logic to update the panel's display based on the player's location
        // This is a placeholder; implement your own logic
      //  Debug.Log("Updating Location Panel Display");
    }
    public void OpenLocationPanel()
    {
        // Logic to open the location panel
        Debug.Log("Location Panel Opened");
    }
    public void CloseLocationPanel()
    {
        
        Debug.Log("Location Panel Closed");
    }
    public void setActive()
    {
        // Logic to set the location panel active
        gameObject.SetActive(true);
        Debug.Log("Location Panel Set Active");
    }
    
}
