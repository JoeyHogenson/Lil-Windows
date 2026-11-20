using System;

/// <summary>Who the player is for this run. Add a value here (and a flag below) to add a playthrough.</summary>
public enum PlayableCharacter
{
    DC = 0,
    Riley = 1,
}

/// <summary>A set of <see cref="PlayableCharacter"/>s, for content that belongs to more than one playthrough.</summary>
[Flags]
public enum PlayableCharacterMask
{
    None = 0,
    DC = 1 << PlayableCharacter.DC,
    Riley = 1 << PlayableCharacter.Riley,
}

public static class PlayableCharacterExtensions
{
    public static bool Includes(this PlayableCharacterMask mask, PlayableCharacter character)
    {
        return ((int)mask & (1 << (int)character)) != 0;
    }
}
