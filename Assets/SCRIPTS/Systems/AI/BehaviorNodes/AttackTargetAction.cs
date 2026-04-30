using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

/// <summary>
/// Behavior Graph Action — executes a melee light-attack combo against the current target.
///
/// RUNNING  : attack animation is playing.
/// SUCCESS  : attack animation finished; the graph can move to the next node.
/// FAILURE  : target lost or actor suppressed mid-attack.
///
/// Place this as the child of a "Pass &amp; Abort Lower Priority If / Is Target In Range"
/// decorator so it fires only when already at melee distance.
/// </summary>
[NodeDescription(
    name: "Attack Target",
    story: "[Brain] attacks [Target]",
    category: "Last Ninja/Combat",
    id: "action_attack_target")]
[Serializable]
public class AttackTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<ActorBrain>  Brain;
    [SerializeReference] public BlackboardVariable<Transform>   Target;

    protected override Status OnStart()
    {
        ActorBrain brain = Brain?.Value;
        if (brain == null) return Status.Failure;

        if (Target?.Value != null)
            brain.currentTarget = Target.Value;

        // Fail immediately so Try In Order falls through to Chase
        if (!brain.IsTargetInAttackRange()) return Status.Failure;

        brain.currentState = ActorCombatState.Attacking;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        ActorBrain brain = Brain?.Value;
        if (brain == null) return Status.Failure;

        return brain.TickAttack() switch
        {
            ActorBrainStatus.Running => Status.Running,
            ActorBrainStatus.Success => Status.Success,
            _                        => Status.Failure,
        };
    }

    protected override void OnEnd()
    {
        ActorBrain brain = Brain?.Value;
        if (brain != null && brain.currentState == ActorCombatState.Attacking)
            brain.currentState = ActorCombatState.Idle;

        Brain?.Value?.StopMovement();
    }
}
