using DG.Tweening;
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

    /// <summary>
    /// Event sent to <see cref="PickupDetector"/> on exit.
    /// Set to <see cref="AnimationExitEvent.None"/> to skip it.
    /// </summary>
    [Tooltip("Event sent to PickupDetector on exit. None = do not notify.")]
    public AnimationExitEvent pickupDetectorEvent = AnimationExitEvent.None;


    [Tooltip("Choose normalized time for activation")]
    [Range(0f, 1f)]
    public float _normalizedTime = 1f;


    // Cached direct references — resolved once on state enter to avoid per-exit allocations.
    private CombatHandler _combatHandler;
    private MovementComponent _movement;
    private ClinchHandler _clinchHandler;
    private PickupDetector _pickupDetector;
    private HealthComponent _healthComponent;

    private bool _isInState = false;
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
        if (_pickupDetector == null)
            _pickupDetector = animator.GetComponent<PickupDetector>();
        if (_healthComponent == null)
            _healthComponent = animator.GetComponent<HealthComponent>();
    }

    public override void OnStateUpdate(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        //Handles interruptions that occur before the normalized time threshold is reached, so that the appropriate "interrupted" events are sent to components.    


        //pickupDetector only applicable to Player
        if (_pickupDetector != null
            && pickupDetectorEvent != AnimationExitEvent.None
            && stateInfo.normalizedTime >= 0.5f && !_isInState)
        {
            _pickupDetector.NotifyCollectWindow();
            _isInState = true; //set _isInState to true to prevent multiple notifications if the state loops back around before exiting
        }
    }

    public override void OnStateMove(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        // If the state loops, we want to treat each loop as a separate playthrough.
        // So if the state has looped back to the beginning, we clear the cached references
        // so they will be re-resolved on the next update (which will be the same as the next entry).
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

        _isInState = false;
    
        bool completed = stateInfo.normalizedTime >= 1.0f;
       
        if (completed)
        {
            if (_combatHandler != null && combatHandlerEvent != AnimationExitEvent.None)
                _combatHandler.OnAnimationStateExit(layerIndex, combatHandlerEvent);

            if (_movement != null && movementEvent != AnimationExitEvent.None)
                _movement.OnAnimationStateExit(layerIndex, movementEvent);

            if (_clinchHandler != null && clinchHandlerEvent != AnimationExitEvent.None)
                _clinchHandler.OnAnimationStateExit(layerIndex, clinchHandlerEvent);
        }
        else
        {
            if (_combatHandler != null && combatHandlerEvent != AnimationExitEvent.None)
                _combatHandler.OnAnimationStateExit(layerIndex, AnimationExitEvent.ClipInterrupted);

            if (_clinchHandler != null && clinchHandlerEvent != AnimationExitEvent.None)
                _clinchHandler.OnAnimationStateExit(layerIndex, AnimationExitEvent.ClipInterrupted);
        }

        // PickupDetector cleanup fires on every exit regardless of completion,
        // because isAction must always be cleared for the Animator to return to Idle.
        if (_pickupDetector != null && pickupDetectorEvent != AnimationExitEvent.None)
            _pickupDetector.OnAnimationStateExit(layerIndex, completed ? pickupDetectorEvent : AnimationExitEvent.ClipInterrupted);
    }

    /*
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        bool completed = stateInfo.normalizedTime >= 1.0f;
        // Get components dynamically to ensure we are talking to the RIGHT instance
        var receivers = animator.GetComponents<IAnimationStateReceiver>();
        foreach (var r in receivers)
        {
            r.OnAnimationNotify(this.specificEvent, completed);
        }
    }
    
     public interface IAnimationStateReceiver {
    void OnAnimationNotify(AnimationExitEvent evt, bool completed);
}
     
     */

}



