using JSAM;
using UnityEngine;

public class PrefabBankManager : MonoBehaviour
{
    [Header("Special Mats (Shaders)")]
    [Tooltip("Material used for phantom Demon Soul-type vfx")]
    public Material PhantomMaterial;

    [Tooltip("GhostTrail Material used for Strike ghost trail vfx")]
    public Material GhostTrailsMat;
    public Material GhostTrailsMatAdditive;

    [Tooltip("Mat applied used when Healing")]
    public Material HealingMat;

    [Tooltip("Materialize Mat used for He-Man Glow on objects within the camera's view")]
    public Material MaterializeMat;

    [Header("Projectiles")]
    [Tooltip("Prefab for the basic projectile used by the player and some enemies. Contains a Projectile component and a TrailRenderer.")]
    public GameObject smokeBombProjectilePrefab; // Used for the smoke bomb projectile and some enemy projectiles


    [Header("VFX EFFECTS (VISUAL PREFABS) ")]
    [Tooltip("Prefab for the basic explosion effect used by the smoke bomb and some hit effects. Contains a ParticleSystem component.")]
    public GameObject smokeExplosionPrefab; // Used for smoke bomb explosion and some hit effects
    public GameObject shurikenHitPrefab; // Used for shuriken hit effects

    [Header("SFX")]
    [Tooltip("Sound commencing on Tier One Drive Strike")]
    public SoundFileObject Tier_One_Drive_Strike; // Played when a Drive Strike is initiated
    public SoundFileObject Charge_Drive_Strike_Tier_Complete; // Played when a Drive Strike reaches max charge
    public SoundFileObject smokeExplosionSfx; // Used for smoke bomb explosion and some hit effects
    public SoundFileObject shurikenHitSfx; // Used for shuriken hit effects   


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

    [Header("Materialize")]
    [Tooltip("Sound played when player materializes")]  
    public SoundFileObject MaterializeSound;    

    [Header("Healing")]
    [Tooltip("Sound played when player uses a healing item")]
    public SoundFileObject HealingSound;
}
