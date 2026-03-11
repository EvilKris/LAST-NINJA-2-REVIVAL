using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central combat controller for an entity. Manages attack execution (light, heavy,
/// charged, acrobatic, clinch), hitbox lifecycle, combo chaining, the charge/tier system,
/// KI defensive actions, and motion-root translation driven by animation curves.
/// Implements <see cref="IAnimationStateListener"/> so the Animator can signal when a
/// clip has finished, allowing <see cref="ResetCombatState"/> to clean up automatically.
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class CombatHandler : MonoBehaviour, IAnimationStateListener
{
    // -------------------------------------------------------------------------
    // Components
    // -------------------------------------------------------------------------
    [Header("Components")]
    private Animator _animator;
    private AnimatorOverrideController _overrideController; // Swaps animation clips at runtime without duplicating the controller asset
    private HealthComponent _health;
    private MovementComponent _movement;


    [Tooltip("Number of charges the entity has (for consecutively stronger special moves) - a move must also be added in the Fighting Style config")]
    public int ChargeCount = 1; // Number of charges the entity has


    // -------------------------------------------------------------------------
    // Fighting Style Data
    // -------------------------------------------------------------------------
    [Header("Data")]
    /// <summary>The active <see cref="FightingStyle"/> ScriptableObject that defines all moves for this entity.</summary>
    public FightingStyle currentStyle;
    
    // -------------------------------------------------------------------------
    // Combo Settings
    // -------------------------------------------------------------------------
    [Header("Combo Settings")]
    private int _comboIndex = 0;          // Tracks which light attack in the sequence fires next
    private float _lastAttackTime;         // Timestamp of the last executed attack, used to detect combo expiry
    private const float COMBO_RESET_TIME = 1.2f; // Seconds of inactivity before the combo resets to the first hit

    // -------------------------------------------------------------------------
    // Charge System  ("Spike Out" style hold-to-charge mechanic)
    // -------------------------------------------------------------------------
    [Header("Charge System (Spike Out Style)")]
    private float _currentChargeTimer;       // Accumulates in seconds while the player holds the attack button; integer part = current tier
    private bool _isCharging;                // True while the button is held and a charge is building
    private int _cachedMaxCharges = -1;      // Cached value to detect style changes and fire OnMaxChargesChanged only when necessary
    private int _cachedCurrentTier = -1;     // Last-known tier, used to avoid redundant UI events
    private float _cachedChargeProgress = -1f; // Last-known fractional progress within the current tier
    private int _lastPlayedTierSfx = -1;     // Prevents the tier-complete SFX from firing more than once per tier

    // --- Charge Events (consumed by UIChargeDisplay and any other listeners) ---
    /// <summary>Fires when the number of available charge tiers changes, e.g. after a style swap.</summary>
    public event Action<int> OnMaxChargesChanged;
    /// <summary>Fires every frame while charging whenever the tier or fractional progress changes appreciably.</summary>
    public event Action<int, float> OnChargeStateChanged;

    // --- Charge Properties ---
    /// <summary>Total number of charged attack tiers defined by the current <see cref="FightingStyle"/>.</summary>
    public int MaxCharges => currentStyle != null && currentStyle.chargedAttacks != null ? currentStyle.chargedAttacks.Count : 0;
    /// <summary>The whole-number tier reached so far (floor of <see cref="_currentChargeTimer"/>).</summary>
    public int CurrentTier => Mathf.FloorToInt(_currentChargeTimer);
    /// <summary>Fractional progress (0–1) toward the next tier; drives the smooth UI charge bar.</summary>
    public float ChargeProgress => _currentChargeTimer % 1.0f;

    // -------------------------------------------------------------------------
    // Internal State
    // -------------------------------------------------------------------------
    [Header("Internal State")]
    private IActiveCombatMove _activeMove;          // The move that is currently playing; null when idle
    private HashSet<Transform> _hitCache = new();   // Entities already struck by the current swing; prevents multi-hit on a single swing
    private CombatHitbox[] _allHitboxes;            // All child hitboxes, cached in Awake
    private bool _hitboxActive;                     // Whether a hitbox is currently open this frame
    private bool _canAcceptComboInput;              // True during the combo window so the next attack can chain seamlessly
    private bool _isAcrobaticMove;                  // Flags the active move as an acrobatic action (used by MovementComponent for special handling)

    // -------------------------------------------------------------------------
    // KI / Defensive Settings
    // -------------------------------------------------------------------------
    [Header("KI Settings")]
    private float _kiBars = 3f;                      // Current KI energy; each defensive action costs 1 bar
    private const float KI_PARRY_WINDOW = 0.2f;      // Seconds after blocking begins during which a KI parry can be triggered
    private float _lastBlockStartTime;               // Timestamp when the block input was first held down
    private bool _isBlocking;                        // Whether the entity is currently in a blocking stance

    private const string CLIP_SLOT_KEY = "Replaceable_Motion_Base"; // Name of the placeholder clip inside the AnimatorController that gets swapped at runtime
    private readonly int HashIsAction = Animator.StringToHash("isAction"); // Pre-hashed Animator parameter for performance

    // -------------------------------------------------------------------------
    // Motion / Root-Motion State
    // -------------------------------------------------------------------------
    [Header("Motion State")]
    private float _lastNormalizedTime;       // normalizedTime from the previous FixedUpdate tick; used to compute per-frame root-motion delta
    private bool _canRotateDuringAttack;     // Derived each frame from the active move's rotation curve; exposed to MovementComponent

    // --- Public read-only state accessors consumed by movement, AI, and UI ---
    public bool CanRotateDuringAttack => _canRotateDuringAttack; // True during the portion of an attack where the entity may turn
    public bool IsAttacking => _activeMove != null;              // True whenever any move is in progress
    public bool IsAcrobatic => _isAcrobaticMove;                 // True while an acrobatic move is executing
    public bool IsCharging => _isCharging;                       // True while the charge button is held

    // Pre-baked animation clips shared with ClinchHandler to avoid repeated lookups
    public AnimationClip ClinchThrowAttackerClip { get; private set; }
    public AnimationClip ClinchThrowVictimClip { get; private set; }
    public AnimationClip ClinchLightAtkAttackerClip { get; private set; }
    public AnimationClip ClinchLightAtkDefenderClip { get; private set; }

    /// <summary>Exposes the override controller so other systems (e.g. <see cref="ClinchHandler"/>) can swap clips.</summary>
    public AnimatorOverrideController OverrideController => _overrideController;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _health   = GetComponent<HealthComponent>();
        _movement = GetComponent<MovementComponent>();

        // Wrap the original controller in an override so we can hot-swap individual clips
        _overrideController = new AnimatorOverrideController(_animator.runtimeAnimatorController);
        _animator.runtimeAnimatorController = _overrideController;

        // Keep Animator ticks in sync with physics (FixedUpdate drives root motion)
        _animator.updateMode = AnimatorUpdateMode.Fixed;

        // Cache all hitboxes once; avoids per-frame GetComponentsInChildren calls
        _allHitboxes = GetComponentsInChildren<CombatHitbox>();
    }

    private void Start()
    {
        //put in Start to ensure other components are initialized first
        if (gameObject.CompareTag("Player"))
        {
            // Find the specific UI instance tagged as 'PlayerUI' or through your Manager
            UIChargeDisplay playerUI = MasterSingleton.Instance.UIManager.chargeMeter;


            //GameObject.FindObjectOfType<UIChargeDisplay>();
            playerUI.SetTarget(this);
        }
        InitializeStyleModules();
    }


    private void InitializeStyleModules()
    {
        // Pre-bake clinch throw clips from the current style
        if (currentStyle != null && currentStyle.clinchThrowDefault != null)
        {
            ClinchThrowAttackerClip = currentStyle.clinchThrowDefault.attackerThrowClip;
            ClinchThrowVictimClip = currentStyle.clinchThrowDefault.victimThrowClip;
        }
        else
        {
            ClinchThrowAttackerClip = null;
            ClinchThrowVictimClip = null;
        }

        // Pre-bake clinch light attack clips from the current style
        if (currentStyle != null && currentStyle.clinchLightAtk != null)
        {
            ClinchLightAtkAttackerClip = currentStyle.clinchLightAtk.attackerAttackClip;
            ClinchLightAtkDefenderClip = currentStyle.clinchLightAtk.victimAttackClip;
        }
        else
        {
            ClinchLightAtkAttackerClip = null;
            ClinchLightAtkDefenderClip = null;
        }
                
        // Check if the current style supports clinching
        // Only players can initiate clinches, enemies should not have ClinchHandler
        if (currentStyle != null && currentStyle.supportsClinching)
        {
            // Add the component if it's missing
            if (!TryGetComponent<ClinchHandler>(out _clinchModule))
            {
                _clinchModule = gameObject.AddComponent<ClinchHandler>();
            }
            _clinchModule.Initialize(this); // Link back to handler
        }
        else
        {
            // Remove it if the new style doesn't support it or if this is not a player
            if (TryGetComponent<ClinchHandler>(out var oldModule))
            {
                Destroy(oldModule);
            }
        }
        
    }


    /*
    private void InvokeCharges()
    {
        // Initialize charge state and notify listeners
        int maxCharges = MaxCharges;
        _cachedMaxCharges = maxCharges;
        OnMaxChargesChanged?.Invoke(maxCharges);
    }*/

    private void Update()
    {
        // Always tick the charge system, even when not attacking
        HandleChargeLogic();

        if (_activeMove == null) return;

        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName("ReplaceableAttack")) return; // Only process while the attack state is active

        float currentTime = stateInfo.normalizedTime;

        TickMoveState(currentTime);

        // --- Rotation allowance ---
        // Let the move's curve dictate whether the entity can turn mid-attack
        _canRotateDuringAttack = _activeMove is CombatMove cm && cm.CanRotate(currentTime);
        _movement.canRotate = _canRotateDuringAttack;
    }

    private void FixedUpdate()
    {
        if (_activeMove == null) return;

        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("ReplaceableAttack"))
        {
            float currentTime = stateInfo.normalizedTime;

            // Apply scripted root motion: evaluate the move's distance curve over the
            // delta between the last and current normalizedTime, then push the entity forward.
            if (currentTime > _lastNormalizedTime && _lastNormalizedTime >= 0)
            {
                if (_activeMove is CombatMove moveCast)
                {
                    float deltaDistance = moveCast.EvaluateMotionDelta(_lastNormalizedTime, currentTime);
                    if (deltaDistance > 0)
                        transform.position += transform.forward * deltaDistance;
                }
            }
            _lastNormalizedTime = currentTime;
        }
    }

    // =========================================================================
    // Charge Logic
    // =========================================================================

    /// <summary>
    /// Ticked every Update. Advances the charge timer while the button is held,
    /// fires change events only when the tier or progress shifts meaningfully,
    /// and plays the tier-complete SFX once per tier crossing.
    /// Also detects style changes so <see cref="OnMaxChargesChanged"/> stays accurate.
    /// </summary>
    private void HandleChargeLogic()
    {
        // Detect style/weapon swaps that alter the maximum number of charge tiers
        int maxCharges = MaxCharges;
        if (maxCharges != _cachedMaxCharges)
        {
            _cachedMaxCharges = maxCharges;
            OnMaxChargesChanged?.Invoke(maxCharges);
        }

        if (_isCharging)
        {
            // Advance the timer, capping at the maximum tier count
            _currentChargeTimer = Mathf.Min(_currentChargeTimer + Time.deltaTime, MaxCharges);

            // Throttle events: only broadcast when the tier or progress actually changed
            int currentTier = CurrentTier;
            float chargeProgress = ChargeProgress;
            if (currentTier != _cachedCurrentTier || Mathf.Abs(chargeProgress - _cachedChargeProgress) > 0.01f)
            {
                _cachedCurrentTier = currentTier;
                _cachedChargeProgress = chargeProgress;
                OnChargeStateChanged?.Invoke(currentTier, chargeProgress);
            }

            // Play the tier-complete chime exactly once each time a new tier is reached
            if (currentTier > 0 && currentTier != _lastPlayedTierSfx)
            {
                _lastPlayedTierSfx = currentTier;
                JSAM.AudioManager.PlaySound(MasterSingleton.Instance.PrefabBankManager.Charge_Drive_Strike_Tier_Complete);
            }
        }
    }

    /// <summary>
    /// Called when the player presses and holds the attack button.
    /// Begins accumulating charge. Ignored if the entity is dead or mid-attack
    /// outside of the combo window.
    /// </summary>
    public void StartCharging()
    {
        if (_health.IsDead) return;
        // Block new charge if an action is still playing and the combo window hasn't opened
        if (_animator.GetBool(HashIsAction) && (_activeMove == null || !_canAcceptComboInput)) return;
        _isCharging = true;
        _currentChargeTimer = 0f;
        _cachedCurrentTier = 0;
        _cachedChargeProgress = 0f;
        _lastPlayedTierSfx = -1;
        OnChargeStateChanged?.Invoke(0, 0f); // Reset UI immediately
    }

    /// <summary>
    /// Called when the player releases the attack button.
    /// Dispatches a light attack (tier 0) or the appropriate charged attack based on
    /// how long the button was held. Handles clinch override: charged inputs are
    /// discarded in a clinch, but a tap still triggers a clinch light attack.
    /// </summary>
    public void ReleaseCharge()
    {
        if (!_isCharging) return;

        // In a clinch, charged attacks are discarded; a quick tap still executes a clinch light attack
        if (_clinchModule != null && _clinchModule.IsClinching)
        {
            bool hasTier = CurrentTier > 0;
            _isCharging = false;
            _currentChargeTimer = 0f;
            _cachedCurrentTier = 0;
            _cachedChargeProgress = 0f;
            _lastPlayedTierSfx = -1;
            OnChargeStateChanged?.Invoke(0, 0f);
            if (hasTier) return; // Charged attack is discarded while clinching
            // Fall through so a tier-0 tap still calls ExecuteLightAttack below
        }

        _isCharging = false;

        int tier = CurrentTier;

        if (tier <= 0)
            ExecuteLightAttack();       // Quick tap → normal light attack
        else
            ExecuteChargedAttack(tier); // Held long enough → charged / special attack

        // Reset charge state and notify UI
        _currentChargeTimer = 0f;
        _cachedCurrentTier = 0;
        _cachedChargeProgress = 0f;
        _lastPlayedTierSfx = -1;
        OnChargeStateChanged?.Invoke(0, 0f);
    }

    /// <summary>
    /// Executes the charged attack corresponding to <paramref name="chargeTier"/>.
    /// Tier 1 maps to index 0, tier 2 to index 1, etc. The index is clamped so that
    /// even if the timer overshoots, the last defined charged move is used.
    /// Also activates the afterimage visual effect for the duration of the move.
    /// </summary>
    public void ExecuteChargedAttack(int chargeTier)
    {
        if (_health.IsDead) return;
        if (_animator.GetBool(HashIsAction) && (_activeMove == null || !_canAcceptComboInput)) return;

        // Convert tier to zero-based list index (Tier 1 → index 0)
        int moveIndex = chargeTier - 1;

        StartCoroutine(SpecialMoveWithAfterimage());

        if (currentStyle.chargedAttacks != null && currentStyle.chargedAttacks.Count > 0)
        {
            // Clamp to stay within bounds if the player somehow exceeds the defined tiers
            int finalIndex = Mathf.Clamp(moveIndex, 0, currentStyle.chargedAttacks.Count - 1);
            PlayMove(currentStyle.chargedAttacks[finalIndex]);
        }
    }

    public float specialMoveDuration = 1f;
    private ClinchHandler _clinchModule;

    /// <summary>
    /// Attaches an <see cref="AfterimageEffect"/> component for the duration of a
    /// charged/special move, then removes it once <see cref="specialMoveDuration"/> elapses.
    /// </summary>
    IEnumerator SpecialMoveWithAfterimage()
    {
        AfterimageEffect effect = gameObject.AddComponent<AfterimageEffect>();
        Debug.Log("Special move activated!");

        yield return new WaitForSeconds(specialMoveDuration);

        Destroy(effect);
        Debug.Log("Special move finished!");
    }

    // =========================================================================
    // Basic Attacks & Combo Chain
    // =========================================================================

    /// <summary>
    /// Executes the next light attack in the combo sequence.
    /// Resets the combo index if the player waited too long between hits.
    /// Delegates to <see cref="ClinchHandler.ExecuteClinchLightAttack"/> when clinching.
    /// </summary>
    public void ExecuteLightAttack()
    {
        if (_health.IsDead) return;
        if (_activeMove != null && !_canAcceptComboInput) return; // Block input outside the combo window

        // While clinching, light attack maps to the clinch-specific strike
        if (_clinchModule != null && _clinchModule.IsClinching)
        {
            _activeMove = currentStyle.clinchLightAtk; // Set the active move so the clinch module can drive hitboxes and audio events properly
            
            _clinchModule.ExecuteClinchLightAttack();
            return;
        }

        // Reset to the first hit if the combo window has expired
        if (Time.time - _lastAttackTime > COMBO_RESET_TIME) _comboIndex = 0;

        CombatMove move = currentStyle.lightAttacks[_comboIndex % currentStyle.lightAttacks.Length];
        PlayMove(move);
        _animator.SetBool(HashIsAction, true);

        _comboIndex++;
        _lastAttackTime = Time.time;
    }

    /// <summary>
    /// Executes the heavy attack defined in the current style.
    /// Resets the light-attack combo index on use.
    /// Delegates to <see cref="ClinchHandler.ExecuteWheelThrow"/> when clinching.
    /// </summary>
    public void ExecuteHeavyAttack()
    {
        if (_health.IsDead) return;
        if (_animator.GetBool(HashIsAction) && (_activeMove == null || !_canAcceptComboInput)) return;

        // While clinching, heavy input triggers a wheel throw instead
        if (_clinchModule != null && _clinchModule.IsClinching)
        {
            _clinchModule.ExecuteWheelThrow();
            return;
        }

        _comboIndex = 0; // Heavy attacks break the light combo sequence
        PlayMove(currentStyle.heavyAttack);
        _animator.SetBool(HashIsAction, true);
    }

    /// <summary>
    /// Executes the acrobatic flip move from the current style.
    /// Cannot be triggered while any other action is already active.
    /// Sets <see cref="_isAcrobaticMove"/> so movement code can apply special physics.
    /// </summary>
    public void ExecuteAcrobatics()
    {
        if (_health.IsDead) return;
        if (_animator.GetBool(HashIsAction)) return; // No acrobatics mid-attack

        CombatMove flipMove = currentStyle.acrobaticFlip;
        if (flipMove == null) return;

        _isAcrobaticMove = true;
        PlayMove(flipMove);
        _animator.SetBool(HashIsAction, true);
    }

    // =========================================================================
    // Core Combat Engine
    // =========================================================================

    /// <summary>
    /// Swaps the placeholder animation clip in the override controller and triggers
    /// the <c>ReplaceableAttack</c> Animator state from the beginning.
    /// Resets all per-move transient state (hitbox, hit cache, audio events).
    /// </summary>
    private void PlayMove(CombatMove move)
    {
        if (move.animationClip == null) return;

        _activeMove = move; // CombatMove satisfies IActiveCombatMove
        _hitboxActive = false;
        _canAcceptComboInput = false;
        _lastNormalizedTime = -0.01f; // Sentinel so the first FixedUpdate delta is ignored

        ClearHitCache();    // Forget targets hit by the previous swing
        ResetAudioEvents(); // Ensure audio events replay from the start of the new clip

        // Grant or deny rotation at the start of the move based on the move's settings
        _movement.canRotate = move.rotationAllowanceEnd > 0f;

        // Hot-swap the clip and restart the Animator state
        _overrideController[CLIP_SLOT_KEY] = move.animationClip;
        _animator.Play("ReplaceableAttack", 0, 0f);
        _animator.Update(0f); // Force an immediate evaluation so state data is fresh this frame
    }

    /// <summary>
    /// Restores all combat state to idle. Called by <see cref="OnAnimationStateExit"/>
    /// when the Animator signals that the current attack clip has finished.
    /// Ensures any lingering open hitbox is closed before clearing state.
    /// </summary>
    public void ResetCombatState()
    {
        if (_activeMove != null && _hitboxActive)
            CloseHitbox(GetHitboxType(_activeMove)); // Safety: close hitbox if animation ended early

        _activeMove = null;
        _hitboxActive = false;
        _canAcceptComboInput = false;
        _canRotateDuringAttack = false;
        _isAcrobaticMove = false;
        _movement.canRotate = true;              // Restore free rotation
        _animator.SetBool(HashIsAction, false);  // Allow new actions
    }

    // =========================================================================
    // Hitbox & Audio Helpers
    // =========================================================================

    /// <summary>
    /// Activates all hitboxes of the given <see cref="HitboxType"/>, stamping them
    /// with the active move's damage and reaction values before enabling collisions.
    /// </summary>
    public void OpenHitbox(int id)
    {
        HitboxType type = (HitboxType)id;
        foreach (var hb in _allHitboxes)
        {
            if (hb.hitboxType == type)
            {
                hb.SetDamage(_activeMove.Damage, _activeMove.ReactionToTrigger);
                hb.Activate();
            }
        }
    }

    /// <summary>Deactivates all hitboxes of the given <see cref="HitboxType"/>.</summary>
    public void CloseHitbox(int id)
    {
        HitboxType type = (HitboxType)id;
        foreach (var hb in _allHitboxes)
        {
            if (hb.hitboxType == type) hb.Deactivate();
        }
    }

    // Returns the hitbox type for a move; clinch attacks use Fist by default
    private static int GetHitboxType(IActiveCombatMove move)
        => move is CombatMove cm ? (int)cm.hitboxType : (int)HitboxType.Fist;

 
   

    // Called each frame by ClinchHandler while the clinch light attack animation is playing
    public void TickClinchAttack(float normalizedTime)
    {
        TickMoveState(normalizedTime);
    }

    /// <summary>
    /// Shared per-frame logic for any active move (both regular attacks and clinch attacks).
    /// Manages the hitbox open/close window, the combo input window, and audio event playback
    /// based on the animation's current <paramref name="normalizedTime"/>.
    /// </summary>
    private void TickMoveState(float normalizedTime)
    {
        // --- Hitbox window ---
        // Open/close the hitbox each frame based on the move's hit window curve
        bool shouldBeOpen = _activeMove.IsInHitWindow(normalizedTime);
        if (shouldBeOpen && !_hitboxActive)
        {
            OpenHitbox(GetHitboxType(_activeMove));
            _hitboxActive = true;
        }
        else if (!shouldBeOpen && _hitboxActive)
        {
            CloseHitbox(GetHitboxType(_activeMove));
            _hitboxActive = false;
        }

        // --- Combo window ---
        // When the combo window opens, clear isAction so the next input can register
        bool newComboState = _activeMove.IsInComboWindow(normalizedTime);
        if (newComboState && !_canAcceptComboInput)
            _animator.SetBool(HashIsAction, false);
        _canAcceptComboInput = newComboState;

        // --- Audio events ---
        UpdateAudioEvents(normalizedTime);
    }

    /// <summary>
    /// Iterates the active move's audio event list and fires any whose trigger time
    /// has been reached by <paramref name="normalizedTime"/>. Each event plays at most once per move.
    /// </summary>
    private void UpdateAudioEvents(float normalizedTime)
    {
        if (_activeMove.AudioEvents == null) return;
        for (int i = 0; i < _activeMove.AudioEvents.Length; i++)
        {
            var ev = _activeMove.AudioEvents[i];
            if (!ev.hasPlayed && normalizedTime >= ev.triggerTime)
            {
                JSAM.AudioManager.PlaySound(ev.sound);
                _activeMove.AudioEvents[i].hasPlayed = true;
            }
        }
    }

    /// <summary>
    /// Clears the <c>hasPlayed</c> flag on every audio event so they can fire again
    /// when the move is replayed (e.g. in a combo).
    /// </summary>
    private void ResetAudioEvents()
    {
        if (_activeMove?.AudioEvents == null) return;
        for (int i = 0; i < _activeMove.AudioEvents.Length; i++)
        {
            _activeMove.AudioEvents[i].hasPlayed = false;
        }
    }

    // =========================================================================
    // Defensive & KI Logic
    // =========================================================================

    /// <summary>Enters or exits the blocking stance and records the timestamp for parry window evaluation.</summary>
    public void SetBlocking(bool blocking)
    {
        _isBlocking = blocking;
        if (blocking) _lastBlockStartTime = Time.time;
        _animator.SetBool("IsBlocking", _isBlocking);
    }

    /// <summary>
    /// Routes a KI button press to the appropriate action:
    /// a parry if the player is blocking, or a power-up if idle.
    /// Requires at least 1 KI bar.
    /// </summary>
    public void HandleKIInput()
    {
        if (_kiBars < 1f) return;

        if (_isBlocking) ExecuteKIParry();
        else if (_activeMove == null) ExecuteKIPowerUp();
    }

    /// <summary>
    /// Executes a KI parry if the KI button was pressed within <see cref="KI_PARRY_WINDOW"/>
    /// seconds of the block input starting (i.e. a just-block timing window).
    /// </summary>
    private void ExecuteKIParry()
    {
        if (Time.time - _lastBlockStartTime <= KI_PARRY_WINDOW)
        {
            _kiBars -= 1f;
            _animator.Play("KI_Parry_Pose");
        }
    }

    /// <summary>Activates a KI power-up, consuming one KI bar.</summary>
    private void ExecuteKIPowerUp()
    {
        _kiBars -= 1f;
        Debug.Log("KI Power Up (Ki no chikara - 気の力)");
    }

    /// <summary>Clears the set of targets already struck by the current swing.</summary>
    public void ClearHitCache() => _hitCache.Clear();
    /// <summary>Records that <paramref name="target"/> was struck this swing to prevent duplicate hits.</summary>
    public void RegisterHit(Transform target) => _hitCache.Add(target);
    /// <summary>Returns <c>true</c> if <paramref name="target"/> was already struck this swing.</summary>
    public bool HasHitTarget(Transform target) => _hitCache.Contains(target);

    /// <summary>
    /// Implements <see cref="IAnimationStateListener"/>. Called by <see cref="AnimationStateNotifier"/>
    /// via a StateMachineBehaviour when an Animator state exits.
    /// Triggers <see cref="ResetCombatState"/> when the attack clip finishes naturally.
    /// </summary>
    public void OnAnimationStateExit(int layerIndex, AnimationExitEvent exitEvent)
    {
        if (exitEvent == AnimationExitEvent.ClipEnded)
        {
            ResetCombatState();
        }
    }
}