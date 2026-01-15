using System.Collections.Generic;
using UnityEngine;

public class CombatHandler : MonoBehaviour
{
    [Header("Components")]
    private Animator _animator;
    private AnimatorOverrideController _overrideController;
    private HealthComponent _health;
    private MovementComponent _movement;

    [Header("Data")]
    public FightingStyle currentStyle;

    [Header("Combo Settings")]
    private int _comboIndex = 0;
    private float _lastAttackTime;
    private const float COMBO_RESET_TIME = 1.2f;

    [Header("Internal State")]
    private CombatMove _activeMove;
    private HashSet<Transform> _hitCache = new();
    private CombatHitbox[] _allHitboxes;
    private bool _hitboxActive;
    private bool _canAcceptComboInput; // New flag for window-based combos

    [Header("Input Timing")]
    private float _attackHoldTimer;
    private bool _isHoldingAttack;
    private bool _isBlocking;

    [Header("KI Settings")]
    private float _kiBars = 3f;
    private const float KI_PARRY_WINDOW = 0.2f; // Tight timing window for Parry
    private float _lastBlockStartTime;

    private const string CLIP_SLOT_KEY = "Replaceable_Motion_Base";


    [Header("Motion State")]
    private float _lastNormalizedTime; // Tracks progress to calculate delta movement
    private bool _canRotateDuringAttack; // Tracks if rotation is allowed during current attack

    public bool CanRotateDuringAttack => _canRotateDuringAttack;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _health = GetComponent<HealthComponent>();
        _movement = GetComponent<MovementComponent>();

        _overrideController = new AnimatorOverrideController(_animator.runtimeAnimatorController);
        _animator.runtimeAnimatorController = _overrideController;

        _allHitboxes = GetComponentsInChildren<CombatHitbox>();
    }

    private void Update()
    {
        if (_activeMove == null) return;

        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);

        if (stateInfo.IsName("ReplaceableAttack"))
        {
            // Use the absolute normalized time (0 to 1)
            // We avoid % 1f here to prevent the 'Double-Jump' on loops
            float currentTime = stateInfo.normalizedTime;

            // If we've passed 1.0, the move is over. 
            if (currentTime >= 1.0f)
            {
                ResetCombatState();
                return;
            }

            // This ensures we only move if the animator has actually progressed
            if (currentTime > _lastNormalizedTime && _lastNormalizedTime >= 0)
            {
                // We calculate exactly how much of the CURVE we covered since last frame
                float deltaDistance = _activeMove.EvaluateMotionDelta(_lastNormalizedTime, currentTime);

                if (deltaDistance > 0)
                {
                    // Move along the current facing direction
                    transform.position += transform.forward * deltaDistance;
                }
            }

            _lastNormalizedTime = currentTime;


            // --- 2. HITBOX WINDOW ---
            bool shouldBeOpen = _activeMove.IsInHitWindow(currentTime);
            if (shouldBeOpen && !_hitboxActive)
            {
                OpenHitbox((int)_activeMove.hitboxType);
                _hitboxActive = true;
                //PlayAttackSFX(currentTime); // Optional: Trigger sound on hit start
            }
            else if (!shouldBeOpen && _hitboxActive)
            {
                CloseHitbox((int)_activeMove.hitboxType);
                _hitboxActive = false;
            }

            // --- 3. COMBO WINDOW ---
            _canAcceptComboInput = _activeMove.IsInComboWindow(currentTime);

            // --- 4. ROTATION ALLOWANCE ---
            _canRotateDuringAttack = _activeMove.CanRotate(currentTime);
            _movement.canRotate = _canRotateDuringAttack;

            // --- 5. AUDIO EVENTS ---
            UpdateAudioEvents(currentTime);

            // --- 6. MOVEMENT AUTO-RESET ---
            if (currentTime >= 0.95f) _movement.speedMultiplier = 1.0f;
        }
        else
        {
            ResetCombatState();
        }
    }

    private void ResetCombatState()
    {
        if (_activeMove != null && _hitboxActive)
            CloseHitbox((int)_activeMove.hitboxType);

        _activeMove = null;
        _hitboxActive = false;
        _canAcceptComboInput = false;
        _canRotateDuringAttack = false;
        _movement.speedMultiplier = 1.0f;
        _movement.canRotate = true; // Re-enable rotation when not attacking
    }

    private void PlayMove(CombatMove move)
    {
        if (move.animationClip == null) return;

        _activeMove = move;
        _hitboxActive = false;
        _canAcceptComboInput = false;

        // Fix: Initialize to a small negative value so the first frame (0) 
        // is always greater than _lastNormalizedTime
        _lastNormalizedTime = -0.01f;

        ClearHitCache();
        ResetAudioEvents();

        _movement.speedMultiplier = move.isHeavy ? 0.5f : 0f;
        _movement.canRotate = move.rotationAllowanceEnd > 0f; // Set initial rotation state

        _overrideController[CLIP_SLOT_KEY] = move.animationClip;
        // Force the animator to update its state immediately
        _animator.Play("ReplaceableAttack", 0, 0f);
        _animator.Update(0f);
    }

    public void ExecuteAcrobatics()
    {
        if (_health != null && _health.IsDead) return;

        // Safety: Only flip if we aren't already mid-attack
        if (_activeMove != null) return;

        CombatMove flipMove = currentStyle.acrobaticFlip;
        if (flipMove == null) return;

        // Trigger the move logic
        PlayMove(flipMove);

        // Optional: Since it's a flip, we might want to ignore collisions 
        // or grant "I-Frames" (Invincibility) here.
        Debug.Log("Ninja Flip Triggered!");
    }

    private void UpdateAudioEvents(float normalizedTime)
    {
        if (_activeMove.audioEvents == null) return;

        for (int i = 0; i < _activeMove.audioEvents.Length; i++)
        {
            var ev = _activeMove.audioEvents[i];
            if (!ev.hasPlayed && normalizedTime >= ev.triggerTime)
            {
                // Using your JSAM integration
                JSAM.AudioManager.PlaySound(ev.sound);
                _activeMove.audioEvents[i].hasPlayed = true;
            }
        }
    }
    private void ResetAudioEvents()
    {
        if (_activeMove.audioEvents == null) return;
        for (int i = 0; i < _activeMove.audioEvents.Length; i++)
        {
            _activeMove.audioEvents[i].hasPlayed = false;
        }
    }



    public void ExecuteLightAttack()
    {
        if (_health != null && _health.IsDead) return;

        // COMBO LOGIC: If already attacking, check the window!
        if (_activeMove != null && !_canAcceptComboInput) return;

        if (Time.time - _lastAttackTime > COMBO_RESET_TIME) _comboIndex = 0;

        CombatMove move = currentStyle.lightAttacks[_comboIndex % currentStyle.lightAttacks.Length];
        PlayMove(move);

        _comboIndex++;
        _lastAttackTime = Time.time;
    }

    public void ExecuteHeavyAttack()
    {
        if (_health != null && _health.IsDead) return;

        // Same combo check for heavy
        if (_activeMove != null && !_canAcceptComboInput) return;

        _comboIndex = 0;
        PlayMove(currentStyle.heavyAttack);
    }


    public void HandleAttackInput(bool isPressed)
    {
        if (isPressed)
        {
            _isHoldingAttack = true;
            _attackHoldTimer = 0f;
        }
        else // Released
        {
            if (!_isHoldingAttack) return;

            if (_attackHoldTimer >= 1.0f) ExecuteMediumAttack();
            else ExecuteLightAttack();

            _isHoldingAttack = false;
        }
    }


    public void ExecuteMediumAttack()
    {
        if (_activeMove != null && !_canAcceptComboInput) return;
        PlayMove(currentStyle.mediumAttack); // Assuming mediumAttack added to FightingStyle
    }

    public void SetBlocking(bool blocking)
    {
        _isBlocking = blocking;
        _animator.SetBool("IsBlocking", _isBlocking);
        //_movement.speedMultiplier = _isBlocking ? 0.2f : 1.0f;
    }


    // --- HITBOX MANAGEMENT ---
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

    public void HandleKIInput()
    {
        if (_kiBars < 1f) return; // Need at least 1 bar

        // CONTEXT 1: KI During Block -> KI Parry
        if (_isBlocking)
        {
            ExecuteKIParry();
        }
        // CONTEXT 2: KI While Neutral -> Power-Up Mode
        else if (_activeMove == null)
        {
            ExecuteKIPowerUp();
        }
        // CONTEXT 3: KI Magic (Context-sensitive/Late game)
        else
        {
            // Handle Magic logic here later...
        }
    }

    private void ExecuteKIParry()
    {
        // Check if the player blocked RECENTLY (Skill test)
        float timeSinceBlock = Time.time - _lastBlockStartTime;

        if (timeSinceBlock <= KI_PARRY_WINDOW)
        {
            Debug.Log("KI PARRY ATTEMPT!");
            _kiBars -= 1f;
            _animator.Play("KI_Parry_Pose"); // Trigger parry animation
                                             // Logic to detect incoming hit and convert to Grab goes here
        }
        else
        {
            Debug.Log("Parry Failed: Blocked too early.");
            _kiBars -= 1f; // Fails cleanly, bar spent
        }
    }

    private void ExecuteKIPowerUp()
    {
        Debug.Log("KI POWER UP!");
        _kiBars -= 1f;
        // Activate aura/buff logic
        // (e.g., Start a Coroutine that sets a 'isPowerUp' flag for 1.5s)
    }

    public void ClearHitCache() => _hitCache.Clear();
    public bool HasHitTarget(Transform target) => _hitCache.Contains(target);
    public void RegisterHit(Transform target) => _hitCache.Add(target);
}