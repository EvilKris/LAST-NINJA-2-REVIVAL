using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

/// <summary>
/// Behavior Graph Action — chases the target at full speed until stalkRange is reached.
/// 
/// RUNNING  : still closing in on target.
/// SUCCESS  : reached stalk range — hand off to StalkTargetAction.
/// FAILURE  : target lost / actor suppressed.
/// 
/// Pair with a Repeat node if you want looping re-acquisition after stalk completes.
/// </summary>
[NodeDescription(
    name: "Chase Target",
    story: "[Brain] chases [Target]",
    category: "Last Ninja/Combat",
    id: "action_chase_target")]
public class ChaseTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<ActorBrain> Brain;
    [SerializeReference] public BlackboardVariable<Transform> Target;

    protected override Status OnStart()
    {
        ActorBrain brain = Brain?.Value;
        if (brain == null) return Status.Failure;

        // Sync the blackboard target into the brain in case it was set externally
        if (Target?.Value != null)
            brain.currentTarget = Target.Value;

        brain.currentState = ActorCombatState.Chasing;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        ActorBrain brain = Brain?.Value;
        if (brain == null) return Status.Failure;

        if (brain.IsSuppressed())   return Status.Failure;
        if (!brain.IsTargetValid()) return Status.Failure;

        // TickChase returns false once stalk range is reached
        bool stillChasing = brain.TickChase();
        return stillChasing ? Status.Running : Status.Success;
    }

    protected override void OnEnd()
    {
        // Stop movement when this node exits (prevents sliding if the graph aborts)
        Brain?.Value?.StopMovement();
    }
}
