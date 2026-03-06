using UnityEngine;

public class AnimationStateExitNotifier : StateMachineBehaviour
{
    [Tooltip("Event passed to IAnimationStateListener.OnAnimationStateExit. " +
             "Use this to distinguish between different exit notifications.")]
    public AnimationExitEvent exitEvent = AnimationExitEvent.None;

    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        //remember - this activates even if the state is interrupted by a transition, so it can be used to trigger events on interruption as well

        var listeners = animator.GetComponents<IAnimationStateListener>();

        if (exitEvent == AnimationExitEvent.None) return;

        foreach (var listener in listeners)
        {
            listener.OnAnimationStateExit(layerIndex, exitEvent);
        }
    }
}
