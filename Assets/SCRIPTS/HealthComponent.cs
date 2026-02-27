using UnityEngine;
using System;
using JSAM;

/// <summary>
/// Component that manages an entity's health, damage, and death.
/// Implements IDamageable for receiving damage and ITargetable for lock-on targeting.
/// Fires events for hit, death, and health changes that other systems can subscribe to.
/// </summary>
[RequireComponent(typeof(Animator))] // Ensures the entity has an animator
public class HealthComponent : MonoBehaviour, IDamageable, ITargetable
{
    [Header("Stats")]
    [Tooltip("Maximum health points for this entity.")]
    public float maxHealth = 100f;
    
    [Tooltip("Current health points. Automatically set to maxHealth on Awake.")]
    [SerializeField] private float currentHealth;    
   
    [Header("Faction & Targeting")]
    [Tooltip("Team/faction alignment. Used for friend-or-foe identification.")]
    public Faction faction = Faction.Enemy;
    
    [Tooltip("Transform that represents the lock-on point for camera/AI targeting. Uses this transform if null.")]
    public Transform lockOnPoint;

    [Tooltip("If true, this entity cannot take damage (temporary invincibility, cutscenes, etc.).")]
    public bool isInvulnerable = false;

    [Header("Effects")]
    [Tooltip("ScriptableObject containing all visual and audio effects for this character.")]
    public CharacterEffects characterEffects;

    [Header("Internal References")]
    private Animator _animator;
   

    /// <summary>
    /// Returns true if the entity has 0 or less health.
    /// </summary>
    public bool IsDead => currentHealth <= 0;

    // Events that other systems can subscribe to
    /// <summary>
    /// Fired when the entity takes damage but doesn't die.
    /// </summary>
    public event Action OnHit;
    
    /// <summary>
    /// Fired when the entity's health reaches 0.
    /// </summary>
    public event Action OnDeath;
    
    /// <summary>
    /// Fired whenever health changes, passing current health, max health, and faction.
    /// </summary>
    public event Action<float, float, Faction> OnHealthChanged;

    /// <summary>
    /// Initialize health to maximum value.
    /// </summary>
    private void Awake()
    {
        currentHealth = maxHealth;
        _animator = GetComponent<Animator>();
    }

    // ═══════════════════════════════════════════════════════════════════
    // IDamageable Implementation
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reduces health by the specified damage amount.
    /// Ignored if the entity is already dead or invulnerable.
    /// Fires OnHit if damaged, OnDeath if health reaches 0.
    /// </summary>
    /// <param name="damage">Amount of damage to apply (positive value)</param>
    public void TakeDamage(float damage, HitReactionType type)
    {
        if (IsDead || isInvulnerable) return;

        // FIXED: Only subtract damage once
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        OnHealthChanged?.Invoke(currentHealth, maxHealth, faction);

        // Play hit effects using CharacterEffects SO
        if (characterEffects != null)
        {
            // Play appropriate hit sound based on damage severity
            characterEffects.PlayHitSound(damage, maxHealth);
            
            // Spawn hit VFX at impact point (offset forward for better visual)
            Vector3 hitPosition = transform.position + transform.forward * characterEffects.hitEffectForwardOffset;
            characterEffects.SpawnHitEffect(damage, maxHealth, hitPosition, -transform.forward);
        }

        // Check if damage was fatal
        if (IsDead)
        {
            HandleDeath(); // Trigger the sequence
            return; // Exit early so we don't play a hit reaction on a corpse
        }
        else
        {
            // Entity survived - trigger hit reaction
            OnHit?.Invoke();
        }


        // Trigger hit reaction only if alive
        if (_animator != null)
        {
            SetHitAnimatorParameters(type);
            _animator.SetTrigger("t_GetHit");
        }
    }

    private void HandleDeath()
    {
        OnDeath?.Invoke();
        Debug.Log($"{gameObject.name} has died. (Shibō - 死亡)");

        // Play death effects using CharacterEffects SO
        if (characterEffects != null)
        {
            characterEffects.PlayDeathEffects(transform);
        }

        if (_animator != null)
        {
            _animator.SetTrigger("isDead"); // Move to Death State
        }

        // Swap materials to phantom/ghost material 
        PrefabBankManager _bank = MasterSingleton.Instance.PrefabBankManager;
        _bank.SwapOutAllMaterials(gameObject, _bank.PhantomMaterial,false);

        // Disable components so the "corpse" doesn't slide around or block hits
        // if (TryGetComponent<Collider>(out var col)) col.enabled = false;
        /*
        foreach (var collider in GetComponents<Collider>())
        {
            collider.enabled = false;
        }*/

        // Start the removal timer
        StartCoroutine(DeathCleanupSequence());
    }

    private System.Collections.IEnumerator DeathCleanupSequence()
    {
        // Wait for the animation to play out (adjust 3.0f to match your clip length)
        yield return new WaitForSeconds(3.0f);

        // Optional: Add a simple fade-out or puff of smoke here

        Destroy(gameObject); // Remove from scene
    }

    private void SetHitAnimatorParameters(HitReactionType type)
    {
        // Organized your animator logic into a helper method
        if (type == HitReactionType.Light_High)
        {
            _animator.SetFloat("f_Random", UnityEngine.Random.value);
            _animator.SetInteger("i_HitType", 0);
        }
        else if (type == HitReactionType.Light_Low)
        {
            _animator.SetInteger("i_HitType", 1);
        }
        else if (type == HitReactionType.Heavy_Back)
        {
            _animator.SetInteger("i_HitType", 2);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // ITargetable Implementation
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the Transform that cameras/AI should focus on when targeting this entity.
    /// Falls back to this entity's transform if no lock-on point is specified.
    /// </summary>
    /// <returns>Transform representing the lock-on focus point</returns>
    public Transform GetLockOnPoint() => lockOnPoint != null ? lockOnPoint : transform;
    
    /// <summary>
    /// Checks if this entity is a valid target for lock-on systems.
    /// Dead entities cannot be targeted.
    /// </summary>
    /// <returns>True if entity can be targeted (is alive), false otherwise</returns>
    public bool IsValidTarget() => !IsDead;
    
    /// <summary>
    /// Returns the faction/team allegiance of this entity.
    /// Used for friend-or-foe identification in AI and targeting systems.
    /// </summary>
    /// <returns>Faction enum value</returns>
    public Faction GetFaction() => faction;
}