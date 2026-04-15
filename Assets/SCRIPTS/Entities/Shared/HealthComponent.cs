using UnityEngine;
using System;
using JSAM;
using DG.Tweening;

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

    [Tooltip("Empty GameObject used as the Cinemachine camera tracking target (Follow / Look At). Uses this transform if null.")]
    public Transform cameraTrackingTarget;

    [Tooltip("If true, this entity cannot take damage (temporary invincibility, cutscenes, etc.).")]
    public bool isInvulnerable = false;

    [Header("Effects")]
    [Tooltip("ScriptableObject containing all visual and audio effects for this character.")]
    public CharacterEffects characterEffects;

    [Header("Layers")]
    [Tooltip("Layer assigned to this entity when it dies. Corpses on this layer only collide with the Floor layer.")]
    public LayerMask deadLayer;
    [Tooltip("Layer used for floor collision. Dead entities remain collidable only with this layer.")]
    public LayerMask floorLayer;

    [Header("Internal References")]
    private Animator _animator;
    private CombatHandler _combatHandler;
    // Sequence used for incremental heal-to-full tweens
    private Sequence _healSequence;

    // Resolved single-bit layer indices derived from the LayerMask fields in Awake
    private int _deadLayerIndex  = -1;
    private int _floorLayerIndex = -1;

    /// <summary>
    /// Returns true if the entity has 0 or less health.
    /// </summary>
    public bool IsDead => currentHealth <= 0;

    /// <summary>
    /// Read-only access to current health. Used by <see cref="Checkpoint"/> to snapshot state.
    /// </summary>
    public float CurrentHealth => currentHealth;

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
        _combatHandler = GetComponent<CombatHandler>();

        _deadLayerIndex  = LayerMaskToIndex(deadLayer);
        _floorLayerIndex = LayerMaskToIndex(floorLayer);

        ConfigureDeadLayer();
    }

    private void Start()
    {
        MasterSingleton.Instance?.UIManager?.RegisterHealthComponent(this);
    }

    private void OnDestroy()
    {
        if (MasterSingleton.Instance != null && MasterSingleton.Instance.UIManager != null)
            MasterSingleton.Instance.UIManager.UnregisterHealthComponent(this);
    }

    /// <summary>
    /// Configures physics layer collision rules so dead entities only collide with the floor.
    /// Reads <see cref="deadLayer"/> and <see cref="floorLayer"/> assigned in the Inspector.
    /// </summary>
    private void ConfigureDeadLayer()
    {
        if (_deadLayerIndex < 0)
        {
            Debug.LogWarning($"HealthComponent on '{gameObject.name}': Dead Layer is not set. Assign it in the Inspector.", this);
            return;
        }

        // Dead entities collide with nothing except the floor
        for (int i = 0; i < 32; i++)
        {
            bool shouldCollide = (i == _floorLayerIndex);
            Physics.IgnoreLayerCollision(_deadLayerIndex, i, !shouldCollide);
        }
    }

    /// <summary>
    /// Converts a <see cref="LayerMask"/> bitmask to a single layer index.
    /// Returns -1 if the mask is empty.
    /// </summary>
    private static int LayerMaskToIndex(LayerMask mask)
    {
        int value = mask.value;
        if (value == 0) return -1;
        for (int i = 0; i < 32; i++)
        {
            if ((value & (1 << i)) != 0)
                return i;
        }
        return -1;
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
    /// <summary>Block damage multiplier — blocked hits deal only this fraction of full damage.</summary>
    private const float BlockDamageMultiplier = 0.05f;

    public void TakeDamage(float damage, HitReactionType type)
    {
        if (IsDead || isInvulnerable) return;

        // If the entity is blocking, reduce damage to 5% and suppress knockdown
        bool isBlocking = _combatHandler != null && _combatHandler.IsBlocking;
        if (isBlocking)
        {
            damage *= BlockDamageMultiplier;
            type = HitReactionType.None; // Knockdown / stagger cannot happen while blocking

            if (characterEffects != null)
                characterEffects.PlayBlockEffects(false, transform.position + transform.forward * characterEffects.hitEffectForwardOffset, transform.forward);
        }

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        OnHealthChanged?.Invoke(currentHealth, maxHealth, faction);

        // Play hit effects using CharacterEffects SO (skipped while blocking — block effects fired above)
        if (!isBlocking && characterEffects != null)
        {
            characterEffects.PlayHitSound(damage, maxHealth);
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
        // Revert to fist style before any death animation plays
        if (_combatHandler != null)
            _combatHandler.RevertToDefaultStyle();

        // Clear the weapon UI icon if this is the player
        if (faction == Faction.Player)
            MasterSingleton.Instance?.InventoryManager?.RevertToFists();

        // Switch to Dead layer so the corpse only collides with the floor
        if (_deadLayerIndex >= 0)
            SetLayerRecursive(gameObject, _deadLayerIndex);

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
        MasterSingleton.Instance.PlayerManager.SwapOutAllMaterials(gameObject, _bank.PhantomMaterial, false);

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
        else if (type == HitReactionType.Light_Stun)
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
    /// Returns the Transform used as the Cinemachine camera tracking target (Follow / Look At).
    /// Falls back to this entity's transform if no tracking target is assigned.
    /// </summary>
    /// <returns>Transform for Cinemachine Follow and Look At binding</returns>
    public Transform GetCameraTrackingTarget() => cameraTrackingTarget != null ? cameraTrackingTarget : transform;
    
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

    public void HealToFull()
    {
        //called when the player interacts with the Buddha Shrine healing trigger, or any other healing source that fully restores health.
        // If already at or above max, do nothing
        if (currentHealth >= maxHealth) return;

        // Grab PrefabBankManager for material swaps during heal
        PrefabBankManager _bank = MasterSingleton.Instance.PrefabBankManager;

        // Kill any existing heal sequence and restore materials if a previous heal had swapped them
        if (_healSequence != null && _healSequence.IsActive())
        {
            _healSequence.Kill();
            _healSequence = null;

        }

        // Calculate how many +1 steps are required
        int steps = Mathf.CeilToInt(maxHealth - currentHealth);
        if (steps <= 0) return;

        // Swap in the healing material on all renderers for visual feedback
        if (_bank != null && _bank.HealingMat != null)
        {
            MasterSingleton.Instance.PlayerManager.SwapOutAllMaterials(gameObject, _bank.HealingMat, false);
        }

        _healSequence = DOTween.Sequence();

        for (int i = 0; i < steps; i++)
        {
            _healSequence.AppendInterval(0.1f);
            _healSequence.AppendCallback(() =>
            {
                currentHealth = Mathf.Min(currentHealth + 1f, maxHealth);
                OnHealthChanged?.Invoke(currentHealth, maxHealth, faction);
                // If we've reached max, stop the sequence early
                if (currentHealth >= maxHealth && _healSequence != null && _healSequence.IsActive())
                {
                    // Restore original materials when healing completes
                  //  if (_bank != null)
                    //    _bank.RestoreSharedMaterials(gameObject);

                    _healSequence.Kill();
                    _healSequence = null;
                }
            });
        }

        _healSequence.Play();
    }

    /// <summary>
    /// Sets current health to a percentage of maxHealth (0..1) and fires the OnHealthChanged event.
    /// Useful for testing scenarios where you want to force a specific health level.
    /// </summary>
    /// <param name="percent">Clamped 0..1 representing fraction of max health.</param>
    public void SetHealthPercentage(float percent)
    {
        float clamped = Mathf.Clamp01(percent);
        currentHealth = maxHealth * clamped;
        OnHealthChanged?.Invoke(currentHealth, maxHealth, faction);

        if (IsDead)
            HandleDeath();
    }

    /// <summary>
    /// Restores the entity to full health without triggering death or hit events.
    /// Used after a respawn (e.g. drowning) to reset the entity to a living state.
    /// </summary>
    public void Revive()
    {
        currentHealth = maxHealth;
        isInvulnerable = false;
        OnHealthChanged?.Invoke(currentHealth, maxHealth, faction);
    }

    /// <summary>
    /// Restores health to an exact value without triggering death or hit events.
    /// Used by <see cref="GameManager"/> to apply a <see cref="CheckpointSnapshot"/>.
    /// </summary>
    public void SetHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth, faction);
    }


}