using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central combat controller for an entity. Manages attack execution (light, heavy,
/// charged, acrobatic, clinch), hitbox lifecycle, combo chaining, and motion-root
/// translation driven by animation curves.
/// Delegates charge logic to <see cref="ChargeHandler"/>, blocking/KI to
/// <see cref="BlockHandler"/>, and strike trail VFX to <see cref="StrikeTrailManager"/>.
/// Uses a <see cref="CombatState"/> state machine for clarity.
/// Implements <see cref="IAnimationStateListener"/> so the Animator can signal when a
/// clip has finished, allowing <see cref="ResetCombatState"/> to clean up automatically.
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class CombatHandler : MonoBehaviour, IAnimationStateListener
{
    #region Components & References

    [Header("Components")]
    private Animator _animator;
    private AnimatorOverrideController _overrideController;
    private HealthComponent _health;
    private MovementComponent _movement;
    private Rigidbody _rb;
    private Collider _collider;
    private ClinchHandler _clinchModule;
    private SwordFightingHandler _swordModule;
    private NunchakuHandler _nunchakuModule;
    private StaffFightingHandler _staffModule;
    private ThrownBombHandler _thrownWeaponModule;
    private ThrownShurikenHandler _shurikenModule;
    private WeaponEventRelay _weaponRelay;

    /// <summary>Subsystem: charge/tier meter.</summary>
    private ChargeHandler _charge;
    /// <summary>Subsystem: blocking and KI defensive actions.</summary>
    private BlockHandler _block;
    /// <summary>Subsystem: per-limb strike trail VFX.</summary>
    private StrikeTrailManager _strikeTrails;

    [Header("Data")]
    public FightingStyle currentStyle;
    private FightingStyle _defaultStyle;
    private int _equippedItemCount;
    private ItemData _equippedItem;

    private const string CLIP_SLOT_KEY = "Replaceable_Motion_Base";
    private const string ACROBATICS_CLIP_SLOT_KEY = "Replaceable_Base_Flip";

    private readonly int HashIsAction   = Animator.StringToHash("isAction");
    private readonly int HashIsGrounded = Animator.StringToHash("b_isGrounded");
    private readonly int HashIsFalling  = Animator.StringToHash("t_isFalling");

    public AnimationClip ClinchThrowAttackerClip    { get; private set; }
    public AnimationClip ClinchThrowVictimClip      { get; private set; }
    public AnimationClip ClinchLightAtkAttackerClip { get; private set; }
    public AnimationClip ClinchLightAtkDefenderClip { get; private set; }
    public AnimationClip BlockClip                  { get; private set; }

    public AnimatorOverrideController OverrideController => _overrideController;

    #endregion

    #region State Machine

    [Header("State")]
    private CombatState _state = CombatState.Idle;

    /// <summary>Current high-level combat state.</summary>
    public CombatState State => _state;

    #endregion

    #region Combo State

    private int _comboIndex;

    #endregion

    #region Charge & Event Accessors

    /// <summary>Fired when a move's hitbox opens. Passes the active <see cref="CombatMove"/>.</summary>
    public event Action<CombatMove> OnHitboxOpened;
    /// <summary>Fired when the active hitbox closes (hit window ended, move reset, or interrupted).</summary>
    public event Action OnHitboxClosed;

    /// <summary>Proxy: subscribe to charge events via the subsystem.</summary>
    public event Action<int> OnMaxChargesChanged
    {
        add    => _charge.OnMaxChargesChanged += value;
        remove => _charge.OnMaxChargesChanged -= value;
    }
    public event Action<int, float> OnChargeStateChanged
    {
        add    => _charge.OnChargeStateChanged += value;
        remove => _charge.OnChargeStateChanged -= value;
    }

    public int MaxCharges => _charge.MaxCharges;
    public int CurrentTier => _charge.CurrentTier;
    public float ChargeProgress => _charge.ChargeProgress;

    #endregion

    #region Acrobatics State

    [Header("Acrobatic Settings")]
    [Tooltip("Vertical impulse applied when the acrobatic flip starts.")]
    public float jumpForce = 8.5f;
    [Tooltip("Forward impulse applied when the acrobatic flip starts.")]
    public float forwardForce = 6.0f;
    [Tooltip("Extra gravity multiplier during the acrobatic arc. Higher = faster/snappier jump while keeping the same shape. 1 = normal gravity.")]
    [Range(1f, 5f)]
    public float acrobaticGravityScale = 1.8f;
    [Tooltip("Playback speed of the acrobatic flip animation.")]
    [Range(1.5f, 10f)]
    public float acrobaticSpeed = 5f;

    [HideInInspector] public bool _isAcrobaticMove; // kept public for AnimationStateNotifier compatibility

    /// <summary>Forward speed captured at launch so momentum can be carried into freefall.</summary>
    private float _acrobaticForwardSpeed;

    #endregion

    #region Freefall Detection

    /// <summary>Cached floor layer index derived from <see cref="HealthComponent.floorLayer"/>.</summary>
    private int _floorLayerIndex = -1;

    /// <summary>
    /// True while the entity's collider is touching anything on the Floor layer.
    /// Updated by <see cref="OnCollisionStay"/> and <see cref="OnCollisionExit"/>.
    /// </summary>
    private bool _isTouchingFloor;

    /// <summary>
    /// Timestamp of the last confirmed floor contact. Used for coyote-time so
    /// <see cref="ExecuteAcrobatics"/> can still fire a short window after leaving a ledge.
    /// </summary>
    private float _lastGroundedTime;

    /// <summary>How long after leaving the ground the acrobatic jump is still permitted.</summary>
    private const float COYOTE_TIME = 0.15f;

    /// <summary>
    /// Number of FixedUpdate ticks spent in Freefall without floor contact.
    /// The falling animation only triggers after a short grace period to avoid
    /// single-frame false positives.
    /// </summary>
    private int _freefallGraceTicks;
    private const int FREEFALL_GRACE_TICKS = 3;

    /// <summary>
    /// Realtime timestamp set when freefall begins. Landing is ignored until
    /// at least <see cref="MIN_FREEFALL_AIRTIME"/> seconds have elapsed, preventing
    /// an instant landing resolution when the entity is still touching the floor
    /// at the moment of launch (e.g. sword leap-back).
    /// </summary>
    private float _freefallMinAirtime;
    private const float MIN_FREEFALL_AIRTIME = 0.5f;

    /// <summary>True when the current freefall was initiated by <see cref="ExecuteSwordLeapBack"/>,
    /// so the extra gravity multiplier from <see cref="FightingStyle.leapBackGravityScale"/> is applied.</summary>
    private bool _isLeapBackFreefall;

    #endregion
    // KI & Defensive state is managed by BlockHandler (_block).

    #region Core Combat State

    private IActiveCombatMove _activeMove;
    private HashSet<Transform> _hitCache = new();
    private CombatHitbox[] _allHitboxes;
    private bool _hitboxActive;
    private bool _canAcceptComboInput;

    private float _lastNormalizedTime;
    private bool _canRotateDuringAttack;

    public float specialMoveDuration = 1f;

    #endregion

    // Strike trail state is managed by StrikeTrailManager (_strikeTrails).

    #region Public State Accessors

    public bool CanRotateDuringAttack => _canRotateDuringAttack;
    public bool IsAttacking    => _activeMove != null;
    public bool IsAcrobatic    => _state == CombatState.Acrobatic;
    public bool IsCharging     => _charge != null && _charge.IsCharging;
    public bool IsBlocking     => _block != null && _block.IsBlocking;
    public bool IsFreefalling  => _state == CombatState.Freefall;
    public bool IsDrawingWeapon => _state == CombatState.DrawingWeapon;

    #endregion

    #region Initialisation

    private void Awake()
    {
        _animator  = GetComponent<Animator>();
        _health    = GetComponent<HealthComponent>();
        _movement  = GetComponent<MovementComponent>();
        _rb        = GetComponent<Rigidbody>();
        _collider  = GetComponent<Collider>();

        _defaultStyle = currentStyle;

        _overrideController = new AnimatorOverrideController(_animator.runtimeAnimatorController);
        _animator.runtimeAnimatorController = _overrideController;

        _animator.updateMode = AnimatorUpdateMode.Fixed;

        _allHitboxes = GetComponentsInChildren<CombatHitbox>();

        // Derive a single layer index from the floor LayerMask for OnCollision checks
        _floorLayerIndex = LayerMaskToIndex(_health.floorLayer);

        _charge = new ChargeHandler(this);
        _strikeTrails = new StrikeTrailManager(_animator, _health);
    }

    private void Start()
    {
        if (gameObject.CompareTag("Player"))
        {
            UIChargeDisplay playerUI = MasterSingleton.Instance.UIManager.chargeMeter;
            playerUI.SetTarget(this);
        }
        _block = new BlockHandler(this, _animator, _overrideController, _movement, _health);
        InitializeStyleModules();

        _health.OnDeath += _block.ForceRelease;
    }

    /// <summary>
    /// Swaps the active <see cref="FightingStyle"/> at runtime (e.g. on weapon pickup).
    /// Resets combat state cleanly before re-initialising all style-dependent modules.
    /// </summary>
    public void EquipStyle(FightingStyle newStyle)
    {
        currentStyle = newStyle;
        ResetCombatState();
        InitializeStyleModules();
    }

    /// <summary>
    /// Equips a weapon from an <see cref="ItemData"/>, using its <c>count</c> to seed
    /// thrown-weapon handlers.
    /// </summary>
    public void EquipStyle(ItemData item)
    {
        _equippedItemCount = item != null ? item.count : 0;
        _equippedItem = item;
        EquipStyle(item != null ? item.fightingStyle : null);
    }

    /// <summary>
    /// Immediately reverts to the default (fist) style that was set in the Inspector.
    /// Called by <see cref="HealthComponent"/> on death before any death animations play.
    /// </summary>
    public void RevertToDefaultStyle()
    {
        EquipStyle(_defaultStyle);
    }

    private void InitializeStyleModules()
    {
        // Reset all non-default animator layers to 0 whenever a new style is activated.
        for (int i = 1; i < _animator.layerCount; i++)
            _animator.SetLayerWeight(i, 0f);

        if (currentStyle != null && currentStyle.clinchThrowDefault != null)
        {
            ClinchThrowAttackerClip = currentStyle.clinchThrowDefault.attackerThrowClip;
            ClinchThrowVictimClip   = currentStyle.clinchThrowDefault.victimThrowClip;
        }
        else
        {
            ClinchThrowAttackerClip = null;
            ClinchThrowVictimClip   = null;
        }

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

        BlockClip = currentStyle != null ? currentStyle.blockClip : null;

        FightingStyleType type = currentStyle != null ? currentStyle.styleType : FightingStyleType.MeleeNoClinch;

        // Clinch
        if (type == FightingStyleType.MeleeAndClinch)
        {
            if (!TryGetComponent<ClinchHandler>(out _clinchModule))
                _clinchModule = gameObject.AddComponent<ClinchHandler>();
            _clinchModule.Initialize(this);
        }
        else
        {
            if (TryGetComponent<ClinchHandler>(out var old))
                Destroy(old);
            _clinchModule = null;
        }

        // Sword Fighting
        if (type == FightingStyleType.SwordFighting)
        {
            if (!TryGetComponent<SwordFightingHandler>(out _swordModule))
                _swordModule = gameObject.AddComponent<SwordFightingHandler>();
            _swordModule.Initialize(this);
        }
        else
        {
            if (TryGetComponent<SwordFightingHandler>(out var old))
            {
                old.Teardown();
                Destroy(old);
            }
            _swordModule = null;
        }

        // Nunchaku
        if (type == FightingStyleType.Nunchaku)
        {
            if (!TryGetComponent<NunchakuHandler>(out _nunchakuModule))
                _nunchakuModule = gameObject.AddComponent<NunchakuHandler>();
            _nunchakuModule.Initialize(this);
        }
        else
        {
            if (TryGetComponent<NunchakuHandler>(out var old))
                Destroy(old);
            _nunchakuModule = null;
        }

        // Staff Fighting
        if (type == FightingStyleType.StaffFighting)
        {
            if (!TryGetComponent<StaffFightingHandler>(out _staffModule))
                _staffModule = gameObject.AddComponent<StaffFightingHandler>();
            _staffModule.Initialize(this);
        }
        else
        {
            if (TryGetComponent<StaffFightingHandler>(out var old))
                Destroy(old);
            _staffModule = null;
        }

        // Thrown Weapon (Bomb)
        if (type == FightingStyleType.ThrownWeaponBomb)
        {
            if (!TryGetComponent<ThrownBombHandler>(out _thrownWeaponModule))
                _thrownWeaponModule = gameObject.AddComponent<ThrownBombHandler>();
            _thrownWeaponModule.Initialize(this, _equippedItemCount);
        }
        else
        {
            if (TryGetComponent<ThrownBombHandler>(out var old))
                Destroy(old);
            _thrownWeaponModule = null;
        }

        // Thrown Weapon (Shuriken)
        if (type == FightingStyleType.ThrownWeaponShuriken)
        {
            if (!TryGetComponent<ThrownShurikenHandler>(out _shurikenModule))
                _shurikenModule = gameObject.AddComponent<ThrownShurikenHandler>();
            _shurikenModule.Initialize(this, _equippedItemCount);
        }
        else
        {
            if (TryGetComponent<ThrownShurikenHandler>(out var old))
            {
                old.Teardown();
                Destroy(old);
            }
            _shurikenModule = null;
        }

        // Weapon Event Relay — bind to whichever weapon handler is active
        IWeaponHandler activeWeapon = (IWeaponHandler)_swordModule
                                   ?? (IWeaponHandler)_nunchakuModule
                                   ?? (IWeaponHandler)_staffModule
                                   ?? (IWeaponHandler)_thrownWeaponModule
                                   ?? (IWeaponHandler)_shurikenModule;

        if (activeWeapon != null)
        {
            if (_weaponRelay == null && !TryGetComponent(out _weaponRelay))
                _weaponRelay = gameObject.AddComponent<WeaponEventRelay>();
            _weaponRelay.Bind(activeWeapon);

            BeginDrawWeapon(activeWeapon);
        }
        else
        {
            if (_weaponRelay != null)
                _weaponRelay.Unbind();
        }
    }

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

    /// <summary>
    /// Initiates the draw-weapon sequence for <paramref name="handler"/>.
    /// If the entity is currently attacking the draw is deferred until the attack
    /// resolves via a coroutine, then locks input for the full draw animation.
    /// </summary>
    private void BeginDrawWeapon(IWeaponHandler handler)
    {
        if (handler == null) return;
        StartCoroutine(DrawWeaponWhenReady(handler));
    }

    private IEnumerator DrawWeaponWhenReady(IWeaponHandler handler)
    {
        // Wait until we're fully idle (not mid-attack, not mid-draw, not blocking).
        yield return new WaitUntil(() =>
            _state == CombatState.Idle && !_health.IsDead);

        _state = CombatState.DrawingWeapon;
        _movement.canRotate = true;   // rotation is permitted during the draw
        _animator.SetBool(HashIsAction, true);

        if (_equippedItem != null && _equippedItem.drawSound != null)
            JSAM.AudioManager.PlaySound(_equippedItem.drawSound);

        handler.PlayDrawAnimation();
    }

    #endregion

    // Strike trail initialisation and management is in StrikeTrailManager.


    #region Collision-Based Floor Detection

    private void OnCollisionStay(Collision collision)
    {
        if (_floorLayerIndex >= 0 && collision.gameObject.layer == _floorLayerIndex)
        {
            _isTouchingFloor = true;
            _lastGroundedTime = Time.time;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (_floorLayerIndex >= 0 && collision.gameObject.layer == _floorLayerIndex)
            _isTouchingFloor = false;
    }

    #endregion

    #region Unity Lifecycle (Update / FixedUpdate)

    private void Update()
    {
        _charge.Tick();

        // --- Block hold tick ---
        if (_state == CombatState.Blocking && _block.TickBlockHold())
            return;

        // Freefall blocks all other ticking
        if (_state == CombatState.Freefall) return;

        if (_activeMove == null) return;

        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        bool inAttackState = stateInfo.IsName("ReplaceableAttack") || stateInfo.IsName("ReplaceableAcrobatics");
        if (!inAttackState) return;

        float currentTime = stateInfo.normalizedTime;

        TickMoveState(currentTime);

        _canRotateDuringAttack = _activeMove is CombatMove cm && cm.CanRotate(currentTime);
        _movement.canRotate = _canRotateDuringAttack;
    }

    private void FixedUpdate()
    {
        // --- Freefall state machine ---
        if (_state == CombatState.Freefall)
        {
            if (_isLeapBackFreefall && currentStyle != null && currentStyle.leapBackGravityScale > 1f)
            {
                float extraGravity = Physics.gravity.y * (currentStyle.leapBackGravityScale - 1f);
                _rb.linearVelocity += new Vector3(0f, extraGravity * Time.fixedDeltaTime, 0f);
            }
            TickFreefall();
            return;
        }

        if (_activeMove == null) return;

        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName("ReplaceableAttack") && !stateInfo.IsName("ReplaceableAcrobatics"))
            return;

        float currentTime = stateInfo.normalizedTime;

        // Forward root-motion via animation curve (non-acrobatic moves only;
        // acrobatic movement is fully physics-driven via Rigidbody velocity).
        if (_state != CombatState.Acrobatic
            && currentTime > _lastNormalizedTime && _lastNormalizedTime >= 0)
        {
            if (_activeMove is CombatMove moveCast)
            {
                float deltaDistance = moveCast.EvaluateMotionDelta(_lastNormalizedTime, currentTime);
                if (deltaDistance > 0)
                    transform.position += transform.forward * deltaDistance;
            }
        }

        // Apply extra gravity while airborne to make the arc snappier.
        if (_state == CombatState.Acrobatic)
        {
            float extraGravity = Physics.gravity.y * (acrobaticGravityScale - 1f);
            _rb.linearVelocity += new Vector3(0f, extraGravity * Time.fixedDeltaTime, 0f);
        }

        // Acrobatic: detect when the entity starts falling and transition to freefall,
        // capturing forward momentum so it carries through the descent.
        if (_state == CombatState.Acrobatic && _rb.linearVelocity.y < 0f)
        {
            Vector3 forwardVelocity = transform.forward * _acrobaticForwardSpeed;
            _rb.linearVelocity = new Vector3(
                forwardVelocity.x,
                _rb.linearVelocity.y,
                forwardVelocity.z
            );
            EnterFreefall();
        }

        _lastNormalizedTime = currentTime;
    }

    #endregion

    #region Freefall

    /// <summary>
    /// Enters freefall state. Called when <see cref="AnimationExitEvent.EndAcrobatics"/>
    /// fires from the Animator, meaning the flip clip has finished and the entity must now
    /// fall until the collider touches the floor.
    /// </summary>
    private void EnterFreefall()
    {
        _state = CombatState.Freefall;
        _isAcrobaticMove = false;
        _freefallGraceTicks = 0;
        _freefallMinAirtime = 0f;
        _isLeapBackFreefall = false;

        // Let physics gravity pull the entity down; OnAnimatorMove skips when isInFlight.
        _movement.isInFlight = true;
        _movement.canRotate  = false;

        // Clear the active move so no hitbox/combo logic ticks
        if (_activeMove != null && _hitboxActive)
            CloseHitbox(GetHitboxType(_activeMove));
        _activeMove = null;
        _hitboxActive = false;
        _canAcceptComboInput = false;

        _animator.applyRootMotion = false;
        _animator.SetBool(HashIsGrounded, false);
        _animator.SetBool(HashIsAction, true);
    }

    /// <summary>
    /// Ticked every FixedUpdate while in <see cref="CombatState.Freefall"/>.
    /// Uses the entity's Collider (via <see cref="_isTouchingFloor"/>) to detect
    /// the floor layer. A short grace period prevents single-frame floor contact
    /// from being missed.
    /// </summary>
    private void TickFreefall()
    {
        if (_health.IsDead)
        {
            OnFreefallLanded();
            return;
        }

        if (Time.time < _freefallMinAirtime) return;

        if (_isTouchingFloor)
        {
            OnFreefallLanded();
        }
        else
        {
            _freefallGraceTicks++;

            // Only fire the falling trigger after a few frames of confirmed air-time
            // to avoid a single-frame flicker at the transition boundary.
            if (_freefallGraceTicks >= FREEFALL_GRACE_TICKS)
                _animator.SetTrigger(HashIsFalling);
        }
    }

    private void OnFreefallLanded()
    {   
        _isLeapBackFreefall = false;
        _movement.isInFlight = false;
        _movement.canRotate  = true;

        _rb.linearVelocity = Vector3.zero;

        _animator.ResetTrigger(HashIsFalling);
        _animator.SetBool(HashIsGrounded, true);
        _animator.SetBool(HashIsAction, false);

        NotifyDust();
        TransitionTo(CombatState.Idle);
    }

    #endregion

    #region State Transitions

    /// <summary>
    /// Central state transition helper. Cleans up the outgoing state and enters the new one.
    /// </summary>
    private void TransitionTo(CombatState newState)
    {
        // --- Exit current state ---
        switch (_state)
        {
            case CombatState.Attacking:
            case CombatState.Acrobatic:
                if (_activeMove != null && _hitboxActive)
                    CloseHitbox(GetHitboxType(_activeMove));
                _activeMove = null;
                _hitboxActive = false;
                _canAcceptComboInput = false;
                _canRotateDuringAttack = false;
                _isAcrobaticMove = false;
                _movement.canRotate = true;
                _animator.applyRootMotion = true;
                break;

            case CombatState.Blocking:
                _block.Reset();
                break;

            case CombatState.Freefall:
                _movement.isInFlight = false;
                _movement.canRotate = true;
                _animator.applyRootMotion = true;
                _animator.ResetTrigger(HashIsFalling);
                break;
        }

        _state = newState;
        _animator.SetFloat("animatorSpeed", 1f);
        _animator.SetBool(HashIsAction, newState != CombatState.Idle);
    }

    /// <summary>
    /// Restores all combat state to idle. Called by <see cref="OnAnimationStateExit"/>
    /// and by external systems (e.g. <see cref="ClinchHandler"/>).
    /// </summary>
    public void ResetCombatState()
    {
        if (_activeMove != null && _hitboxActive)
        {
            CloseHitbox(GetHitboxType(_activeMove));
            OnHitboxClosed?.Invoke();
        }

        _strikeTrails.DisableAll();

        _activeMove = null;
        _hitboxActive = false;
        _block?.Reset();
        _canAcceptComboInput = false;
        _canRotateDuringAttack = false;
        _isAcrobaticMove = false;
        _state = CombatState.Idle;
        _movement.isInFlight = false;
        _movement.canRotate = true;
        _animator.applyRootMotion = true;
        _animator.SetFloat("animatorSpeed", 1f);
        _animator.ResetTrigger(HashIsFalling);
        _animator.SetBool(HashIsAction, false);
    }

    #endregion

    #region Charge Logic

    public void StartCharging()
    {
        if (_health.IsDead) return;
        if (_state == CombatState.Freefall) return;
        if (_state == CombatState.DrawingWeapon) return;
        if (_animator.GetBool(HashIsAction) && (_activeMove == null || !_canAcceptComboInput)) return;

        _charge.StartCharging();
    }

    public void ReleaseCharge()
    {
        if (!_charge.IsCharging) return;

        if (_clinchModule != null && _clinchModule.IsClinching)
        {
            bool hasTier = _charge.CurrentTier > 0;
            _charge.Cancel();
            if (hasTier) return;
        }

        int tier = _charge.Release();

        if (tier <= 0) ExecuteLightAttack();
        else           ExecuteChargedAttack(tier);
    }

    public void ExecuteChargedAttack(int chargeTier)
    {
        if (_health.IsDead) return;
        if (_state == CombatState.Freefall) return;
        if (_animator.GetBool(HashIsAction) && (_activeMove == null || !_canAcceptComboInput)) return;

        _animator.SetBool(HashIsAction, true);
        int moveIndex = chargeTier - 1;

        StartCoroutine(SpecialMoveWithAfterimage());

        if (currentStyle.chargedAttacks != null && currentStyle.chargedAttacks.Count > 0)
        {
            int finalIndex = Mathf.Clamp(moveIndex, 0, currentStyle.chargedAttacks.Count - 1);
            PlayMove(currentStyle.chargedAttacks[finalIndex]);
            _state = CombatState.Attacking;
        }
    }

    IEnumerator SpecialMoveWithAfterimage()
    {
        AfterimageEffect effect = gameObject.AddComponent<AfterimageEffect>();
        yield return new WaitForSeconds(specialMoveDuration);
        Destroy(effect);
        _animator.SetBool(HashIsAction, false);
    }

    #endregion

    #region Attacks & Combo Chain

    public void ExecuteLightAttack()
    {
        if (_health.IsDead) return;
        if (_state == CombatState.Freefall) return;
        if (_state == CombatState.DrawingWeapon) return;
        if (_activeMove != null && !_canAcceptComboInput) return;
        if (_block.IsBlocking) return;

        if (_clinchModule != null && _clinchModule.IsClinching)
        {
            if (_clinchModule.IsExecutingThrow) return;
            _activeMove = currentStyle.clinchLightAtk;
            _clinchModule.ExecuteClinchLightAttack();
            return;
        }

        // Only continue the combo chain if the attack was pressed inside the previous
        // move's combo window. Any press outside that window restarts from index 0.
        if (!_canAcceptComboInput)
            _comboIndex = 0;

        CombatMove move = currentStyle.lightAttacks[_comboIndex % currentStyle.lightAttacks.Length];
        PlayMove(move);
        _state = CombatState.Attacking;
        _animator.SetBool(HashIsAction, true);

        if (_health.characterEffects != null
            && _health.characterEffects.sfxLightAttackCry != null
            && UnityEngine.Random.Range(0, 5) == 0)
        {
            JSAM.AudioManager.PlaySound(_health.characterEffects.sfxLightAttackCry);
        }

        _comboIndex++;
    }

    public void ExecuteHeavyAttack()
    {
        if (_health.IsDead) return;
        if (_state == CombatState.Freefall) return;
        if (_state == CombatState.DrawingWeapon) return;
        if (_animator.GetBool(HashIsAction) && (_activeMove == null || !_canAcceptComboInput)) return;
        if (_block.IsBlocking) return;

        if (_clinchModule != null && _clinchModule.IsClinching)
        {
            _clinchModule.ExecuteWheelThrow();
            return;
        }

        _comboIndex = 0;
        PlayMove(currentStyle.heavyAttack);
        _state = CombatState.Attacking;
        _animator.SetBool(HashIsAction, true);
    }

    public void ExecuteAcrobatics()
    {
        if (_health.IsDead) return;
        if (_state != CombatState.Idle) return;  // DrawingWeapon is not Idle, so this blocks it naturally.

        // Allow the jump if grounded now OR within the coyote-time window after leaving the floor.
        bool withinCoyoteWindow = (Time.time - _lastGroundedTime) <= COYOTE_TIME;
        if (!_isTouchingFloor && !withinCoyoteWindow) return;

        CombatMove flipMove = currentStyle.acrobaticFlip;
        if (flipMove == null) return;

        _state = CombatState.Acrobatic;
        _isAcrobaticMove = true;
        _acrobaticForwardSpeed = forwardForce;

        _animator.applyRootMotion = false;

        // Physics-driven jump: apply an impulse and let gravity do the rest.
        _movement.isInFlight = true;
        _movement.canRotate  = false;
        Vector3 velocity = transform.forward * forwardForce;
        velocity.y = jumpForce;
        _rb.linearVelocity = velocity;

        StartCoroutine(FlipWithAfterimage(flipMove));
        PlayMove(flipMove, isAcrobatic: true);
        _animator.SetBool(HashIsAction, true);
        _animator.SetBool(HashIsGrounded, false);
    }

    private IEnumerator FlipWithAfterimage(CombatMove flipMove)
    {
        FlipAfterimageEffect effect = gameObject.AddComponent<FlipAfterimageEffect>();
        float duration = flipMove.animationClip != null ? flipMove.animationClip.length * 0.8f : specialMoveDuration;
        yield return new WaitForSeconds(duration);
        Destroy(effect);
    }

    /// <summary>
    /// Sword-only combo cancel: if the entity is mid-attack in a combo window with a
    /// <see cref="FightingStyleType.SwordFighting"/> style, leap backward with an arc
    /// and enter freefall. Resets the combo chain.
    /// </summary>
    /// <returns><c>true</c> if the leap-back was executed; <c>false</c> if preconditions were not met.</returns>
    public bool ExecuteSwordLeapBack()
    {
        if (_health.IsDead) return false;
        if (_state != CombatState.Attacking) return false;
        if (!_canAcceptComboInput) return false;
        if (currentStyle == null || currentStyle.styleType != FightingStyleType.SwordFighting) return false;

        if (_health.characterEffects != null && _health.characterEffects.sfxLightAttackCry != null)
            JSAM.AudioManager.PlaySound(_health.characterEffects.sfxLightAttackCry);

        // Reset combo
        _comboIndex = 0;

        // Clean up the current attack (close hitbox, clear trails, etc.)
        if (_activeMove != null && _hitboxActive)
        {
            CloseHitbox(GetHitboxType(_activeMove));
            OnHitboxClosed?.Invoke();
        }
        _strikeTrails.DisableAll();
        _activeMove = null;
        _hitboxActive = false;
        _canAcceptComboInput = false;
        _canRotateDuringAttack = false;

        // Physics-driven backward leap: entity stays facing the same direction
        _animator.applyRootMotion = false;
        _movement.isInFlight = true;
        _movement.canRotate  = false;

        float upForce   = currentStyle.leapBackUpForce;
        float backForce = currentStyle.leapBackForce;
        Vector3 velocity = -transform.forward * backForce;
        velocity.y = upForce;
        _rb.linearVelocity = velocity;

        // Transition straight into freefall — the "Acrobatics Fall" state is driven
        // by the t_isFalling trigger from Any State, so no special clip override needed.
        _state = CombatState.Freefall;
        _freefallGraceTicks = 0;
        _freefallMinAirtime = Time.time + MIN_FREEFALL_AIRTIME;
        _isLeapBackFreefall = true;

        _animator.SetBool(HashIsGrounded, false);
        _animator.SetBool(HashIsAction, true);
        _animator.SetTrigger(HashIsFalling);
        

        return true;
    }

    #endregion

    #region Core Combat Engine

    private void PlayMove(CombatMove move, bool isAcrobatic = false)
    {
        if (move.animationClip == null) return;

        _activeMove = move;
        _hitboxActive = false;
        _canAcceptComboInput = false;
        _lastNormalizedTime = -0.01f;

        ClearHitCache();
        ResetAudioEvents();

        _movement.canRotate = move.rotationAllowanceEnd > 0f;

        // Activate the strike trail only for heavy moves
        _strikeTrails.DisableAll();
        if (move.isHeavy && move.strikeLimb != VFXLimb.None)
            _strikeTrails.EnableForLimb(move.strikeLimb);

        if (isAcrobatic)
        {
            _overrideController[ACROBATICS_CLIP_SLOT_KEY] = move.animationClip;
            _animator.Play("ReplaceableAcrobatics", 0, 0f);
        }
        else
        {
            _overrideController[CLIP_SLOT_KEY] = move.animationClip;
            //_animator.Play("ReplaceableAttack", 0, 0f);
            _animator.CrossFade("ReplaceableAttack", 0.05f, 0, 0f);

        }
        _animator.Update(0f);
    }

    #endregion

    #region Hitbox & Audio

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

    public void CloseHitbox(int id)
    {
        HitboxType type = (HitboxType)id;
        foreach (var hb in _allHitboxes)
        {
            if (hb.hitboxType == type) hb.Deactivate();
        }
    }

    private static int GetHitboxType(IActiveCombatMove move)
        => move is CombatMove cm ? (int)cm.hitboxType : (int)HitboxType.Fist;

    public void TickClinchAttack(float normalizedTime)
    {
        if (_activeMove == null) return;
        TickMoveState(normalizedTime);
    }

    private void TickMoveState(float normalizedTime)
    {
        bool shouldBeOpen = _activeMove.IsInHitWindow(normalizedTime);
        if (shouldBeOpen && !_hitboxActive)
        {
            OpenHitbox(GetHitboxType(_activeMove));
            _hitboxActive = true;
            OnHitboxOpened?.Invoke(_activeMove as CombatMove);
        }
        else if (!shouldBeOpen && _hitboxActive)
        {
            CloseHitbox(GetHitboxType(_activeMove));
            _hitboxActive = false;
            OnHitboxClosed?.Invoke();
        }

        // Guard against re-entrancy: OnHitboxOpened subscribers (e.g. thrown-weapon handlers)
        // may call RevertToDefaultStyle() → ResetCombatState() which nulls _activeMove.
        if (_activeMove == null) return;

        bool newComboState = _activeMove.IsInComboWindow(normalizedTime);
        if (newComboState && !_canAcceptComboInput)
            _animator.SetBool(HashIsAction, false);
        _canAcceptComboInput = newComboState;

        UpdateAudioEvents(normalizedTime);
    }

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

    private void ResetAudioEvents()
    {
        if (_activeMove?.AudioEvents == null) return;
        for (int i = 0; i < _activeMove.AudioEvents.Length; i++)
            _activeMove.AudioEvents[i].hasPlayed = false;
    }

    #endregion

    #region Defensive & KI Logic

    public void SetBlocking(bool blocking)
    {
        if (_clinchModule != null && _clinchModule.IsClinching) return;

        if (blocking)
        {
            if (_activeMove != null || _block.IsBlocking) return;
            if (_state == CombatState.Freefall || _state == CombatState.DrawingWeapon) return;
            if (BlockClip == null) return;

            _state = CombatState.Blocking;
            _animator.SetBool(HashIsAction, true);
            _block.SetBlocking(true, BlockClip);
        }
    }

    public void ResetBlocking() => _block.ResetBlocking();

    public void HandleKIInput() => _block.HandleKIInput(_activeMove != null);

    public void ClearHitCache() => _hitCache.Clear();
    public void RegisterHit(Transform target) => _hitCache.Add(target);
    public bool HasHitTarget(Transform target) => _hitCache.Contains(target);

    #endregion

    #region Animation State Callbacks

    public void OnAnimationStateExit(int layerIndex, AnimationExitEvent exitEvent)
    {
        if (exitEvent == AnimationExitEvent.ClipEnded)
        {
            ResetCombatState();
        }
        else if (exitEvent == AnimationExitEvent.EndBlock)
        {
            _block.OnBlockAnimationEnded();
            _state = CombatState.Idle;
            _animator.SetBool(HashIsAction, false);
        }
        else if (exitEvent == AnimationExitEvent.EndAcrobatics)
        {
            // The flip animation clip finished — enter freefall.
            // If ResetCombatState already ran (e.g. death), state is already Idle; skip.
            if (_state != CombatState.Acrobatic) return;

            EnterFreefall();
        }
        else if (exitEvent == AnimationExitEvent.EndDrawWeapon)
        {
            if (_state == CombatState.DrawingWeapon)
            {
                _state = CombatState.Idle;
                _animator.SetBool(HashIsAction, false);
            }
        }
    }

    public void NotifyDust()
    {
        if (_movement.dustParticles != null)
            _movement.dustParticles.Play();
    }

    /// <summary>
    /// Kept for backward compatibility with <see cref="AnimationStateNotifier"/>.
    /// No longer needed for freefall — floor detection is now collider-based.
    /// </summary>
    public void ToggleAcrobaticGroundCheck(bool active)
    {
        // Intentionally empty — freefall is now driven by EndAcrobatics + collider contact.
    }

    #endregion
}
