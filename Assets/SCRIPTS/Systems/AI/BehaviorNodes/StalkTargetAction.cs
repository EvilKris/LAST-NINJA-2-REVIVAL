using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

/// <summary>
/// Behavior Graph Action — Dark Souls-style combat weave around target.
/// Maintains stalk distance and circles with a sine-wave sidestep.
/// 
/// RUNNING  : still stalking within range.
/// SUCCESS  : target moved beyond chaseResumeRange — return to ChaseTargetAction.
/// FAILURE  : target lost / actor suppressed.
/// </summary>
[NodeDescription(
    name: "Stalk Target",
    story: "[Brain] stalks [Target]",
    category: "Last Ninja/Combat",
    id: "action_stalk_target")]
public class StalkTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<ActorBrain> Brain;
    [SerializeReference] public BlackboardVariable<Transform> Target;

    protected override Status OnStart()
    {
        ActorBrain brain = Brain?.Value;
        if (brain == null) return Status.Failure;

        if (Target?.Value != null)
            brain.currentTarget = Target.Value;

        brain.currentState = ActorCombatState.Stalking;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        ActorBrain brain = Brain?.Value;
        if (brain == null) return Status.Failure;

        if (brain.IsSuppressed())   return Status.Failure;
        if (!brain.IsTargetValid()) return Status.Failure;

        // TickStalk returns false once the target has moved out of chase-resume range
        bool stillStalking = brain.TickStalk();
        return stillStalking ? Status.Running : Status.Success;
    }

    protected override void OnEnd()
    {
        Brain?.Value?.StopMovement();
    }
}
