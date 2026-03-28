using UnityEngine;
using JSAM;

public class AreaSoundTrigger : MonoBehaviour
{
    public enum AreaZoneType { SWAMP, WATER, FOREST }

    [Header("Area Zone Type")]
    public AreaZoneType zoneType;

    [Tooltip("Layer to detect as player")]
    public LayerMask playerLayer = -1;

    private bool isPlayerInside = false;
    private SoundFileObject sfx;

    private SoundFileObject GetSoundForZone()
    {
        var bank = MasterSingleton.Instance.PrefabBankManager;
        switch (zoneType)
        {
            case AreaZoneType.SWAMP:
                return bank.AreaSoundSwamp;
            case AreaZoneType.WATER:
                return bank.AreaSoundWater;
            case AreaZoneType.FOREST:
                return bank.AreaSoundForest;
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
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsInPlayerLayer(other.gameObject) && isPlayerInside)
        {
            isPlayerInside = false;
            AudioManager.StopSound(sfx);
        }
    }
}