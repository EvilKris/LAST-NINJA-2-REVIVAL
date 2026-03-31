using UnityEngine;
using System.Linq;

public class SMB_AnimationEventNotifier : StateMachineBehaviour
{
    // This SMB allows you to specify events that trigger on enter, update (at specific normalized times), and exit of an animation state.
    // It's rock-solid but the only weakness is that it uses strings to identify the function to call on receivers
    // so it's up to you to ensure those are correct and that the receivers implement the expected functions. But this design keeps it super flexible and decoupled.

    public AnimationStateEvent.StateEvent[] OnEnterEvents;
    public AnimationStateEvent.StateEvent[] OnUpdateEvents;
    public AnimationStateEvent.StateEvent[] OnExitEvents;

    // A tiny internal class to track state per-animator without Dictionaries
    private class StateTracker : MonoBehaviour
    {
        public float LastTime;
        public ISMBReceiver[] Receivers;
    }

    private void OnValidate()
    {
        if (OnUpdateEvents != null)
            OnUpdateEvents = OnUpdateEvents.OrderBy(e => e.NormalizedTime).ToArray();
    }

    private StateTracker GetTracker(Animator animator)
    {
        var tracker = animator.GetComponent<StateTracker>();
        if (tracker == null)
        {
            tracker = animator.gameObject.AddComponent<StateTracker>();
            tracker.hideFlags = HideFlags.HideInInspector; // Keep it clean
            tracker.Receivers = animator.GetComponentsInChildren<ISMBReceiver>();
        }
        return tracker;
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var tracker = GetTracker(animator);
        tracker.LastTime = -0.01f; // Ensure 0.0 events fire

        foreach (var e in OnEnterEvents) e.Invoke(tracker.Receivers);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var tracker = GetTracker(animator);
        ProcessUpdateEvents(tracker, stateInfo.normalizedTime, stateInfo.loop);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var tracker = GetTracker(animator);

        // 1. Force-fire any Update events that were skipped (e.g. hitbox-off)
        // We simulate the time reaching 1.0 to trigger everything remaining
        ProcessUpdateEvents(tracker, 1.01f, false, true);

        // 2. Fire standard Exit events
        foreach (var e in OnExitEvents) e.Invoke(tracker.Receivers);

        tracker.LastTime = -0.01f; // Reset for next use
    }

    private void ProcessUpdateEvents(StateTracker tracker, float currentTime, bool isLooping, bool isExiting = false)
    {
        float lastTime = tracker.LastTime;

        // Handle looping wrap-around
        if (!isExiting && lastTime > 1.0f)
        {
            if (!isLooping) return;
            lastTime = (lastTime % 1.0f) - 1.0f;
        }

        float effectiveCurrent = currentTime % 1.0f;
        if (isExiting || (currentTime >= 1.0f && !isLooping)) effectiveCurrent = 1.01f;

        foreach (var e in OnUpdateEvents)
        {
            if (isExiting && !e.ForceCallOnExit) continue;
            if (!isExiting && lastTime > 1.0f && !e.RepeatOnLoop) continue;

            // Trigger if the event time falls between our last check and now
            if (lastTime < e.NormalizedTime && effectiveCurrent >= e.NormalizedTime)
            {
                e.Invoke(tracker.Receivers);
            }
        }

        tracker.LastTime = currentTime;
    }
}
