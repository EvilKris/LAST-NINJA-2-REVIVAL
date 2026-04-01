using UnityEngine;
using JSAM;
using System.Collections;

public class AreaSoundTrigger : MonoBehaviour
{
    //this is mostly for background sounds 
    public enum AreaZoneType { SFX_SWAMP, SFX_WATER, SFX_FOREST, MUSIC_SHRINE }

    [Header("Area Zone Type")]
    public AreaZoneType zoneType;

    [Tooltip("Layer to detect as player")]
    public LayerMask playerLayer = -1;

    private bool isPlayerInside = false;
    private SoundFileObject sfx;
    private MusicFileObject music;
    private MusicFileObject previousMusic;

    private SoundFileObject GetSoundForZone()
    {
        var bank = MasterSingleton.Instance.PrefabBankManager;
        switch (zoneType)
        {
            case AreaZoneType.SFX_SWAMP:
                return bank.AreaSoundSwamp;
            case AreaZoneType.SFX_WATER:
                return bank.AreaSoundWater;
            case AreaZoneType.SFX_FOREST:
                return bank.AreaSoundForest;
            default:
                return null;
        }
    }

    private MusicFileObject GetMusicForZone()
    {
        var bank = MasterSingleton.Instance.PrefabBankManager;
        switch (zoneType)
        {
            case AreaZoneType.MUSIC_SHRINE:
                return bank.shrineMusic;
            default:
                return null;
        }
    }

    private bool IsInPlayerLayer(GameObject obj)
    {
        return ((1 << obj.layer) & playerLayer) != 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsInPlayerLayer(other.gameObject) && !isPlayerInside)
        {
            isPlayerInside = true;

            sfx = GetSoundForZone();
            if (sfx != null)
            {
                AudioManager.PlaySound(sfx);
            }

            // Handle music zones: replace the level's main music while inside
            music = GetMusicForZone();
            if (music != null)
            {
                var bank = MasterSingleton.Instance.PrefabBankManager;
                // store previous music so we can restore it on exit
                previousMusic = bank.thisLevelMusic;

                // Stop previous music with fade (stopInstantly = false uses transition settings)
                if (previousMusic != null)
                {
                    AudioManager.StopMusic(previousMusic, null, false);
                }

                // set the bank's main music to this area's music
                bank.thisLevelMusic = music;

                // play the area's music (will fade in based on music file settings)
                AudioManager.PlayMusic(music);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsInPlayerLayer(other.gameObject) && isPlayerInside)
        {
            isPlayerInside = false;

            if (sfx != null)
            {
                AudioManager.StopSound(sfx);
            }

            // If we changed the level music for this area, stop it and restore previous
            if (music != null)
            {
                // Stop area music with fade (stopInstantly = false)
                AudioManager.StopMusic(music, null, false);

                var bank = MasterSingleton.Instance.PrefabBankManager;
                // restore the bank's main music
                bank.thisLevelMusic = previousMusic;

                if (previousMusic != null)
                {
                    // Play previous music (will fade in)
                    AudioManager.PlayMusic(previousMusic);
                }

                // clear stored references
                music = null;
                previousMusic = null;
            }
        }
    }
}