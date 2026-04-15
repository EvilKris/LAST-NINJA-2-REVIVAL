using JSAM;
using Unity.Cinemachine;
using UnityEngine;


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
    private float previousMusicTime;

    // Shrine camera — found on this prefab at startup
    private CinemachineCamera _shrineCamera;
    private int _shrineCameraOriginalPriority;
    private CinemachineCamera _previousCamera;

    private void Awake()
    {
        if (zoneType == AreaZoneType.MUSIC_SHRINE)
        {
            _shrineCamera = GetComponentInChildren<CinemachineCamera>();
            if (_shrineCamera != null)
                _shrineCameraOriginalPriority = _shrineCamera.Priority;
        }
        else
            Debug.Log("AreaSoundTrigger: MUSIC_SHRINE zone type requires a CinemachineCamera component on this or a parent GameObject for camera switching to work.");
    }

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

                // Stop previous music with fade, saving playback position to restore later
                if (previousMusic != null)
                {
                    var helper = AudioManager.StopMusic(previousMusic, null, false);
                    if (helper != null && helper.AudioSource != null)
                    {
                        previousMusicTime = helper.AudioSource.time;
                    }
                }

                // set the bank's main music to this area's music
                bank.thisLevelMusic = music;

                // play the area's music (will fade in based on music file settings)
                AudioManager.PlayMusic(music);
            }

            //turn on cam 
            if (zoneType == AreaZoneType.MUSIC_SHRINE && _shrineCamera != null)
            {
                _previousCamera = CameraZoneManager.Instance.GetCurrentCamera();
                CameraZoneManager.Instance.ActivateCamera(_shrineCamera);
            }
        }
    }

    /// <summary>
    /// Stops any active area sound/music and resets tracking state.
    /// Call this on player death so sounds do not persist through the respawn.
    /// </summary>
    public void ForceStop()
    {
        if (!isPlayerInside) return;

        isPlayerInside = false;

        if (sfx != null)
            AudioManager.StopSound(sfx);

        if (music != null)
        {
            AudioManager.StopMusic(music, null, false);
            var bank = MasterSingleton.Instance.PrefabBankManager;
            bank.thisLevelMusic = previousMusic;
            music = null;
            previousMusic = null;
            previousMusicTime = 0f;
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
                    // Fade previous music back in and restore saved playback position
                    var helper = AudioManager.FadeMusicIn(previousMusic, 0.2f);
                    if (helper != null && helper.AudioSource != null)
                    {
                        helper.AudioSource.time = previousMusicTime;
                    }
                }

                // clear stored references
                music = null;
                previousMusic = null;
                previousMusicTime = 0f;
            }



            //turn off cam 
            if (zoneType == AreaZoneType.MUSIC_SHRINE && _shrineCamera != null)
            {
                _shrineCamera.Priority = _shrineCameraOriginalPriority;
                if (_previousCamera != null)
                    CameraZoneManager.Instance.ActivateCamera(_previousCamera);
                _previousCamera = null;
            }

        }
    }
}