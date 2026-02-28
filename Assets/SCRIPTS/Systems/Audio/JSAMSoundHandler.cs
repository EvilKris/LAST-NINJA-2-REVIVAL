using JSAM;
using UnityEngine;

/// <summary>
/// Wrapper class for JSAM AudioManager functionality.
/// Provides convenient methods for playing sounds and music through Unity Events.
/// </summary>
public class JSAMSoundHandler : MonoBehaviour
{
    /// <summary>
    /// Plays a sound effect using JSAM AudioManager.
    /// Can be called from Unity Events.
    /// </summary>
    /// <param name="sfx">The SoundFileObject to play</param>
    public void PlayJSAMSound(SoundFileObject sfx)
    {
        AudioManager.PlaySound(sfx);
    }

    /// <summary>
    /// Plays a music track using JSAM AudioManager.
    /// Can be called from Unity Events.
    /// </summary>
    /// <param name="music">The MusicFileObject to play</param>
    public void PlayJSAMMusic(MusicFileObject music)
    {
        AudioManager.PlayMusic(music);
    }   

    /// <summary>
    /// Stops all currently playing sound effects.
    /// Can be called from Unity Events.
    /// </summary>
    public void StopJSAMSound()
    {
        AudioManager.StopAllSounds();
    }

    /// <summary>
    /// Stops a specific music track.
    /// Can be called from Unity Events.
    /// </summary>
    /// <param name="music">The MusicFileObject to stop</param>
    public void StopJSAMMusic(MusicFileObject music)
    {
        AudioManager.StopMusic(music);
    }       
}

