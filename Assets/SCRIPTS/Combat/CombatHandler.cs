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

    private const string CLIP_SLOT_KEY = "Replaceable_Motion_Base";

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
        // 1. Exit early if no move is playing
        if (_activeMove == null) return;

        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);

        // 2. Check if we are in the attack state
        if (stateInfo.IsName("ReplaceableAttack"))
        {
            float time = stateInfo.normalizedTime % 1f; //

            // --- HITBOX WINDOW ---
            bool shouldBeOpen = (time >= _activeMove.hitStart && time <= _activeMove.hitEnd);
            if (shouldBeOpen && !_hitboxActive)
            {
                OpenHitbox((int)_activeMove.hitboxType); // Use the move's type!
                _hitboxActive = true;
            }
            else if (!shouldBeOpen && _hitboxActive)
            {
                CloseHitbox((int)_activeMove.hitboxType);
                _hitboxActive = false;
            }

            // --- COMBO WINDOW ---
            _canAcceptComboInput = (time >= _activeMove.comboStart && time <= _activeMove.comboEnd);

            // --- MOVEMENT AUTO-RESET ---
            // If the animation is almost finished, give movement back to avoid sticking
            if (time >= 0.95f) _movement.speedMultiplier = 1.0f;
        }
        else
        {
            // Reset state if animator transitioned to Idle/Hurt/etc.
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
        _movement.speedMultiplier = 1.0f;
    }

    private void PlayMove(CombatMove move)
    {
        if (move.animationClip == null) return;

        _activeMove = move;
        _hitboxActive = false;
        _canAcceptComboInput = false;
        ClearHitCache();

        // 1. Restriction Logic
        _movement.speedMultiplier = move.isHeavy ? 0.5f : 0f;

        // 2. Perform the Swap
        _overrideController[CLIP_SLOT_KEY] = move.animationClip;
        _animator.runtimeAnimatorController = _overrideController;

        // 3. Play the State
        _animator.Play("ReplaceableAttack", 0, 0f);

        // Coroutine removed! Update() now handles the reset.
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

    // --- HITBOX MANAGEMENT ---
    public void OpenHitbox(int id)
    {
        HitboxType type = (HitboxType)id;
        foreach (var hb in _allHitboxes)
        {
            if (hb.hitboxType == type)
            {
                hb.SetDamage(_activeMove.damage); // Use damage from Move data
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

    public void ClearHitCache() => _hitCache.Clear();
    public bool HasHitTarget(Transform target) => _hitCache.Contains(target);
    public void RegisterHit(Transform target) => _hitCache.Add(target);
}