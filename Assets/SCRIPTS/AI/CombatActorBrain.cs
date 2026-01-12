using UnityEngine;

/// <summary>
/// Defines the AI behavior states for combat actors.
/// </summary>
public enum ActorCombatState 
{ 
    Idle,     // Not engaged, standing still or patrolling
    Chasing,  // Moving directly towards target at full speed
    Stalking  // Circling/weaving around target in close range (Dark Souls-style)
}

/// <summary>
/// AI brain for combat actors (enemies and companions).
/// Handles target detection, state-based behavior, and combat positioning.
/// Uses a simple state machine: Idle → Chasing → Stalking → back to Chasing if too far.
/// Works with MovementComponent for physics-based movement.
/// </summary>
public class CombatActorBrain : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("Which faction to target. Set to 'Player' for enemies, 'Enemy' for companions.")]
    public Faction targetFaction = Faction.Enemy;
    
    [Tooltip("Current target's lock-on point. Auto-assigned when a valid target is found.")]
    public Transform currentTarget;
    
    [Tooltip("Maximum distance to detect and engage targets (in meters).")]
    public float detectionRange = 15f;

    [Header("Current Behavior")]
    [Tooltip("Current AI state. Automatically transitions based on distance to target.")]
    public ActorCombatState currentState = ActorCombatState.Idle;

    // Component references
    private MovementComponent _mover;
    private HealthComponent _health;

    /// <summary>
    /// Cache component references.
    /// </summary>
    private void Awake()
    {
        _mover = GetComponent<MovementComponent>();
        _health = GetComponent<HealthComponent>();
    }

    /// <summary>
    /// Main AI update loop. Runs in FixedUpdate for consistent physics-based behavior.
    /// Handles target validation, target acquisition, and state-based behavior execution.
    /// </summary>
    private void FixedUpdate()
    {
        // Don't process AI if dead or movement is locked (e.g., during attack animations)
        if ((_health != null && _health.IsDead) || _mover.speedMultiplier <= 0.01f) 
            return;

        // Validate current target or find a new one
        if (currentTarget == null || !IsTargetValid(currentTarget))
        {
            FindNewTarget();
            
            // No valid target found - remain idle
            if (currentTarget == null)
            {
                _mover.ProcessMovement(Vector3.zero);
                return;
            }
        }

        // Calculate distance to target for state transitions
        float distance = Vector3.Distance(transform.position, currentTarget.position);

        // Execute behavior based on current state
        switch (currentState)
        {
            case ActorCombatState.Idle:
                // Transition to chasing when target enters detection range
                if (distance < detectionRange) 
                    currentState = ActorCombatState.Chasing;
                break;

            case ActorCombatState.Chasing:
                HandleChase(distance);
                break;

            case ActorCombatState.Stalking:
                HandleStalk(distance);
                break;
        }
    }

    /// <summary>
    /// Chasing behavior: Move directly towards the target at full speed.
    /// Transitions to Stalking when close enough for combat engagement.
    /// </summary>
    /// <param name="distance">Current distance to target</param>
    private void HandleChase(float distance)
    {
        // Calculate direction to target
        Vector3 dir = (currentTarget.position - transform.position).normalized;
        
        // Move directly towards target
        _mover.ProcessMovement(dir);
        _mover.RotateTowardsDirection(dir);

        // Transition to stalking when within striking range
        if (distance <= 5f) 
            currentState = ActorCombatState.Stalking;
    }

    /// <summary>
    /// Stalking behavior: Dark Souls-style combat dance.
    /// Maintains optimal combat distance while circling/weaving around the target.
    /// Always faces the target while moving (strafe behavior).
    /// </summary>
    /// <param name="distance">Current distance to target</param>
    private void HandleStalk(float distance)
    {
        Vector3 dirToTarget = (currentTarget.position - transform.position).normalized;

        // Calculate movement based on distance to target
        Vector3 moveInput = Vector3.zero;

        // Distance-based approach/retreat logic
        if (distance > 3.5f) 
            moveInput += dirToTarget;      // Too far - close in
        else if (distance < 2.5f) 
            moveInput -= dirToTarget;      // Too close - back off

        // Add sideways weaving/circling using sine wave
        // Creates unpredictable side-to-side movement
        moveInput += 0.6f * Mathf.Sin(Time.time * 2f) * transform.right;

        // Use target-oriented movement mode (always faces target while moving)
        _mover.ProcessMovement(moveInput.normalized, currentTarget.position);

        // Transition back to chasing if target moves too far away
        if (distance > 7f) 
            currentState = ActorCombatState.Chasing;
    }

    /// <summary>
    /// Checks if the given target is still valid for engagement.
    /// A target is invalid if it's dead, destroyed, or doesn't implement ITargetable.
    /// </summary>
    /// <param name="t">Target transform to validate</param>
    /// <returns>True if target is valid, false otherwise</returns>
    private bool IsTargetValid(Transform t) => t.GetComponent<ITargetable>()?.IsValidTarget() ?? false;

    /// <summary>
    /// Scans the detection range for valid targets matching the target faction.
    /// Automatically assigns the first valid target found.
    /// Called when current target is lost or becomes invalid.
    /// </summary>
    private void FindNewTarget()
    {
        // Scan for all colliders in detection radius
        Collider[] cols = Physics.OverlapSphere(transform.position, detectionRange);
        
        foreach (var col in cols)
        {
            // Check if collider has ITargetable component
            if (col.TryGetComponent<ITargetable>(out var target))
            {
                // Verify faction matches and target is valid (alive, etc.)
                if (target.GetFaction() == targetFaction && target.IsValidTarget())
                {
                    // Assign target's lock-on point (usually chest/head position)
                    currentTarget = target.GetLockOnPoint();
                    break;
                }
            }
        }
    }
}