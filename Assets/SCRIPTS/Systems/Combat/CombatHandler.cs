using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central combat controller for an entity. Manages attack execution (light, heavy,
/// charged, acrobatic, clinch), hitbox lifecycle, combo chaining, the charge/tier system,
/// KI defensive actions, and motion-root translation driven by animation curves.
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

    [Header("Data")]
    public FightingStyle currentStyle;

    private const string CLIP_SLOT_KEY = "Replaceable_Motion_Base";
    private const string BLOCK_CLIP_SLOT_KEY = "ReplaceableBlock";
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
    private float _lastAttackTime;
    private const float COMBO_RESET_TIME = 1.2f;

    #endregion

    #region Charge System

    [Header("Charge System (Spike Out Style)")]
    [Tooltip("Number of charges the entity has (for consecutively stronger special moves)")]
    public int ChargeCount = 1;

    private float _currentChargeTimer;
    private bool _isCharging;
    private int _cachedMaxCharges = -1;
    private int _cachedCurrentTier = -1;
    private float _cachedChargeProgress = -1f;
    private int _lastPlayedTierSfx = -1;

    public event Action<int> OnMaxChargesChanged;
    public event Action<int, float> OnChargeStateChanged;

    public int MaxCharges => currentStyle != null && currentStyle.chargedAttacks != null ? currentStyle.chargedAttacks.Count : 0;
    public int CurrentTier => Mathf.FloorToInt(_currentChargeTimer);
    public float ChargeProgress => _currentChargeTimer % 1.0f;

    #endregion

    #region Acrobatics State

    [Header("Acrobatic Settings")]
    [Range(0.5f, 5f)]
    public float acrobaticGravityScale = 1.5f;

    [HideInInspector] public bool _isAcrobaticMove; // kept public for AnimationStateNotifier compatibility
    private float _acrobaticBaseY;
    private float _acrobaticGravityVel;
    private float _acrobaticGravityOffset;
    private float _acrobaticPeakY;

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
    /// Number of FixedUpdate ticks spent in Freefall without floor contact.
    /// The falling animation only triggers after a short grace period to avoid
    /// single-frame false positives.
    /// </summary>
    private int _freefallGraceTicks;
    private const int FREEFALL_GRACE_TICKS = 3;

    #endregion

    #region KI & Defensive State

    private float _kiBars = 3f;
    private const float KI_PARRY_WINDOW = 0.2f;
    private const float BLOCK_HOLD_THRESHOLD = 0.9f;
    private float _lastBlockStartTime;
    private bool _isBlocking;
    private bool _blockAnimationPlaying;
    private bool _blockFrozen;
    private bool _blockReleased;
    private bool _blockHeld;

    #endregion

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

    #region Public State Accessors

    public bool CanRotateDuringAttack => _canRotateDuringAttack;
    public bool IsAttacking    => _activeMove != null;
    public bool IsAcrobatic    => _state == CombatState.Acrobatic;
    public bool IsCharging     => _isCharging;
    public bool IsBlocking     => _isBlocking;
    public bool IsFreefalling  => _state == CombatState.Freefall;

    #endregion

    #region Initialisation

    private void Awake()
    {
        _animator  = GetComponent<Animator>();
        _health    = GetComponent<HealthComponent>();
        _movement  = GetComponent<MovementComponent>();
        _rb        = GetComponent<Rigidbody>();
        _collider  = GetComponent<Collider>();

        _overrideController = new AnimatorOverrideController(_animator.runtimeAnimatorController);
        _animator.runtimeAnimatorController = _overrideController;

        _animator.updateMode = AnimatorUpdateMode.Fixed;

        _allHitboxes = GetComponentsInChildren<CombatHitbox>();

        // Derive a single layer index from the floor LayerMask for OnCollision checks
        _floorLayerIndex = LayerMaskToIndex(_health.floorLayer);
    }

    private void Start()
    {
        if (gameObject.CompareTag("Player"))
        {
            UIChargeDisplay playerUI = MasterSingleton.Instance.UIManager.chargeMeter;
            playerUI.SetTarget(this);
        }
        InitializeStyleModules();

        _health.OnDeath += ForceReleaseBlock;
    }

    private void InitializeStyleModules()
    {
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

        if (currentStyle != null && currentStyle.supportsClinching)
        {
            if (!TryGetComponent<ClinchHandler>(out _clinchModule))
                _clinchModule = gameObject.AddComponent<ClinchHandler>();
            _clinchModule.Initialize(this);
        }
        else
        {
            if (TryGetComponent<ClinchHandler>(out var oldModule))
                Destroy(oldModule);
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

    #endregion

    #region Collision-Based Floor Detection

    private void OnCollisionStay(Collision collision)
    {
        if (_floorLayerIndex >= 0 && collision.gameObject.layer == _floorLayerIndex)
            _isTouchingFloor = true;
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
        HandleChargeLogic();

        // --- Block hold tick ---
        if (_state == CombatState.Blocking && _blockAnimationPlaying)
        {
            AnimatorStateInfo blockState = _animator.GetCurrentAnimatorStateInfo(0);
            if (blockState.IsName("ReplaceableBlock"))
            {
                if (!_blockFrozen && !_blockReleased && _blockHeld && blockState.normalizedTime >= BLOCK_HOLD_THRESHOLD)
                {
                    _blockFrozen = true;
                    _animator.SetFloat("animatorSpeed", 0f);
                }
                return;
            }
        }

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
            TickFreefall();
            return;
        }

        if (_activeMove == null) return;

        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName("ReplaceableAttack") && !stateInfo.IsName("ReplaceableAcrobatics"))
            return;

        float currentTime = stateInfo.normalizedTime;

        // Forward root-motion via animation curve
        if (currentTime > _lastNormalizedTime && _lastNormalizedTime >= 0)
        {
            if (_activeMove is CombatMove moveCast)
            {
                float deltaDistance = moveCast.EvaluateMotionDelta(_lastNormalizedTime, currentTime);
                if (deltaDistance > 0)
                    transform.position += transform.forward * deltaDistance;
            }
        }

        // Acrobatic vertical arc with gravity blend
        if (_state == CombatState.Acrobatic && _activeMove is CombatMove acroMove)
        {
            float curveY = acroMove.EvaluateVerticalPosition(currentTime);

            if (curveY > _acrobaticPeakY)
                _acrobaticPeakY = curveY;

            bool descending = curveY < _acrobaticPeakY - 0.01f;
            if (descending)
            {
                _acrobaticGravityVel    += Physics.gravity.y * acrobaticGravityScale * Time.fixedDeltaTime;
                _acrobaticGravityOffset += _acrobaticGravityVel * Time.fixedDeltaTime;
            }

            float combinedY = _acrobaticBaseY + curveY + _acrobaticGravityOffset;
            combinedY = Mathf.Max(combinedY, _acrobaticBaseY);

            Vector3 pos = transform.position;
            pos.y = combinedY;
            transform.position = pos;
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
                _blockAnimationPlaying = false;
                _blockFrozen = false;
                _blockReleased = false;
                _blockHeld = false;
                _isBlocking = false;
                _animator.SetFloat("animatorSpeed", 1f);
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
            CloseHitbox(GetHitboxType(_activeMove));

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

    private void HandleChargeLogic()
    {
        int maxCharges = MaxCharges;
        if (maxCharges != _cachedMaxCharges)
        {
            _cachedMaxCharges = maxCharges;
            OnMaxChargesChanged?.Invoke(maxCharges);
        }

        if (_isCharging)
        {
            _currentChargeTimer = Mathf.Min(_currentChargeTimer + Time.deltaTime, MaxCharges);

            int currentTier = CurrentTier;
            float chargeProgress = ChargeProgress;
            if (currentTier != _cachedCurrentTier || Mathf.Abs(chargeProgress - _cachedChargeProgress) > 0.01f)
            {
                _cachedCurrentTier    = currentTier;
                _cachedChargeProgress = chargeProgress;
                OnChargeStateChanged?.Invoke(currentTier, chargeProgress);
            }

            if (currentTier > 0 && currentTier != _lastPlayedTierSfx)
            {
                _lastPlayedTierSfx = currentTier;
                JSAM.AudioManager.PlaySound(MasterSingleton.Instance.PrefabBankManager.Charge_Drive_Strike_Tier_Complete);
            }
        }
    }

    public void StartCharging()
    {
        if (_health.IsDead) return;
        if (_state == CombatState.Freefall) return;
        if (_animator.GetBool(HashIsAction) && (_activeMove == null || !_canAcceptComboInput)) return;

        _isCharging          = true;
        _currentChargeTimer  = 0f;
        _cachedCurrentTier   = 0;
        _cachedChargeProgress = 0f;
        _lastPlayedTierSfx   = -1;
        OnChargeStateChanged?.Invoke(0, 0f);
    }

    public void ReleaseCharge()
    {
        if (!_isCharging) return;

        if (_clinchModule != null && _clinchModule.IsClinching)
        {
            bool hasTier = CurrentTier > 0;
            _isCharging = false;
            _currentChargeTimer = 0f;
            _cachedCurrentTier  = 0;
            _cachedChargeProgress = 0f;
            _lastPlayedTierSfx = -1;
            OnChargeStateChanged?.Invoke(0, 0f);
            if (hasTier) return;
        }

        _isCharging = false;
        int tier = CurrentTier;

        if (tier <= 0) ExecuteLightAttack();
        else           ExecuteChargedAttack(tier);

        _currentChargeTimer   = 0f;
        _cachedCurrentTier    = 0;
        _cachedChargeProgress = 0f;
        _lastPlayedTierSfx    = -1;
        OnChargeStateChanged?.Invoke(0, 0f);
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
        if (_activeMove != null && !_canAcceptComboInput) return;
        if (_isBlocking) return;

        if (_clinchModule != null && _clinchModule.IsClinching)
        {
            if (_clinchModule.IsExecutingThrow) return;
            _activeMove = currentStyle.clinchLightAtk;
            _clinchModule.ExecuteClinchLightAttack();
            return;
        }

        if (Time.time - _lastAttackTime > COMBO_RESET_TIME) _comboIndex = 0;

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
        _lastAttackTime = Time.time;
    }

    public void ExecuteHeavyAttack()
    {
        if (_health.IsDead) return;
        if (_state == CombatState.Freefall) return;
        if (_animator.GetBool(HashIsAction) && (_activeMove == null || !_canAcceptComboInput)) return;
        if (_isBlocking) return;

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
        if (_state != CombatState.Idle) return;

        CombatMove flipMove = currentStyle.acrobaticFlip;
        if (flipMove == null) return;

        _state = CombatState.Acrobatic;
        _isAcrobaticMove = true;
        _acrobaticBaseY  = transform.position.y;
        _acrobaticGravityVel    = 0f;
        _acrobaticGravityOffset = 0f;
        _acrobaticPeakY         = 0f;

        _animator.applyRootMotion = false;
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
        }
        else if (!shouldBeOpen && _hitboxActive)
        {
            CloseHitbox(GetHitboxType(_activeMove));
            _hitboxActive = false;
        }

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
            if (_activeMove != null || _isBlocking) return;
            if (_state == CombatState.Freefall) return;
            if (BlockClip == null) return;

            _state = CombatState.Blocking;
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
            if (_blockAnimationPlaying) return;
        }
    }

    public void ResetBlocking()
    {
        _blockHeld = false;

        if (!_blockFrozen)
        {
            _blockReleased = true;
            return;
        }

        _blockReleased = true;
        _blockFrozen   = false;
        _isBlocking    = false;
        _animator.SetFloat("animatorSpeed", 1f);
    }

    public void HandleKIInput()
    {
        if (_kiBars < 1f) return;
        if (_isBlocking) ExecuteKIParry();
        else if (_activeMove == null) ExecuteKIPowerUp();
    }

    private void ExecuteKIParry()
    {
        if (Time.time - _lastBlockStartTime <= KI_PARRY_WINDOW)
        {
            _kiBars -= 1f;
            _animator.Play("KI_Parry_Pose");
        }
    }

    private void ExecuteKIPowerUp()
    {
        _kiBars -= 1f;
    }

    public void ClearHitCache() => _hitCache.Clear();
    public void RegisterHit(Transform target) => _hitCache.Add(target);
    public bool HasHitTarget(Transform target) => _hitCache.Contains(target);

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

    public void OnAnimationStateExit(int layerIndex, AnimationExitEvent exitEvent)
    {
        if (exitEvent == AnimationExitEvent.ClipEnded)
        {
            ResetCombatState();
        }
        else if (exitEvent == AnimationExitEvent.EndBlock)
        {
            _blockAnimationPlaying = false;
            _blockFrozen   = false;
            _blockReleased = false;
            _blockHeld     = false;
            _isBlocking    = false;
            _state = CombatState.Idle;
            _animator.SetFloat("animatorSpeed", 1f);
            _animator.SetBool(HashIsAction, false);

            if (_movement != null)
                _movement.isMovementLocked = false;
        }
        else if (exitEvent == AnimationExitEvent.EndAcrobatics)
        {
            // The flip animation clip finished — enter freefall.
            // If ResetCombatState already ran (e.g. death), state is already Idle; skip.
            if (_state != CombatState.Acrobatic) return;

            EnterFreefall();
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
