using UnityEngine;

/// <summary>
/// Manages blocking, KI parry, and KI power-up logic.
/// Extracted from <see cref="CombatHandler"/> to isolate the defensive sub-state machine.
/// </summary>
public class BlockHandler
{
    private readonly CombatHandler _combat;
    private readonly Animator _animator;
    private readonly AnimatorOverrideController _overrideController;
    private readonly MovementComponent _movement;
    private readonly HealthComponent _health;

    private const string BLOCK_CLIP_SLOT_KEY = "ReplaceableBlock";
    private const float KI_PARRY_WINDOW = 0.2f;
    private const float BLOCK_HOLD_THRESHOLD = 0.9f;

    private float _lastBlockStartTime;
    private bool _blockAnimationPlaying;
    private bool _blockFrozen;
    private bool _blockReleased;
    private bool _blockHeld;

    public bool IsBlocking { get; private set; }

    public BlockHandler(CombatHandler combat, Animator animator,
        AnimatorOverrideController overrideController, MovementComponent movement,
        HealthComponent health)
    {
        _combat = combat;
        _animator = animator;
        _overrideController = overrideController;
        _movement = movement;
        _health = health;
    }

    /// <summary>
    /// Tick the block-hold freeze logic. Called from <see cref="CombatHandler.Update"/>
    /// while in <see cref="CombatState.Blocking"/>.
    /// Returns true if the caller should skip the rest of its Update tick.
    /// </summary>
    public bool TickBlockHold()
    {
        if (!_blockAnimationPlaying) return false;

        AnimatorStateInfo blockState = _animator.GetCurrentAnimatorStateInfo(0);
        if (blockState.IsName("ReplaceableBlock"))
        {
            if (!_blockFrozen && !_blockReleased && _blockHeld && blockState.normalizedTime >= BLOCK_HOLD_THRESHOLD)
            {
                _blockFrozen = true;
                _animator.SetFloat("animatorSpeed", 0f);
            }
            return true;
        }

        return false;
    }

    public void SetBlocking(bool blocking, AnimationClip blockClip)
    {
        if (blocking)
        {
            if (blockClip == null) return;

            IsBlocking = true;
            _blockAnimationPlaying = true;
            _blockFrozen = false;
            _blockHeld = true;
            _lastBlockStartTime = Time.time;

            _animator.SetFloat("animatorSpeed", 1f);

            _overrideController[BLOCK_CLIP_SLOT_KEY] = blockClip;
            _animator.Play("ReplaceableBlock", 0, 0f);
            _animator.Update(0f);
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
        IsBlocking     = false;
        _animator.SetFloat("animatorSpeed", 1f);
    }

    public void HandleKIInput(bool hasActiveMove)
    {
        if (_health == null || _health.AvailableKI <= 0) return;
        if (IsBlocking) ExecuteKIParry();
        else if (!hasActiveMove) ExecuteKIPowerUp();
    }

    /// <summary>
    /// Clears all block state. Called on state exit or forced release (e.g. death).
    /// </summary>
    public void Reset()
    {
        _blockAnimationPlaying = false;
        _blockFrozen = false;
        _blockReleased = false;
        _blockHeld = false;
        IsBlocking = false;
        _animator.SetFloat("animatorSpeed", 1f);
    }

    public void ForceRelease()
    {
        if (!_blockFrozen && !_blockAnimationPlaying) return;

        Reset();

        if (_movement != null)
            _movement.isMovementLocked = false;
    }

    /// <summary>Called by <see cref="CombatHandler.OnAnimationStateExit"/> for EndBlock.</summary>
    public void OnBlockAnimationEnded()
    {
        Reset();

        if (_movement != null)
            _movement.isMovementLocked = false;
    }

    private void ExecuteKIParry()
    {
        if (Time.time - _lastBlockStartTime <= KI_PARRY_WINDOW)
        {
            _health.SpendInnerForce();
            _animator.Play("KI_Parry_Pose");
        }
    }

    private void ExecuteKIPowerUp()
    {
        _health.SpendInnerForce();
    }
}
