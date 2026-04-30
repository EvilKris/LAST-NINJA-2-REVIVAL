using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

/// <summary>
/// Behavior Graph Action — runs while the actor is grabbed in a clinch.
/// Counts down a timer and randomly attempts to break free.
/// 
/// RUNNING  : still in clinch (CanBreakClinch == true).
/// SUCCESS  : clinch ended (CanBreakClinch became false, i.e. released or broke out).
/// FAILURE  : brain is null.
///
/// Place this at the TOP of your root selector so it has the highest priority.
/// </summary>
[NodeDescription(
    name: "Handle Clinch Break",
    story: "[Brain] tries to break clinch",
    category: "Last Ninja/Combat",
    id: "action_handle_clinch_break")]
public class HandleClinchBreakAction : Action
{
    [SerializeReference] public BlackboardVariable<ActorBrain> Brain;

    protected override Status OnUpdate()
    {
        ActorBrain brain = Brain?.Value;
        if (brain == null) return Status.Failure;

        if (brain.Clinch == null || !brain.Clinch.CanBreakClinch)
            return Status.Success;  // No longer grabbed

        brain.TickClinchBreak(Time.deltaTime);
        return Status.Running;
    }
}
