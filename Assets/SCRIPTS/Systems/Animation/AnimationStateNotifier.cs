using UnityEngine;

/// <summary>
/// A <see cref="StateMachineBehaviour"/> that notifies specific components on the same
/// <see cref="Animator"/> GameObject when an animation state exits.
/// Attach this to an Animator state via the Unity Animator window.
/// Set each component's event field to the event you want it to receive.
/// Leave a field as <see cref="AnimationExitEvent.None"/> to skip that component entirely.
/// </summary>
public class AnimationStateNotifier : StateMachineBehaviour
{
    /// <summary>
    /// Event sent to <see cref="CombatHandler"/> on exit.
    /// Set to <see cref="AnimationExitEvent.None"/> to skip it.
    /// </summary>
    [Tooltip("Event sent to CombatHandler on exit. None = do not notify.")]
    public AnimationExitEvent combatHandlerEvent = AnimationExitEvent.None;

    /// <summary>
    /// Event sent to <see cref="MovementComponent"/> on exit.
    /// Set to <see cref="AnimationExitEvent.None"/> to skip it.
    /// </summary>
    [Tooltip("Event sent to MovementComponent on exit. None = do not notify.")]
    public AnimationExitEvent movementEvent = AnimationExitEvent.None;

    /// <summary>
    /// Event sent to <see cref="ClinchHandler"/> on exit.
    /// Set to <see cref="AnimationExitEvent.None"/> to skip it.
    /// </summary>
    [Tooltip("Event sent to ClinchHandler on exit. None = do not notify.")]
    public AnimationExitEvent clinchHandlerEvent = AnimationExitEvent.None;

    // Cached direct references — resolved once on state enter to avoid per-exit allocations.
    private CombatHandler _combatHandler;
    private MovementComponent _movement;
    private ClinchHandler _clinchHandler;

    /// <summary>
    /// Called by Unity when the Animator enters this state.
    /// Resolves and caches component references once per entry so
    /// <see cref="OnStateExit"/> can call them directly with no allocation.
    /// </summary>
    public override void OnStateEnter(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        if (_combatHandler == null)
            _combatHandler = animator.GetComponent<CombatHandler>();
        if (_movement == null)
            _movement = animator.GetComponent<MovementComponent>();
        if (_clinchHandler == null)
            _clinchHandler = animator.GetComponent<ClinchHandler>();
    }

    /// <summary>
    /// Called by Unity when the Animator exits this state — including when interrupted
    /// by a transition. Each component is notified independently using its own event field.
    /// </summary>
    /// <param name="animator">The Animator this behaviour is attached to.</param>
    /// <param name="stateInfo">Info about the state that is being exited.</param>
    /// <param name="layerIndex">The Animator layer index this state belongs to.</param>
    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        if (!(stateInfo.normalizedTime >= 1.0f)) return;

        if (_combatHandler != null && combatHandlerEvent != AnimationExitEvent.None)
            _combatHandler.OnAnimationStateExit(layerIndex, combatHandlerEvent);

        if (_movement != null && movementEvent != AnimationExitEvent.None)
            _movement.OnAnimationStateExit(layerIndex, movementEvent);

        if (_clinchHandler != null && clinchHandlerEvent != AnimationExitEvent.None)
            _clinchHandler.OnAnimationStateExit(layerIndex, clinchHandlerEvent);
    }
}

    

