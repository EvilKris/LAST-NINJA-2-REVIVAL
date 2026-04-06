using JSAM;
using UnityEngine;

public class PrefabBankManager : MonoBehaviour
{
    [Tooltip("Material used for phantom Demon Soul-type vfx")]
    public Material PhantomMaterial;

    [Header("GhostTrail")]
    [Tooltip("Material used for Drive Strike ghost trail vfx")]
    public Material GhostTrailsMat;
    public Material GhostTrailsMatAdditive;

    [Header("Healing")]
    [Tooltip("Mat used when Healing")]
    public Material HealingMat;

    [Tooltip("Sound commences on Tier One Drive Strike")]
    public SoundFileObject Tier_One_Drive_Strike;
    public SoundFileObject Charge_Drive_Strike_Tier_Complete;

    [Header("Pickup")]
    [Tooltip("Default sound played when any item is picked up. Can be overridden per-item in ItemData.")]
    public SoundFileObject DefaultPickupSound;

    [Header("Environment AreaZone Sounds")]
    [Tooltip("Sounds played when player enters an AreaZone")]
    public SoundFileObject AreaSoundSwamp;
    public SoundFileObject AreaSoundWater;
    public SoundFileObject AreaSoundForest;

    [Header("Environment AreaZone Music")]
    [Tooltip("AreaZone but for actual music change")]
    public MusicFileObject thisLevelMusic;
    public MusicFileObject shrineMusic;

    [Header("Environment Particle Effects")]
    [Tooltip("Particle effect played when player enters an AreaZone")]
    public GameObject SwampDrowningSplashes;


    [Header("Drowning")]
    [Tooltip("Sound played when player falls into liquid and drowns")]
    public SoundFileObject DrowningSound_swamp;
    public SoundFileObject Bubbles; 

    [Header("Healing")]
    [Tooltip("Sound played when player uses a healing item")]
    public SoundFileObject HealingSound;
}
