using UnityEngine;

/// <summary>
/// Marks a GameObject as belonging to certain playthroughs. Once the player picks a character,
/// <see cref="PlaythroughDirector"/> turns this object on for the listed characters and off for everyone else.
/// Put it on DC-only NPCs (Chico), Riley-only systems (the segregation systems), intro overlays, etc.
/// Objects without it are shared by every playthrough.
/// </summary>
[DisallowMultipleComponent]
public class PlaythroughOnly : MonoBehaviour
{
    [Tooltip("The playthroughs this object appears in.")]
    public PlayableCharacterMask characters = PlayableCharacterMask.DC;

    public bool AppearsFor(PlayableCharacter character)
    {
        return characters.Includes(character);
    }
}
