using UnityEngine;

public class AnimationStateExitNotifier : StateMachineBehaviour
{
    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        var listeners = animator.GetComponents<IAnimationStateListener>();

        foreach (var listener in listeners)
        {
            listener.OnAnimationStateExit(stateInfo.shortNameHash, layerIndex);
        }
    }
}
