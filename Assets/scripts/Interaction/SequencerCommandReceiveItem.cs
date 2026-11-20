using UnityEngine;

namespace PixelCrushers.DialogueSystem.SequencerCommands
{
    /// <summary>
    /// ReceiveItem(item, giver): <c>giver</c> (default: the speaker) hands the player <c>item</c>; it stays in the
    /// player's hand until handed over or dropped.
    /// Example, on the nurse's "Here is the medicine" line: ReceiveItem(Medicine, speaker)
    /// </summary>
    public class SequencerCommandReceiveItem : SequencerCommand
    {
        void Start()
        {
            string item = GetParameter(0);
            Transform giver = GetSubject(1, speaker);
            FirstPersonHands hands = FirstPersonHands.Instance;
            if(hands == null || string.IsNullOrEmpty(item))
            {
                Stop();
                return;
            }
            hands.ReceiveFrom(item, giver, Stop);
        }
    }
}
