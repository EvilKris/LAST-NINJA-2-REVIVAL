using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HealthComponent))]
public class CombatHandler : MonoBehaviour
{
    [Header("Components")]
    private Animator _animator;
    private AnimatorOverrideController _overrideController;
    private HealthComponent _health;
    private MovementComponent _movement;


    [Tooltip("Number of charges the entity has (for consecutively stronger special moves) - a move must also be added in the Fighting Style config")]
    public int ChargeCount = 1; // Number of charges the entity has


    [Header("Data")]
    public FightingStyle currentStyle;

    [Header("Clinch Settings")]
    [Tooltip("Time in seconds before an entity can be clinched again after being thrown")]
    public float ClinchRecovery = 3f;
    
    [Header("Combo Settings")]
    private int _comboIndex = 0;
    private float _lastAttackTime;
    private const float COMBO_RESET_TIME = 1.2f;

    [Header("Charge System (Spike Out Style)")]
    private float _currentChargeTimer;
    private bool _isCharging;
    private int _cachedMaxCharges = -1;
    private int _cachedCurrentTier = -1;
    private float _cachedChargeProgress = -1f;
    private int _lastPlayedTierSfx = -1;

    // Inside CombatHandler.cs
    private ClinchHandler _clinchModule;


    // Events for UI updates
    public event Action<int> OnMaxChargesChanged;
    public event Action<int, float> OnChargeStateChanged;

    // Properties for UI and Logic access
    public int MaxCharges => currentStyle != null && currentStyle.chargedAttacks != null ? currentStyle.chargedAttacks.Count : 0;
    public int CurrentTier => Mathf.FloorToInt(_currentChargeTimer);
    public float ChargeProgress => _currentChargeTimer % 1.0f; // For smooth UI bar filling

    [Header("Internal State")]
    private CombatMove _activeMove;
    private HashSet<Transform> _hitCache = new();
    private CombatHitbox[] _allHitboxes;
    private bool _hitboxActive;
    private bool _canAcceptComboInput;
    private bool _isAcrobaticMove;
    private bool _isClinchAttack; // Tracks if currently executing a clinch attack

    [Header("KI Settings")]
    private float _kiBars = 3f;
    private const float KI_PARRY_WINDOW = 0.2f;
    private float _lastBlockStartTime;
    private bool _isBlocking;

    private const string CLIP_SLOT_KEY = "Replaceable_Motion_Base";

    [Header("Motion State")]
    private float _lastNormalizedTime;
    private bool _canRotateDuringAttack;

    public bool CanRotateDuringAttack => _canRotateDuringAttack;
    public bool IsAttacking => _activeMove != null;
    public bool IsAcrobatic => _isAcrobaticMove;
    public bool IsClinchAttack => _isClinchAttack;
    public bool IsCharging => _isCharging;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _health = GetComponent<HealthComponent>();
        _movement = GetComponent<MovementComponent>();

        _overrideController = new AnimatorOverrideController(_animator.runtimeAnimatorController);
        _animator.runtimeAnimatorController = _overrideController;

        _animator.updateMode = AnimatorUpdateMode.Fixed;
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

        InitializeStyleModules(); // Initialize style-specific modules (will need to be called again if style changes) 
    }


    private void InitializeStyleModules()
    {


        #region Clinch Module Setup 
        // Check if the current style supports clinching
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
            // Remove it if the new style doesn't support it
            if (TryGetComponent<ClinchHandler>(out var oldModule))
            {
                Destroy(oldModule);
            }
        }
        #endregion  
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
        HandleChargeLogic();

        if (_activeMove == null) return;

        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("ReplaceableAttack"))
        {
            float currentTime = stateInfo.normalizedTime;

            // Hitbox Management
            bool shouldBeOpen = _activeMove.IsInHitWindow(currentTime);
            if (shouldBeOpen && !_hitboxActive)
            {
                OpenHitbox((int)_activeMove.hitboxType);
                _hitboxActive = true;
            }
            else if (!shouldBeOpen && _hitboxActive)
            {
                CloseHitbox((int)_activeMove.hitboxType);
                _hitboxActive = false;
            }

            _canAcceptComboInput = _activeMove.IsInComboWindow(currentTime);
            _canRotateDuringAttack = _activeMove.CanRotate(currentTime);
            _movement.canRotate = _canRotateDuringAttack;

            UpdateAudioEvents(currentTime);
        }
    }

    private void FixedUpdate()
    {
        if (_activeMove == null) return;

        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("ReplaceableAttack"))
        {
            float currentTime = stateInfo.normalizedTime;

            if (currentTime >= 1.0f)
            {
                ResetCombatState();
                return;
            }

            if (currentTime > _lastNormalizedTime && _lastNormalizedTime >= 0)
            {
                float deltaDistance = _activeMove.EvaluateMotionDelta(_lastNormalizedTime, currentTime);
                if (deltaDistance > 0)
                {
                    transform.position += transform.forward * deltaDistance;
                }
            }
            _lastNormalizedTime = currentTime;
        }
        else
        {
            ResetCombatState();
        }
    }

    // --- Charge Logic Implementation ---

    private void HandleChargeLogic()
    {
        // Check if max charges changed (e.g., weapon switch)
        int maxCharges = MaxCharges;
        if (maxCharges != _cachedMaxCharges)
        {
            _cachedMaxCharges = maxCharges;
            OnMaxChargesChanged?.Invoke(maxCharges);
        }

        if (_isCharging)
        {
            // Increment timer but clamp at Max Charges defined by the Move List
            _currentChargeTimer = Mathf.Min(_currentChargeTimer + Time.deltaTime, MaxCharges);

            // Only invoke event if charge state actually changed
            int currentTier = CurrentTier;
            float chargeProgress = ChargeProgress;
            if (currentTier != _cachedCurrentTier || Mathf.Abs(chargeProgress - _cachedChargeProgress) > 0.01f)
            {
                _cachedCurrentTier = currentTier;
                _cachedChargeProgress = chargeProgress;
                OnChargeStateChanged?.Invoke(currentTier, chargeProgress);
            }

            // Play sound effect once when a new tier is reached
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
        _isCharging = true;
        _currentChargeTimer = 0f;
        _cachedCurrentTier = 0;
        _cachedChargeProgress = 0f;
        _lastPlayedTierSfx = -1;
        OnChargeStateChanged?.Invoke(0, 0f);
    }

    public void ReleaseCharge()
    {
        if (!_isCharging) return;
        
        // Check if we're in a clinch - if so, reset charge and cancel the release
        if (_clinchModule != null && _clinchModule.IsClinching)
        {
            // Reset charging state - charge is lost if released during clinch
            _isCharging = false;
            _currentChargeTimer = 0f;
            _cachedCurrentTier = 0;
            _cachedChargeProgress = 0f;
            _lastPlayedTierSfx = -1;
            OnChargeStateChanged?.Invoke(0, 0f);
            return;
        }
        
        _isCharging = false;

        int tier = CurrentTier;

        if (tier <= 0)
            ExecuteLightAttack();
        else
            ExecuteChargedAttack(tier);

        _currentChargeTimer = 0f;
        _cachedCurrentTier = 0;
        _cachedChargeProgress = 0f;
        _lastPlayedTierSfx = -1;
        OnChargeStateChanged?.Invoke(0, 0f);
    }

    public void ExecuteChargedAttack(int chargeTier)
    {
        if (_health.IsDead) return;
        if (_activeMove != null && !_canAcceptComboInput) return;

        // Spike Out Logic: Tier 1 = List Index 0
        int moveIndex = chargeTier - 1;

        StartCoroutine(SpecialMoveWithAfterimage());

        if (currentStyle.chargedAttacks != null && currentStyle.chargedAttacks.Count > 0)
        {
            // Safety clamp to ensure we don't go out of bounds
            int finalIndex = Mathf.Clamp(moveIndex, 0, currentStyle.chargedAttacks.Count - 1);
            PlayMove(currentStyle.chargedAttacks[finalIndex]);
        }
    }

    public float specialMoveDuration = 1f;

    IEnumerator SpecialMoveWithAfterimage()
    {
        // Add afterimage effect
        AfterimageEffect effect = gameObject.AddComponent<AfterimageEffect>();

        // Do your special move animation/logic here
        Debug.Log("Special move activated!");

        // Wait for move to finish
        yield return new WaitForSeconds(specialMoveDuration);

        // Remove afterimage effect
        Destroy(effect);

        Debug.Log("Special move finished!");
    }

    // --- Basic Attacks & Combos ---

    public void ExecuteLightAttack()
    {
        if (_health.IsDead) return;
        if (_activeMove != null && !_canAcceptComboInput) return;

        if (Time.time - _lastAttackTime > COMBO_RESET_TIME) _comboIndex = 0;

        CombatMove move = currentStyle.lightAttacks[_comboIndex % currentStyle.lightAttacks.Length];
        PlayMove(move);

        _comboIndex++;
        _lastAttackTime = Time.time;
    }

    public void ExecuteHeavyAttack()
    {
        if (_health.IsDead) return;
        if (_activeMove != null && !_canAcceptComboInput) return;

        _comboIndex = 0;
        PlayMove(currentStyle.heavyAttack);
    }

    public void ExecuteAcrobatics()
    {
        if (_health.IsDead || _activeMove != null) return;

        CombatMove flipMove = currentStyle.acrobaticFlip;
        if (flipMove == null) return;

        _isAcrobaticMove = true;
        PlayMove(flipMove);
    }

    // --- Core Combat Engine ---

    private void PlayMove(CombatMove move)
    {
        if (move.animationClip == null) return;

        _activeMove = move;
        _hitboxActive = false;
        _canAcceptComboInput = false;
        _lastNormalizedTime = -0.01f;

        ClearHitCache();
        ResetAudioEvents();

        _movement.canRotate = move.rotationAllowanceEnd > 0f;
        _overrideController[CLIP_SLOT_KEY] = move.animationClip;

        _animator.Play("ReplaceableAttack", 0, 0f);
        _animator.Update(0f);
    }

    private void ResetCombatState()
    {
        if (_activeMove != null && _hitboxActive)
            CloseHitbox((int)_activeMove.hitboxType);

        _activeMove = null;
        _hitboxActive = false;
        _canAcceptComboInput = false;
        _canRotateDuringAttack = false;
        _isAcrobaticMove = false;
        _isClinchAttack = false; // Clear clinch attack flag
        _movement.canRotate = true;
    }

    // --- Hitbox & Audio Helpers ---

    public void OpenHitbox(int id)
    {
        HitboxType type = (HitboxType)id;
        foreach (var hb in _allHitboxes)
        {
            if (hb.hitboxType == type)
            {
                hb.SetDamage(_activeMove.damage, _activeMove.reactionToTrigger);
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

    private void UpdateAudioEvents(float normalizedTime)
    {
        if (_activeMove.audioEvents == null) return;
        for (int i = 0; i < _activeMove.audioEvents.Length; i++)
        {
            var ev = _activeMove.audioEvents[i];
            if (!ev.hasPlayed && normalizedTime >= ev.triggerTime)
            {
                JSAM.AudioManager.PlaySound(ev.sound);
                _activeMove.audioEvents[i].hasPlayed = true;
            }
        }
    }

    private void ResetAudioEvents()
    {
        if (_activeMove?.audioEvents == null) return;
        for (int i = 0; i < _activeMove.audioEvents.Length; i++)
        {
            _activeMove.audioEvents[i].hasPlayed = false;
        }
    }

    // --- Defensive & KI Logic ---

    public void SetBlocking(bool blocking)
    {
        _isBlocking = blocking;
        if (blocking) _lastBlockStartTime = Time.time;
        _animator.SetBool("IsBlocking", _isBlocking);
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
        Debug.Log("KI Power Up (Ki no chikara - 気の力)");
    }

    //Clinch Attacks (if applicable)  
    public void ExecuteCustomMove(CombatMove move)
    {
        if (_health.IsDead) return;
        _isClinchAttack = true; // Mark as clinch attack
        PlayMove(move);
    }

    public void ExecuteCustomThrow(CombatThrow throwMove)
    {
        if (_health.IsDead) return;
        _isClinchAttack = true; // Mark as clinch attack
        PlayThrowMove(throwMove);
    }

    private void PlayThrowMove(CombatThrow throwMove)
    {
        throw new NotImplementedException();
    }

    public void ClearHitCache() => _hitCache.Clear();
    public void RegisterHit(Transform target) => _hitCache.Add(target);
    public bool HasHitTarget(Transform target) => _hitCache.Contains(target);

  
}