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

    private static bool _deadLayerConfigured = false;
    private static int _deadLayer = -1;
    private static int _floorLayer = -1;

    /// <summary>
    /// Initialize health to maximum value.
    /// </summary>
    private void Awake()
    {
        currentHealth = maxHealth;
        _animator = GetComponent<Animator>();
        ConfigureDeadLayer();
    }

    /// <summary>
    /// One-time static setup that configures physics layer collision rules for the 'Dead' layer.
    /// Dead entities will only collide with the floor, preventing corpses from blocking gameplay.
    /// </summary>
    private static void ConfigureDeadLayer()
    {
        if (_deadLayerConfigured) return;
        _deadLayerConfigured = true;

        _deadLayer = LayerMask.NameToLayer("Dead");
        _floorLayer = LayerMask.NameToLayer("Floor");

        if (_deadLayer < 0)
        {
            Debug.LogWarning("HealthComponent: 'Dead' layer not found. Add it in Edit > Project Settings > Tags and Layers.");
            return;
        }

        // Dead entities collide with nothing except the floor
        for (int i = 0; i < 32; i++)
        {
            bool shouldCollide = (i == _floorLayer);
            Physics.IgnoreLayerCollision(_deadLayer, i, !shouldCollide);
        }
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
            HandleDeath();
            return;
        }

        // Entity survived - trigger hit reaction
        OnHit?.Invoke();

        if (_animator != null && type != HitReactionType.None)
        {
            SetHitAnimatorParameters(type);
            _animator.SetTrigger("t_GetHit");
        }
    }

    /// <summary>
    /// Handles all death logic: layer swap, events, effects, animator trigger,
    /// material swap, and starting the cleanup coroutine.
    /// </summary>
    private void HandleDeath()
    {
        // Switch to Dead layer so the corpse only collides with the floor
        if (_deadLayer >= 0)
            SetLayerRecursive(gameObject, _deadLayer);

        OnDeath?.Invoke();
        //Debug.Log($"{gameObject.name} has died. (Shibō - 死亡)");

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

    /// <summary>
    /// Coroutine that waits for the death animation to finish, then destroys the GameObject.
    /// </summary>
    private System.Collections.IEnumerator DeathCleanupSequence()
    {
        // Wait for the animation to play out (adjust 3.0f to match your clip length)
        yield return new WaitForSeconds(3.0f);

        // Optional: Add a simple fade-out or puff of smoke here

        Destroy(gameObject); // Remove from scene
    }

    /// <summary>
    /// Sets the Animator parameters that drive the hit-reaction blend tree
    /// based on the <see cref="HitReactionType"/> of the incoming attack.
    /// </summary>
    /// <param name="type">The category of hit reaction to play.</param>
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

    /// <summary>
    /// Recursively sets the physics layer on <paramref name="obj"/> and all of its children.
    /// </summary>
    /// <param name="obj">Root GameObject to update.</param>
    /// <param name="layer">Layer index to assign.</param>
    private static void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        // GetComponentsInChildren includes the root and avoids manual recursion
        foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }
}