using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

/// <summary>
/// Behavior Graph Action — walks the actor around a looping <see cref="WaypointPath"/>.
///
/// RUNNING  : actor is patrolling (this node never self-terminates).
/// FAILURE  : brain is null or the waypoint path has no points.
///
/// Place this as the rightmost child of the root Try-In-Order selector so it acts
/// as the idle/patrol fallback when no target has been found.
/// </summary>
[NodeDescription(
    name: "Patrol Waypoints",
    story: "[Brain] patrols along [WayPoints]",
    category: "Last Ninja/Combat",
    id: "action_patrol_waypoints")]
public class PatrolWaypointsAction : Action
{
    [SerializeReference] public BlackboardVariable<ActorBrain>   Brain;
    [SerializeReference] public BlackboardVariable<WaypointPath> WayPoints;

    protected override Status OnStart()
    {
        ActorBrain brain = Brain?.Value;
        if (brain == null)                         return Status.Failure;
        if (WayPoints?.Value == null)              return Status.Failure;
        if (WayPoints.Value.Count == 0)            return Status.Failure;

        brain.currentState = ActorCombatState.Idle;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        ActorBrain brain = Brain?.Value;
        if (brain == null) return Status.Failure;

        return brain.TickPatrol(WayPoints?.Value) switch
        {
            ActorBrainStatus.Running => Status.Running,
            ActorBrainStatus.Success => Status.Success,
            _                        => Status.Failure,
        };
    }

    protected override void OnEnd()
    {
        Brain?.Value?.StopMovement();
    }
}
