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
    #region Components & References

    [Header("Components")]
    private Animator _animator;
    private AnimatorOverrideController _overrideController; // Swaps animation clips at runtime without duplicating the controller asset
    private HealthComponent _health;
    private MovementComponent _movement;
    private Rigidbody _rb;
    private ClinchHandler _clinchModule;

    [Header("Data")]
    /// <summary>The active <see cref="FightingStyle"/> ScriptableObject that defines all moves for this entity.</summary>
    public FightingStyle currentStyle;

    // Animator clip slot keys — name of the placeholder clips inside the AnimatorController that get swapped at runtime
    private const string CLIP_SLOT_KEY = "Replaceable_Motion_Base";
    private const string BLOCK_CLIP_SLOT_KEY = "ReplaceableBlock";
    private const string ACROBATICS_CLIP_SLOT_KEY = "Replaceable_Base_Flip";

    // Pre-hashed Animator parameters for performance
    private readonly int HashIsAction   = Animator.StringToHash("isAction");
    private readonly int HashIsGrounded = Animator.StringToHash("b_isGrounded");
    private readonly int HashIsFalling  = Animator.StringToHash("t_isFalling");

    // Pre-baked animation clips shared with ClinchHandler to avoid repeated lookups
    public AnimationClip ClinchThrowAttackerClip    { get; private set; }
    public AnimationClip ClinchThrowVictimClip      { get; private set; }
    public AnimationClip ClinchLightAtkAttackerClip { get; private set; }
    public AnimationClip ClinchLightAtkDefenderClip { get; private set; }
    public AnimationClip BlockClip                  { get; private set; }

    /// <summary>Exposes the override controller so other systems (e.g. <see cref="ClinchHandler"/>) can swap clips.</summary>
    public AnimatorOverrideController OverrideController => _overrideController;

    #endregion

    #region Combo State

    [Header("Combo Settings")]
    private int _comboIndex = 0;          // Tracks which light attack in the sequence fires next
    private float _lastAttackTime;         // Timestamp of the last executed attack, used to detect combo expiry
    private const float COMBO_RESET_TIME = 1.2f; // Seconds of inactivity before the combo resets to the first hit

    #endregion

    #region Charge System

    [Header("Charge System (Spike Out Style)")]
    [Tooltip("Number of charges the entity has (for consecutively stronger special moves) - a move must also be added in the Fighting Style config")]
    public int ChargeCount = 1;

    private float _currentChargeTimer;       // Accumulates in seconds while the player holds the attack button; integer part = current tier
    private bool _isCharging;                // True while the button is held and a charge is building
    private int _cachedMaxCharges = -1;      // Cached value to detect style changes and fire OnMaxChargesChanged only when necessary
    private int _cachedCurrentTier = -1;     // Last-known tier, used to avoid redundant UI events
    private float _cachedChargeProgress = -1f; // Last-known fractional progress within the current tier
    private int _lastPlayedTierSfx = -1;     // Prevents the tier-complete SFX from firing more than once per tier

    /// <summary>Fires when the number of available charge tiers changes, e.g. after a style swap.</summary>
    public event Action<int> OnMaxChargesChanged;
    /// <summary>Fires every frame while charging whenever the tier or fractional progress changes appreciably.</summary>
    public event Action<int, float> OnChargeStateChanged;

    /// <summary>Total number of charged attack tiers defined by the current <see cref="FightingStyle"/>.</summary>
    public int MaxCharges => currentStyle != null && currentStyle.chargedAttacks != null ? currentStyle.chargedAttacks.Count : 0;
    /// <summary>The whole-number tier reached so far (floor of <see cref="_currentChargeTimer"/>).</summary>
    public int CurrentTier => Mathf.FloorToInt(_currentChargeTimer);
    /// <summary>Fractional progress (0–1) toward the next tier; drives the smooth UI charge bar.</summary>
    public float ChargeProgress => _currentChargeTimer % 1.0f;

    #endregion

    #region Acrobatics State

    [Header("Acrobatic Settings")]
    [Tooltip("Multiplier applied to Physics.gravity.y during the descending phase of a flip. Higher = faster/heavier landing.")]
    [Range(0.5f, 5f)]
    public float acrobaticGravityScale = 1.5f;

    public bool _isAcrobaticMove;          // Flags the active move as an acrobatic action (used by MovementComponent for special handling)
    private float _acrobaticBaseY;          // World Y at flip start; vertical curve is applied as an offset from this
    private float _acrobaticGravityVel;     // Simulated downward velocity accumulated during descent (m/s)
    private float _acrobaticGravityOffset;  // Integrated displacement from gravity (metres, always <= 0)
    private float _acrobaticPeakY;          // Highest curve Y seen so far; used to detect the descending phase
    private int _acrobaticAirFrames;        // Counts FixedUpdate ticks since launch; reserved for future use
    private bool _acrobaticLanded;          // True once the ground-check fires; prevents double-landing

    #endregion

    #region Freefall Detection

    private bool _freefallGroundCheckActive;  // True while the freefall ground-check poll is running
    private bool _isFreefalling;              // True while the entity is in confirmed freefall

    #endregion

    #region KI & Defensive State

    [Header("KI Settings")]
    private float _kiBars = 3f;                      // Current KI energy; each defensive action costs 1 bar
    private const float KI_PARRY_WINDOW = 0.2f;      // Seconds after blocking begins during which a KI parry can be triggered
    private const float BLOCK_HOLD_THRESHOLD = 0.9f; // Normalized time at which the block clip pauses via animatorSpeed = 0
    private float _lastBlockStartTime;               // Timestamp when the block input was first held down
    private bool _isBlocking;                        // Whether the entity is currently in a blocking stance
    private bool _blockAnimationPlaying;             // True while the block clip is playing (cancellation suppressed until EndBlock fires)
    private bool _blockFrozen;                       // True while animatorSpeed is 0 and the clip is held at the hold frame
    private bool _blockReleased;                     // True after ResetBlocking() so Update() does not re-freeze the clip
    private bool _blockHeld;                         // True while the block button is physically held down

    #endregion

    #region Core Combat State

    [Header("Internal State")]
    private IActiveCombatMove _activeMove;          // The move that is currently playing; null when idle
    private HashSet<Transform> _hitCache = new();   // Entities already struck by the current swing; prevents multi-hit on a single swing
    private CombatHitbox[] _allHitboxes;            // All child hitboxes, cached in Awake
    private bool _hitboxActive;                     // Whether a hitbox is currently open this frame
    private bool _canAcceptComboInput;              // True during the combo window so the next attack can chain seamlessly

    [Header("Motion State")]
    private float _lastNormalizedTime;       // normalizedTime from the previous FixedUpdate tick; used to compute per-frame root-motion delta
    private bool _canRotateDuringAttack;     // Derived each frame from the active move's rotation curve; exposed to MovementComponent

    public float specialMoveDuration = 1f;

    #endregion

    #region Public State Accessors

    public bool CanRotateDuringAttack => _canRotateDuringAttack; // True during the portion of an attack where the entity may turn
    public bool IsAttacking    => _activeMove != null;           // True whenever any move is in progress
    public bool IsAcrobatic    => _isAcrobaticMove;              // True while an acrobatic move is executing
    public bool IsCharging     => _isCharging;                   // True while the charge button is held
    public bool IsBlocking     => _isBlocking;                   // True while the entity is in a blocking stance
    public bool IsFreefalling  => _isFreefalling;                // True while the entity is in confirmed freefall

    #endregion

    #region Initialisation

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _health   = GetComponent<HealthComponent>();
        _movement = GetComponent<MovementComponent>();
        _rb       = GetComponent<Rigidbody>();

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

        // Force-release a frozen block if this entity dies while the hold is active
        _health.OnDeath += ForceReleaseBlock;
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

        // Pre-bake block clip from the current style
        BlockClip = currentStyle != null ? currentStyle.blockClip : null;
                
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

    #endregion

    #region Unity Lifecycle (Update / FixedUpdate)

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

        // --- Block hold tick ---
        // Once the block clip reaches BLOCK_HOLD_THRESHOLD, pause it via animatorSpeed = 0.
        // The pose holds until ResetBlocking() restores animatorSpeed to 1, or ForceReleaseBlock
        // is called on death.
        if (_blockAnimationPlaying)
        {
            AnimatorStateInfo blockState = _animator.GetCurrentAnimatorStateInfo(0);
            if (blockState.IsName("ReplaceableBlock"))
            {
                if (!_blockFrozen && !_blockReleased && _blockHeld && blockState.normalizedTime >= BLOCK_HOLD_THRESHOLD)
                {
                    _blockFrozen = true;
                    _animator.SetFloat("animatorSpeed", 0f);
                }
                return; // Skip attack ticking while block is active
            }
        }

        if (_activeMove == null) return;

        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        bool inAttackState = stateInfo.IsName("ReplaceableAttack") || stateInfo.IsName("ReplaceableAcrobatics");
        if (!inAttackState) return; // Only process while an active move state is playing

        float currentTime = stateInfo.normalizedTime;

        TickMoveState(currentTime);

        // --- Rotation allowance ---
        // Let the move's curve dictate whether the entity can turn mid-attack
        _canRotateDuringAttack = _activeMove is CombatMove cm && cm.CanRotate(currentTime);
        _movement.canRotate = _canRotateDuringAttack;
    }

    private void FixedUpdate()
    {
        TickFreefallDetection();

        if (_activeMove == null) return;

        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("ReplaceableAttack") || stateInfo.IsName("ReplaceableAcrobatics"))
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



            // For acrobatic moves, blend the animation curve's vertical arc with
            // simulated gravity so the descent feels natural instead of abrupt.
            if (_isAcrobaticMove && _activeMove is CombatMove acroMove)
            {
                float curveY = acroMove.EvaluateVerticalPosition(currentTime);

                // Track the highest point the curve has reached
                if (curveY > _acrobaticPeakY)
                    _acrobaticPeakY = curveY;

                // Once past the apex, accumulate downward velocity and integrate
                // it into a displacement offset that pulls the character down.
                bool descending = curveY < _acrobaticPeakY - 0.01f;
                if (descending)
                {
                    // v += g * scale * dt  (gravity.y is negative)
                    _acrobaticGravityVel += Physics.gravity.y * acrobaticGravityScale * Time.fixedDeltaTime;
                    // displacement += v * dt
                    _acrobaticGravityOffset += _acrobaticGravityVel * Time.fixedDeltaTime;
                }

                // Combine curve offset with accumulated gravity displacement
                float combinedY = _acrobaticBaseY + curveY + _acrobaticGravityOffset;

                // Never go below the starting ground level
                combinedY = Mathf.Max(combinedY, _acrobaticBaseY);

                Vector3 pos = transform.position;
                pos.y = combinedY;
                transform.position = pos;
            }



            /*


            // Ground-check: only active once ToggleAcrobaticGroundCheck(true) has been called.
            // SphereCasts downward each FixedUpdate until the floor layer is detected,
            // at which point landing cleanup fires and the acrobatic state is cleared.
            // If the floor is NOT detected the flip has gone over an edge — enter freefall.
            if (_freefallGroundCheckActive && _isAcrobaticMove && !_acrobaticLanded)
            {   
                const float castRadius = 0.2f;
                const float castDistance = 0.1f;
                bool hitGround = Physics.SphereCast(
                    transform.position + Vector3.up * castRadius,
                    castRadius,
                    Vector3.down,
                    out _,
                    castDistance,
                    _health.floorLayer,
                    QueryTriggerInteraction.Ignore);

                if (hitGround)
                {   
                    OnAcrobaticLanded();
                }
                else if (!_isFreefalling)
                {
                    // Flip missed the floor — transition into confirmed freefall.
                    // Preserve the current momentum so the character keeps moving
                    // along the same trajectory instead of stopping dead mid-air.
                    if (_rb != null)
                    {
                        // Forward velocity from the motion curve (approximate via last delta)
                        float forwardSpeed = 0f;
                        if (_activeMove is CombatMove fallMove && _lastNormalizedTime >= 0 && currentTime > _lastNormalizedTime)
                        {
                            float dt = Time.fixedDeltaTime;
                            if (dt > 0f)
                                forwardSpeed = fallMove.EvaluateMotionDelta(_lastNormalizedTime, currentTime) / dt;
                        }

                        // Downward velocity from the gravity simulation
                        float downSpeed = _acrobaticGravityVel;

                        _rb.linearVelocity = transform.forward * forwardSpeed + Vector3.up * downSpeed;
                    }

                    _isAcrobaticMove = false;
                    _isFreefalling = true;
                    _movement.isInFlight = true; // Prevent MovementComponent from zeroing velocity
                    _animator.SetBool(HashIsGrounded, false);
                    _animator.SetTrigger(HashIsFalling);
                    _animator.SetBool(HashIsAction, true);
                }
            }
            */

            _lastNormalizedTime = currentTime;
        }
    }

    #endregion

    #region Freefall Detection

    /// <summary>
    /// Runs every FixedUpdate while <see cref="_freefallGroundCheckActive"/> is true.
    /// SphereCasts against <see cref="HealthComponent.floorLayer"/> each physics tick.
    /// When airborne the freefall trigger fires immediately; once the floor is detected
    /// the state is cleaned up via <see cref="OnFreefallLanded"/>.
    /// </summary>
    private void TickFreefallDetection()
    {
        if (!_freefallGroundCheckActive) return;
        if (_health.IsDead) return;
        // if (_isAcrobaticMove) return; // Acrobatic ground-check in FixedUpdate owns this while a flip is active

        _movement.isInFlight = true;

        const float castRadius = 0.2f;
        const float castDistance = 0.6f;
        bool onFloor = Physics.SphereCast(
            transform.position + Vector3.up * castRadius,
            castRadius,
            Vector3.down,
            out _,
            castDistance,
            _health.floorLayer,
            QueryTriggerInteraction.Ignore);

        
        if (!onFloor)
        {
            if (!_isFreefalling)
            {                
                _isFreefalling = true;
                _movement.isInFlight = true; // Prevent MovementComponent from zeroing velocity
                _animator.SetTrigger(HashIsFalling);
                _animator.SetBool(HashIsAction, true);
            }
        }
        else if (_isFreefalling)
        {
            _movement.isInFlight = false;

            OnFreefallLanded();
        }
    }

    /// <summary>
    /// Called when the floor is detected after a confirmed freefall.
    /// Restores all freefall-related state and clears the action lock.
    /// </summary>
    private void OnFreefallLanded()
    {
        _isFreefalling = false;
        _movement.isInFlight = false;

        // Zero residual velocity so the character doesn't slide after landing
        
        _rb.linearVelocity = Vector3.zero;

       

        _animator.ResetTrigger(HashIsFalling);
        _animator.SetBool(HashIsGrounded, true);
        _animator.SetBool(HashIsAction, false);
        NotifyDust(); // Land with a dust effect    
        ResetCombatState(); 
    }

    #endregion

    #region Charge Logic

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

        _animator.SetBool(HashIsAction, true);
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
        _animator.SetBool(HashIsAction, false); 
        Debug.Log("Special move finished!");
    }

    #endregion

    #region Attacks & Combo Chain

    /// <summary>
    /// Executes the next light attack in the combo sequence.
    /// Resets the combo index if the player waited too long between hits.
    /// Delegates to <see cref="ClinchHandler.ExecuteClinchLightAttack"/> when clinching.
    /// </summary>
    public void ExecuteLightAttack()
    {
        if (_health.IsDead) return;
        if (_activeMove != null && !_canAcceptComboInput) return; // Block input outside the combo window
        if (IsBlocking) return; // Cannot attack while blocking

        // While clinching, light attack maps to the clinch-specific strike
        if (_clinchModule != null && _clinchModule.IsClinching)
        {
            if (_clinchModule.IsExecutingThrow) return;

            _activeMove = currentStyle.clinchLightAtk; // Set the active move so the clinch module can drive hitboxes and audio events properly
            
            _clinchModule.ExecuteClinchLightAttack();
            return;
        }

        // Reset to the first hit if the combo window has expired
        if (Time.time - _lastAttackTime > COMBO_RESET_TIME) _comboIndex = 0;

        CombatMove move = currentStyle.lightAttacks[_comboIndex % currentStyle.lightAttacks.Length];
        PlayMove(move);
        _animator.SetBool(HashIsAction, true);

        // ~1-in-5 chance to play a light attack cry
        if (_health.characterEffects != null
            && _health.characterEffects.sfxLightAttackCry != null
            && UnityEngine.Random.Range(0, 5) == 0)
        {
            JSAM.AudioManager.PlaySound(_health.characterEffects.sfxLightAttackCry);
        }

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
        if (IsBlocking) return; // Cannot attack while blocking

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
        if (IsBlocking) return; // Cannot attack while blocking

        CombatMove flipMove = currentStyle.acrobaticFlip;
        if (flipMove == null) return;

        _isAcrobaticMove = true;
        _acrobaticBaseY = transform.position.y;
        _acrobaticGravityVel = 0f;
        _acrobaticGravityOffset = 0f;
        _acrobaticPeakY = 0f;
        _acrobaticAirFrames = 0;
        _acrobaticLanded = false;
        _animator.applyRootMotion = false;
        StartCoroutine(FlipWithAfterimage(flipMove));
        PlayMove(flipMove, isAcrobatic: true);
        _animator.SetBool(HashIsAction, true);
        _animator.SetBool(HashIsGrounded, false );
        
    }

    /// <summary>
    /// Attaches a <see cref="FlipAfterimageEffect"/> for the duration of the flip animation,
    /// producing a white, semi-transparent ghost trail that is more spread out than the
    /// charged-attack afterimage.
    /// </summary>
    private IEnumerator FlipWithAfterimage(CombatMove flipMove)
    {
        FlipAfterimageEffect effect = gameObject.AddComponent<FlipAfterimageEffect>();

        float duration = flipMove.animationClip != null ? flipMove.animationClip.length * 0.8f : specialMoveDuration;
        yield return new WaitForSeconds(duration);

        Destroy(effect);
    }

    #endregion

    #region Core Combat Engine

    /// <summary>
    /// Swaps the placeholder animation clip in the override controller and triggers
    /// the appropriate Animator state from the beginning.
    /// Uses <c>ReplaceableAcrobatics</c> and <see cref="ACROBATICS_CLIP_SLOT_KEY"/> when
    /// <paramref name="isAcrobatic"/> is <c>true</c>; otherwise uses <c>ReplaceableAttack</c>
    /// and <see cref="CLIP_SLOT_KEY"/>.
    /// Resets all per-move transient state (hitbox, hit cache, audio events).
    /// </summary>
    private void PlayMove(CombatMove move, bool isAcrobatic = false)
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

        // Hot-swap the clip and restart the correct Animator state
        if (isAcrobatic)
        {
            _overrideController[ACROBATICS_CLIP_SLOT_KEY] = move.animationClip;
            _animator.Play("ReplaceableAcrobatics", 0, 0f);
        }
        else
        {
            _overrideController[CLIP_SLOT_KEY] = move.animationClip;
            _animator.Play("ReplaceableAttack", 0, 0f);
        }
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
        _blockAnimationPlaying = false;
        _blockFrozen = false;
        _blockReleased = false;
        _blockHeld = false;
        _isBlocking = false;
        _canAcceptComboInput = false;
        _canRotateDuringAttack = false;
        _isAcrobaticMove = false;
        _isFreefalling = false;
        _freefallGroundCheckActive = false;
        _movement.isInFlight = false;
        _movement.canRotate = true;
        _animator.applyRootMotion = true;
        _animator.SetFloat("animatorSpeed", 1f);
        _animator.ResetTrigger(HashIsFalling);
        _animator.SetBool(HashIsAction, false);
    }

    #endregion

    #region Hitbox & Audio

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
        if (_activeMove == null) return;
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

    #endregion

    #region Defensive & KI Logic

    /// <summary>
    /// Enters or exits the blocking stance.
    /// On press: hot-swaps the block clip into ReplaceableBlock and plays it from the start.
    /// The clip runs freely until it reaches <see cref="BLOCK_HOLD_THRESHOLD"/> (0.9 normalizedTime),
    /// at which point Update() sets <c>animatorSpeed</c> to 0, freezing the pose.
    /// On release before the threshold: input is suppressed — the clip must reach the hold frame first.
    /// On release at or after the hold frame: call <see cref="ResetBlocking"/> directly (player via
    /// input callback, enemy AI via CombatActorBrain), which restores animatorSpeed so the final
    /// 10% plays through and EndBlock fires from the AnimationStateNotifier.
    /// </summary>
    public void SetBlocking(bool blocking)
    {
        if(_clinchModule.IsClinching) return; //not available during clinch (yet)

        if (blocking)
        {
            // Cannot block while attacking or already mid-block
            if (_activeMove != null || _isBlocking) return;
            if (BlockClip == null) return;

            _isBlocking = true;
            _blockAnimationPlaying = true;
            _blockFrozen = false;
            _blockHeld = true;
            _lastBlockStartTime = Time.time;

            _animator.SetBool(HashIsAction, true);
            _animator.SetFloat("animatorSpeed", 1f);

            _overrideController[BLOCK_CLIP_SLOT_KEY] = BlockClip;
            _animator.Play("ReplaceableBlock", 0, 0f);
            _animator.Update(0f);
        }
        else
        {
            // Suppress release until the hold frame has been reached;
            // once frozen, ResetBlocking() is the correct unfreeze path.
            if (_blockAnimationPlaying) return;
        }
    }

    /// <summary>
    /// Unfreezes the block clip so its final 10% plays through to completion.
    /// Called by <see cref="PlayerController"/> when the block button is released, and by
    /// <see cref="CombatActorBrain"/> for enemy-controlled entities.
    /// Has no effect if the clip has not yet reached the hold frame (the hold threshold
    /// acts as a minimum committed block duration).
    /// </summary>
    public void ResetBlocking()
    {
        _blockHeld = false;                      // Mark the button as released

        if (!_blockFrozen)
        {
            // Clip hasn't reached the hold frame yet — it will now play through
            // to the end naturally because _blockHeld is false, so Update()
            // will skip the freeze at BLOCK_HOLD_THRESHOLD.
            _blockReleased = true;
            return;
        }

        _blockReleased = true;                   // Prevent Update() from re-freezing the clip
        _blockFrozen = false;
        _isBlocking = false;                     // No longer absorbing hits from this point
        _animator.SetFloat("animatorSpeed", 1f); // Resume playback of the final 10%
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
    /// Immediately clears all block state and restores animatorSpeed.
    /// Subscribed to <see cref="HealthComponent.OnDeath"/> so a frozen block is
    /// always cleaned up even if the entity dies while holding the block pose.
    /// </summary>
    private void ForceReleaseBlock()
    {
        if (!_blockFrozen && !_blockAnimationPlaying) return;

        _blockFrozen = false;
        _blockReleased = false;
        _blockHeld = false;
        _blockAnimationPlaying = false;
        _isBlocking = false;
        _animator.SetFloat("animatorSpeed", 1f);

        if (_movement != null)
            _movement.isMovementLocked = false;
    }

    #endregion

    #region Animation State Callbacks

    /// <summary>
    /// Called when the ground-check SphereCast detects the Floor layer after a flip.
    /// Restores physics, animator state, and combat state — replacing the old
    /// <see cref="AnimationExitEvent.EndAcrobatics"/> path which no longer drives landing.
    /// </summary>
    private void OnAcrobaticLanded()
    {
        //seems to only be working on interrupted flip landings so far. 
        Debug.Break();
        _acrobaticLanded = true;
        _isAcrobaticMove = false;
        _animator.SetBool(HashIsGrounded, true);
        NotifyDust(); // Play dust effect on landing    

        ResetCombatState();
    }

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
        else if (exitEvent == AnimationExitEvent.EndBlock)
        {
            _blockAnimationPlaying = false;
            _blockFrozen = false;
            _blockReleased = false;
            _blockHeld = false;
            _isBlocking = false;
            _animator.SetFloat("animatorSpeed", 1f);
            _animator.SetBool(HashIsAction, false);

            if (_movement != null)
                _movement.isMovementLocked = false;
        }
        else if(exitEvent == AnimationExitEvent.EndAcrobatics)
        {
            // Guard: if the ground-check already triggered a landing (OnFreefallLanded
            // set _freefallGroundCheckActive to false via ResetCombatState), the entity
            // has already landed cleanly — do not restart freefall state.
            if (!_freefallGroundCheckActive)
                return;

            _isAcrobaticMove = false;
            _isFreefalling = true;
        }
    }

    public void NotifyDust()
    {
        //probably just temporary until we have a better system for mid-animation events,
        //but for now this lets us trigger the dust effect at the right time during the
        //attack animation without needing to create a separate state or animation event for it.  
        _movement.dustParticles.Play(); // Add a dust effect when landing from the flip

    }

    /// <summary>
    /// Enables or disables the freefall ground-check poll.
    /// Pass <c>true</c> when the entity leaves the ground (e.g. after a flip that misses the floor,
    /// or any other airborne state); pass <c>false</c> to cancel it when grounded state is restored
    /// by another system.
    /// </summary>
    public void ToggleAcrobaticGroundCheck(bool active)
    {
        //at 0.9 normalized time in the flip animation, the ground-check becomes active. If the SphereCast detects the floor, the landing sequence triggers; if not, the character enters freefall.
        _freefallGroundCheckActive = active;
        if (!active)
        {
            _isFreefalling = false;
            _animator.ResetTrigger(HashIsFalling);
        }
    }

    #endregion
}