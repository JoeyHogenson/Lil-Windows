using UnityEngine;

/// <summary>
/// Put on a door's collider (next to its SimpleDoor): pressing E on it sends the player back to their cell
/// instead of opening the door. PlayerLogic does the interaction.
/// </summary>
public class ReturnToCellDoor : MonoBehaviour
{
    [Tooltip("Where the player lands. Left empty, the GameController's player starting position (the cell) is used.")]
    public Transform cell;

    /// <summary>The cell to send the player to, or null if neither this nor the GameController has one.</summary>
    public Transform Destination
    {
        get
        {
            if(cell == null)
            {
                GameController game = FindFirstObjectByType<GameController>();
                if(game != null) cell = game.PlayerStartingPosition;
            }
            return cell;
        }
    }
}
