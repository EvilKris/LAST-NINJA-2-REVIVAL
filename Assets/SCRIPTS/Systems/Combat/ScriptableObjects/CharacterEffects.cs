using UnityEngine;
using JSAM;

/// <summary>
/// ScriptableObject that defines all visual and audio effects for a character.
/// Covers common fighting game scenarios including hits, blocks, knockdowns, and status effects.
/// Used by HealthComponent and CombatHandler to play appropriate feedback.
/// </summary>
[CreateAssetMenu(fileName = "NewCharacterEffects", menuName = "Combat/Character Effects")]
public class CharacterEffects : ScriptableObject
{
    // ?????????????????????????????????????????????????????????????????????????????????????????????
    // AUDIO EFFECTS (SFX)
    // ?????????????????????????????????????????????????????????????????????????????????????????????
    
    [Header("--- ATTACK CRIES/DAMAGE REACTIONS ---")]

    [Tooltip("Light attack sound (random)")]
    public SoundFileObject sfxLightAttackCry;
    [Tooltip("Light hit reaction sound (small grunt/gasp)")]
    public SoundFileObject sfxLightHit;
    
    [Tooltip("Medium attack sound (random)")]
    public SoundFileObject sfxMediumAttackCry;
    [Tooltip("Medium hit reaction sound (moderate grunt/pain)")]
    public SoundFileObject sfxMediumHit;

    [Tooltip("Heavy attack sound (random - more common)")]
    public SoundFileObject sfxHeavyAttackCry;
    [Tooltip("Heavy hit reaction sound (large grunt/pain)")]
    public SoundFileObject sfxHeavyHit;


    [Tooltip("Critical hit reaction sound (extreme pain/dramatic)")]
    public SoundFileObject sfxCriticalHit;

    [Tooltip("Sound played when the character is being thrown")]
    public SoundFileObject sfxBeingThrown;

    [Tooltip("Sound played when the character impacts floor after being thrown")]
    public SoundFileObject sfxLandAfterThrown;


    [Header("--- PAIN VOCALS/GRUNTS SOUND EFFECTS/DEATH --- ")]
    [Tooltip("Sound played for light pain vocals/grunts - played randomly not every instance")]
    public SoundFileObject sfxLightPainVocal;
    [Tooltip("Sound played for heavy pain vocals/grunts - always played")]
    public SoundFileObject sfxHeavyPainVocal;
    [Tooltip("Sound played for death")]
    public SoundFileObject sfxDeathVocal;
    [Tooltip("Sound played when the character struggles in a clinch")]
    public SoundFileObject sfxClinchStruggleVocal;
    [Tooltip("Sound played when the character throws an opponent")]
    public SoundFileObject sfxThrowVocal;

    [Header("--- SPEECH ---")]
    [Tooltip("Sound played for character talking/dialogue")]
    public SoundFileObject speechSFX;

    [Header("--- BLOCKING ---")]
    [Tooltip("Successfully blocked an attack (impact sound)")]
    public SoundFileObject sfxBlock;
    
    [Tooltip("Perfect/parry block sound (metallic ring or special effect)")]
    public SoundFileObject sfxPerfectBlock;
    
    [Tooltip("Guard break sound (shield crack/shatter)")]
    public SoundFileObject sfxGuardBreak;
    
    [Header("--- KNOCKDOWN & GETUP ---")]
    [Tooltip("Body hitting the ground (thud)")]
    public SoundFileObject sfxKnockdown;
    
    [Tooltip("Getting back up from knockdown (effort grunt)")]
    public SoundFileObject sfxGetUp;
    
    [Tooltip("Ragdoll/thrown body sound (heavy impact)")]
    public SoundFileObject sfxThrown;
    
    [Header("??? DEATH ???")]
    [Tooltip("Death cry/scream")]
    public SoundFileObject sfxDeath;
    
    [Tooltip("Body collapsing sound")]
    public SoundFileObject sfxBodyCollapse;
    
    [Header("--- FOOTSTEPS ---")]
    [Tooltip("Default footstep sound played while walking (used when no TerrainSoundData is found).")]
    public SoundFileObject sfxFootstepWalk;

    [Tooltip("Default footstep sound played while running (used when no TerrainSoundData is found). Falls back to sfxFootstepWalk if unassigned.")]
    public SoundFileObject sfxFootstepRun;

    [Header("--- MOVEMENT & EXERTION ---")]
    [Tooltip("Jumping effort sound")]
    public SoundFileObject sfxJump;
    
    [Tooltip("Landing from height")]
    public SoundFileObject sfxLand;
    
    [Tooltip("Dash/dodge sound (quick movement)")]
    public SoundFileObject sfxDash;
    
    [Tooltip("Heavy breathing (low health or exhaustion)")]
    public SoundFileObject sfxBreathing;
    
    [Header("--- SPECIAL STATES ---")]
    [Tooltip("Power-up activation sound")]
    public SoundFileObject sfxPowerUp;
    
    [Tooltip("Stun/dizzy sound (birds chirping, stars)")]
    public SoundFileObject sfxStunned;
    
    [Tooltip("Healing/recovery sound")]
    public SoundFileObject sfxHeal;

    // ?????????????????????????????????????????????????????????????????????????????????????????????
    // VISUAL EFFECTS (VFX)
    // ?????????????????????????????????????????????????????????????????????????????????????????????
    [Space(3)]
    [Header("═══════════════ VISUAL EFFECTS (VFX) ═══════════════")]
    [Space(3)]

    [Header("--- ATTACK EFFECTS ---")]  
    [Tooltip("Melee strike trail effect ")]
    public GameObject strikeTrailMelee;  
    
    [Header("--- ATTACK HIT EFFECTS ---")]
    [Tooltip("Light hit particle effect (small impact sparks)")]
    public GameObject vfxLightHit;
    
    [Tooltip("Medium hit particle effect (blood/energy burst)")]
    public GameObject vfxMediumHit;
    
    [Tooltip("Heavy hit particle effect (large impact explosion)")]
    public GameObject vfxHeavyHit;
    
    [Tooltip("Critical hit particle effect (screen flash, special visual)")]
    public GameObject vfxCriticalHit;
    
    [Header("--- BLOCK EFFECTS ---")]
    [Tooltip("Block spark effect (clash visual)")]
    public GameObject vfxBlockSpark;
    
    [Tooltip("Perfect block effect (bright flash, ring expansion)")]
    public GameObject vfxPerfectBlock;
    
    [Tooltip("Guard break effect (shield shatter particles)")]
    public GameObject vfxGuardBreak;
    
    [Header("--- STATUS EFFECTS ---")]
    [Tooltip("Stun stars/birds circling head")]
    public GameObject vfxStunned;
    
    [Tooltip("Power-up aura/glow")]
    public GameObject vfxPowerUp;
    
    [Tooltip("Healing sparkles/light")]
    public GameObject vfxHeal;
    
    [Tooltip("Low health warning visual (red glow, sweat drops)")]
    public GameObject vfxLowHealth;
    
    [Header("--- MOVEMENT EFFECTS ---")]
    [Tooltip("Dust cloud when landing")]
    public GameObject vfxLandDust;
    
    [Tooltip("Dash trail/after-image effect")]
    public GameObject vfxDashTrail;
    
    [Tooltip("Ground impact ripple (heavy landing or knockdown)")]
    public GameObject vfxGroundImpact;
    
    [Header("--- DEATH EFFECTS ---")]
    [Tooltip("Death explosion/fade effect")]
    public GameObject vfxDeath;
    
    [Tooltip("Soul/spirit leaving body effect (optional)")]
    public GameObject vfxSoulRelease;
    
    // ?????????????????????????????????????????????????????????????????????????????????????????????
    // SPAWN POINTS & OFFSETS
    // ?????????????????????????????????????????????????????????????????????????????????????????????
    
    [Header("--- VFX SPAWN CONFIGURATION ---")]
    [Tooltip("Default VFX spawn height offset from character origin (useful for centering effects)")]
    public float vfxHeightOffset = 1f;
    
    [Tooltip("Forward offset for hit effects (so they appear at contact point, not character center)")]
    public float hitEffectForwardOffset = 0.5f;
    
    [Tooltip("Default lifetime for spawned VFX (if they don't auto-destroy)")]
    public float defaultVfxLifetime = 2f;
    
    // ?????????????????????????????????????????????????????????????????????????????????????????????
    // HELPER METHODS
    // ?????????????????????????????????????????????????????????????????????????????????????????????
    
    /// <summary>
    /// Plays the appropriate hit sound based on damage severity.
    /// </summary>
    /// <param name="damage">Damage amount received</param>
    /// <param name="maxHealth">Maximum health of character (for calculating severity)</param>
    public void PlayHitSound(float damage, float maxHealth)
    {
        float damagePercent = damage / maxHealth;
        
        if (damagePercent >= 0.3f && sfxCriticalHit != null)
            JSAM.AudioManager.PlaySound(sfxCriticalHit);
        else if (damagePercent >= 0.15f && sfxHeavyHit != null)
            JSAM.AudioManager.PlaySound(sfxHeavyHit);
        else if (damagePercent >= 0.05f && sfxMediumHit != null)
            JSAM.AudioManager.PlaySound(sfxMediumHit);
        else if (sfxLightHit != null)
            JSAM.AudioManager.PlaySound(sfxLightHit);
    }
    
    /// <summary>
    /// Spawns the appropriate hit VFX based on damage severity.
    /// </summary>
    /// <param name="damage">Damage amount received</param>
    /// <param name="maxHealth">Maximum health of character</param>
    /// <param name="spawnPosition">World position to spawn effect</param>
    /// <param name="hitDirection">Direction the hit came from (for effect orientation)</param>
    public GameObject SpawnHitEffect(float damage, float maxHealth, Vector3 spawnPosition, Vector3 hitDirection)
    {
        float damagePercent = damage / maxHealth;
        GameObject effectPrefab = null;
        
        if (damagePercent >= 0.3f && vfxCriticalHit != null)
            effectPrefab = vfxCriticalHit;
        else if (damagePercent >= 0.15f && vfxHeavyHit != null)
            effectPrefab = vfxHeavyHit;
        else if (damagePercent >= 0.05f && vfxMediumHit != null)
            effectPrefab = vfxMediumHit;
        else if (vfxLightHit != null)
            effectPrefab = vfxLightHit;
        
        if (effectPrefab != null)
        {
            Vector3 adjustedPosition = spawnPosition + Vector3.up * vfxHeightOffset;
            Quaternion rotation = hitDirection != Vector3.zero 
                ? Quaternion.LookRotation(hitDirection) 
                : Quaternion.identity;
            
            GameObject instance = Instantiate(effectPrefab, adjustedPosition, rotation);
            
            // Auto-destroy if the prefab doesn't have its own self-destruct
            if (instance.GetComponent<ParticleSystem>() == null || !instance.GetComponent<ParticleSystem>().main.stopAction.Equals(ParticleSystemStopAction.Destroy))
            {
                Destroy(instance, defaultVfxLifetime);
            }
            
            return instance;
        }
        
        return null;
    }
    
    /// <summary>
    /// Plays death effects (both audio and visual).
    /// </summary>
    /// <param name="characterTransform">Transform of the dying character</param>
    public void PlayDeathEffects(Transform characterTransform)
    {
        // Audio
        if (sfxDeath != null)
            JSAM.AudioManager.PlaySound(sfxDeath);
        
        if (sfxBodyCollapse != null)
            JSAM.AudioManager.PlaySound(sfxBodyCollapse);
        
        // Visual
        if (vfxDeath != null)
        {
            Vector3 spawnPos = characterTransform.position + Vector3.up * vfxHeightOffset;
            GameObject deathEffect = Instantiate(vfxDeath, spawnPos, Quaternion.identity);
            Destroy(deathEffect, defaultVfxLifetime);
        }
        
        if (vfxSoulRelease != null)
        {
            Vector3 spawnPos = characterTransform.position + Vector3.up * (vfxHeightOffset * 1.5f);
            GameObject soulEffect = Instantiate(vfxSoulRelease, spawnPos, Quaternion.identity);
            Destroy(soulEffect, defaultVfxLifetime + 1f);
        }
    }
    
    /// <summary>
    /// Plays block effects at the impact point.
    /// </summary>
    /// <param name="isPerfectBlock">Whether this was a perfect/parry block</param>
    /// <param name="impactPosition">World position of the block</param>
    /// <param name="impactNormal">Surface normal at impact (for effect orientation)</param>
    public void PlayBlockEffects(bool isPerfectBlock, Vector3 impactPosition, Vector3 impactNormal)
    {
        // Audio
        if (isPerfectBlock && sfxPerfectBlock != null)
            JSAM.AudioManager.PlaySound(sfxPerfectBlock);
        else if (sfxBlock != null)
            JSAM.AudioManager.PlaySound(sfxBlock);
        
        // Visual
        GameObject effectPrefab = isPerfectBlock ? vfxPerfectBlock : vfxBlockSpark;
        
        if (effectPrefab != null)
        {
            Quaternion rotation = impactNormal != Vector3.zero 
                ? Quaternion.LookRotation(impactNormal) 
                : Quaternion.identity;
            
            GameObject blockEffect = Instantiate(effectPrefab, impactPosition, rotation);
            Destroy(blockEffect, defaultVfxLifetime);
        }
    }
}
