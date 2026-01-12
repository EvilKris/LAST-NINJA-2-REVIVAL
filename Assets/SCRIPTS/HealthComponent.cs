using UnityEngine;
using System;

/// <summary>
/// Component that manages an entity's health, damage, and death.
/// Implements IDamageable for receiving damage and ITargetable for lock-on targeting.
/// Fires events for hit, death, and health changes that other systems can subscribe to.
/// </summary>
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
    /// Fired whenever health changes, passing the new current health value.
    /// </summary>
    public event Action<float> OnHealthChanged;

    /// <summary>
    /// Initialize health to maximum value.
    /// </summary>
    private void Awake()
    {
        currentHealth = maxHealth;
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
    public void TakeDamage(float damage)
    {
        // Don't process damage if already dead or invulnerable
        if (IsDead || isInvulnerable) return;

        // Apply damage and clamp to 0
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        
        // Notify listeners of health change
        OnHealthChanged?.Invoke(currentHealth);

        // Check if damage was fatal
        if (IsDead)
        {
            OnDeath?.Invoke();
            Debug.Log($"{gameObject.name} has died. (Shibō - 死亡)");
        }
        else
        {
            // Entity survived - trigger hit reaction
            OnHit?.Invoke();
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