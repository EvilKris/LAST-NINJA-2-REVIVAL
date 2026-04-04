using UnityEngine;

[RequireComponent(typeof(Animator))]
public class RandomAnimatorController : MonoBehaviour,ISMBReceiver
{
    // This script randomly triggers different idle actions on an Animator after random intervals.
    // It assumes the Animator has an integer parameter "i_ActionIndex" to select the action and a trigger "t_DoAction" to start it.
    // Each action clip should return to idle

    public Animator animator;
    public float minWait = 2f;
    public float maxWait = 6f;
    public int actionCount = 2; // Number of different idle actions 

    private float timer;
    private bool isPlayingAction;

    void Start()
    {
        ResetTimer();
    }

    void Update()
    {
        if (isPlayingAction) return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            PlayRandomAction();
        }
    }

    void PlayRandomAction()
    {
        int action = Random.Range(0, actionCount + 1); // number of actions

        animator.SetInteger("i_ActionIndex", action);
        animator.SetTrigger("t_DoAction");

        isPlayingAction = true;
    }

    // Called from animation event at END of each action clip
    public void OnActionFinished()
    {
        isPlayingAction = false;
        ResetTimer();
    }

    void ResetTimer()
    {
        timer = Random.Range(minWait, maxWait);
    }

    public void OnAnimationSignal(string functionName, AnimationStateEvent.StateEvent data)
    {
        if(functionName == "ResetTimer")
        {
            OnActionFinished();
        }
    }
}