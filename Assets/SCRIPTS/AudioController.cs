using UnityEngine;
using JSAM;

public class AudioController : MonoBehaviour
{
    public void UpdateMoveAudio(CombatMove move, float normalizedTime)
    {
        if (move.audioEvents == null || move.audioEvents.Length == 0) return;

        for (int i = 0; i < move.audioEvents.Length; i++)
        {
            // If the animation has reached the 'slider' time and hasn't played yet
            if (normalizedTime >= move.audioEvents[i].triggerTime && !move.audioEvents[i].hasPlayed)
            {
                if (move.audioEvents[i].sound != null)
                {
                    // JSAM plays the sound (randomizing internally if set up in Library)
                    AudioManager.PlaySound(move.audioEvents[i].sound, transform.position);
                    move.audioEvents[i].hasPlayed = true;
                }
            }
        }
    }

    // Call this at the start of every new attack
    public void ResetAudio(CombatMove move)
    {
        if (move.audioEvents == null) return;
        for (int i = 0; i < move.audioEvents.Length; i++)
        {
            move.audioEvents[i].hasPlayed = false;
        }
    }
}