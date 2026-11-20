using UnityEngine;

namespace PixelCrushers.DialogueSystem.SequencerCommands
{
    /// <summary>
    /// HandOver(item, receiver): the player hands <c>item</c> (e.g. Medicine) to <c>receiver</c> (default: the
    /// listener). Both hands meet between them and the item changes hands; the command ends when the arm is back.
    /// If the player isn't holding it, it's put in their hand first.
    /// Example, on the player's "Here are your pills" line: HandOver(Medicine, listener)
    /// </summary>
    public class SequencerCommandHandOver : SequencerCommand
    {
        void Start()
        {
            string item = GetParameter(0);
            Transform receiver = GetSubject(1, listener);
            FirstPersonHands hands = FirstPersonHands.Instance;
            if(hands == null || receiver == null)
            {
                Stop();
                return;
            }
            if(!string.IsNullOrEmpty(item) && hands.HeldItemName != item) hands.Hold(item);
            hands.HandOver(receiver, Stop);
        }
    }

}
