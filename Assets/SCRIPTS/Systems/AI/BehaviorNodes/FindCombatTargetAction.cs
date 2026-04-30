using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

/// <summary>
/// Behavior Graph Action — scans for a valid target of the correct faction.
/// 
/// SUCCESS  : target found and written to the Target blackboard variable.
/// FAILURE  : no valid target is in detection range.
/// 
/// Wire this as the first node after the root selector so the tree only
/// proceeds to movement nodes once a target exists.
/// </summary>
[NodeDescription(
    name: "Find Combat Target",
    story: "Find a [Target] using [Brain]",
    category: "Last Ninja/Combat",
    id: "action_find_combat_target")]
public class FindCombatTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<ActorBrain> Brain;
    [SerializeReference] public BlackboardVariable<Transform> Target;

    protected override Status OnUpdate()
    {
        ActorBrain brain = Brain?.Value;
        if (brain == null) return Status.Failure;

        if (brain.FindTarget())
        {
            if (Target != null) Target.Value = brain.currentTarget;
            return Status.Success;
        }

        return Status.Failure;
    }
}
