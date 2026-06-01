using Unity.Behavior;
using UnityEngine;

/// <summary>
/// MonoBehaviour that drives all AI combat logic for a single actor.
/// Attached alongside <see cref="MovementComponent"/>, <see cref="CombatHandler"/>,
/// and <see cref="HealthComponent"/> on every enemy/companion prefab.
///
/// The Unity Behavior Graph owns the high-level decision flow; each Behavior node
/// calls the public API on this class rather than touching other components directly.
///
/// Key responsibilities:
///   - Scanning for and validating a combat target (<see cref="FindTarget"/>).
///   - Driving movement during chase (<see cref="TickChase"/>) and stalk (<see cref="TickStalk"/>).
///   - Exposing suppression / validity checks used by every Behavior node.
///   - Bridging to <see cref="ClinchHandler"/> for clinch-break logic.
/// </summary>
[RequireComponent(typeof(BehaviorGraphAgent))]
[RequireComponent(typeof(MovementComponent))]
[RequireComponent(typeof(CombatHandler))]
[RequireComponent(typeof(HealthComponent))]
public class ActorBrain : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector configuration
    // -------------------------------------------------------------------------

    [Header("Faction")]
    [Tooltip("Which faction this actor belongs to. Targets must be from a different faction.")]
    public Faction faction = Faction.Enemy;

    [Header("Detection")]
    [Tooltip("Radius within which this actor detects a target. Set ~2 units above stalkRange " +
             "so the enemy notices the player just before closing to stalk distance.")]
    public float detectionRange = 4.5f;

    [Tooltip("Layer mask used for the detection overlap sphere. Should include the Player and Companion layers.")]
    public LayerMask targetLayers;

    [Header("Movement Ranges")]
    [Tooltip("Distance at which the actor stops chasing and begins stalking (circling).")]
    public float stalkRange = 2.5f;

    [Tooltip("Distance at which the actor leaves stalk mode and resumes chasing.")]
    public float chaseResumeRange = 4f;

    [Header("Attack")]
    [Tooltip("Distance at which the actor is close enough to land a hit.")]
    public float attackRange = 1.4f;

    [Header("Clinch Break")]
    [Tooltip("Minimum seconds before this actor attempts a clinch break.")]
    public float clinchBreakMinTime = 0.8f;

    [Tooltip("Maximum seconds before a clinch break is attempted.")]
    public float clinchBreakMaxTime = 2.5f;

    [Tooltip("Probability (0-1) that a break attempt actually succeeds each try.")]
    [Range(0f, 1f)]
    public float clinchBreakChance = 0.5f;

    [Header("Patrol")]
    [Tooltip("Waypoint path this actor patrols when no target is found. " +
             "If left empty the component is located automatically in children.")]
    [SerializeField] private WaypointPath _waypointPathOverride;

    // -------------------------------------------------------------------------
    // Public state (read by Behavior nodes)
    // -------------------------------------------------------------------------

    /// <summary>Current high-level AI state. Written by Behavior nodes; read by this brain.</summary>
    public ActorCombatState currentState = ActorCombatState.Idle;

    /// <summary>The Transform the actor is currently engaging. May be null.</summary>
    public Transform currentTarget;

    // -------------------------------------------------------------------------
    // Component references
    // -------------------------------------------------------------------------

    private BehaviorGraphAgent _graph;
    private MovementComponent   _movement;
    private CombatHandler       _combat;
    private HealthComponent     _health;

    /// <summary>Reference to the ClinchHandler
    public ClinchHandler Clinch { get; private set; }

    // -------------------------------------------------------------------------
    // Internal clinch-break timer
    // -------------------------------------------------------------------------

    private float _clinchBreakTimer;
    private float _clinchBreakThreshold;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _graph    = GetComponent<BehaviorGraphAgent>();
        _movement = GetComponent<MovementComponent>();
        _combat   = GetComponent<CombatHandler>();
        _health   = GetComponent<HealthComponent>();
        Clinch    = GetComponent<ClinchHandler>();
    }

    private void Start()
    {
        WaypointPath path = _waypointPathOverride != null
            ? _waypointPathOverride
            : GetComponentInChildren<WaypointPath>();

        _graph.BlackboardReference.SetVariableValue("Brain",     this);
        _graph.BlackboardReference.SetVariableValue("Target",    (Transform)null);
        _graph.BlackboardReference.SetVariableValue("WayPoints", path);

        if (path == null)
            Debug.LogWarning($"[ActorBrain] No WaypointPath found on {name}. " +
                              "Patrol node will return Failure.", this);
    }

    // -------------------------------------------------------------------------
    // Target acquisition
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans nearby colliders for a valid, opposing-faction target within
    /// <see cref="detectionRange"/> and writes it to <see cref="currentTarget"/>.
    /// </summary>
    /// <returns>True if a target was found and cached, false otherwise.</returns>
    public bool FindTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange, targetLayers);

        float   bestDist   = float.MaxValue;
        Transform bestTarget = null;

        foreach (Collider col in hits)
        {
            if (col.gameObject == gameObject) continue;

            ITargetable targetable = col.GetComponent<ITargetable>();
            if (targetable == null || !targetable.IsValidTarget()) continue;
            if (targetable.GetFaction() == faction)               continue;

            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < bestDist)
            {
                bestDist   = dist;
                bestTarget = targetable.GetLockOnPoint() != null
                    ? targetable.GetLockOnPoint()
                    : col.transform;
            }
        }

        if (bestTarget != null)
        {
            currentTarget = bestTarget;
            return true;
        }

        return false;
    }

    // -------------------------------------------------------------------------
    // Validity / suppression guards (used by every Behavior node)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true when the actor should abort its current action —
    /// i.e. it is dead, stunned, or otherwise unable to act.
    /// </summary>
    public bool IsSuppressed()
    {
        if (_health == null) return false;
        return currentState == ActorCombatState.Suppressed || _health.IsDead;
    }

    /// <summary>
    /// Returns true when <see cref="currentTarget"/> still exists and is valid.
    /// </summary>
    public bool IsTargetValid()
    {
        if (currentTarget == null) return false;

        ITargetable targetable = currentTarget.GetComponentInParent<ITargetable>();
        return targetable != null && targetable.IsValidTarget();
    }

    // -------------------------------------------------------------------------
    // Movement ticks (called every frame by Behavior nodes)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Moves the actor toward <see cref="currentTarget"/> at full run speed.
    /// </summary>
    /// <returns>
    /// True  — still outside stalk range, keep chasing.<br/>
    /// False — inside stalk range, caller should hand off to Stalk.
    /// </returns>
    public bool TickChase()
    {
        if (currentTarget == null) return false;

        Vector3 toTarget = currentTarget.position - transform.position;
        float   dist     = toTarget.magnitude;

        if (dist <= stalkRange) return false;

        Vector3 moveDir = toTarget.normalized;
        _movement.ProcessMovement(moveDir, currentTarget.position);
        return true;
    }

    /// <summary>
    /// Circles the target at stalk distance using a sinusoidal sidestep.
    /// </summary>
    /// <returns>
    /// True  — still within chase-resume range, keep stalking.<br/>
    /// False — target has moved outside chase-resume range, caller should resume Chase.
    /// </returns>
    public bool TickStalk()
    {
        if (currentTarget == null) return false;

        Vector3 toTarget = currentTarget.position - transform.position;
        float   dist     = toTarget.magnitude;

        if (dist > chaseResumeRange) return false;

        // Oscillate laterally with a sine wave for a natural weaving feel
        Vector3 forward  = toTarget.normalized;
        Vector3 right    = Vector3.Cross(Vector3.up, forward);
        float   strafe   = Mathf.Sin(Time.time * 1.8f);
        Vector3 moveDir  = (forward * 0.3f + right * strafe).normalized;

        _movement.ProcessMovement(moveDir, currentTarget.position);
        return true;
    }

    /// <summary>Immediately halts all velocity on <see cref="MovementComponent"/>.</summary>
    public void StopMovement()
    {
        _movement?.ProcessMovement(Vector3.zero);
    }

    // -------------------------------------------------------------------------
    // Attack (called by AttackTargetAction)
    // -------------------------------------------------------------------------

    /// <summary>Returns true when the current target is within melee <see cref="attackRange"/>.</summary>
    public bool IsTargetInAttackRange()
    {
        if (currentTarget == null) return false;
        return Vector3.Distance(transform.position, currentTarget.position) <= attackRange;
    }

    /// <summary>
    /// Faces the target and fires a light-attack through <see cref="CombatHandler"/>.
    /// </summary>
    /// <returns>
    /// Running — attack animation is still playing.<br/>
    /// Success — attack finished (CombatHandler returned to Idle).<br/>
    /// Failure — target lost or actor suppressed.
    /// </returns>
    public ActorBrainStatus TickAttack()
    {
        if (IsSuppressed() || !IsTargetValid()) return ActorBrainStatus.Failure;

        // Face the target while attacking
        Vector3 toTarget = currentTarget.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(toTarget),
                Time.deltaTime * 12f);

        if (_combat.IsAttacking)
            return ActorBrainStatus.Running;

        _combat.ExecuteLightAttack();
        return ActorBrainStatus.Running;
    }

    // -------------------------------------------------------------------------
    // Patrol (called by PatrolWaypointsAction)
    // -------------------------------------------------------------------------

    private int _patrolIndex;

    /// <summary>
    /// Walks the actor through each waypoint in <paramref name="path"/> in sequence,
    /// looping back to the first point when the last is reached.
    /// </summary>
    /// <returns>Always returns Running (patrol never ends on its own).</returns>
    public ActorBrainStatus TickPatrol(WaypointPath path)
    {
        if (path == null || path.Count == 0) return ActorBrainStatus.Failure;

        Transform wp = path.Get(_patrolIndex);
        Vector3   toWp = wp.position - transform.position;
        toWp.y = 0f;

        if (toWp.sqrMagnitude < 0.2f * 0.2f)
        {
            _patrolIndex = (_patrolIndex + 1) % path.Count;
            wp   = path.Get(_patrolIndex);
            toWp = wp.position - transform.position;
            toWp.y = 0f;
        }

        _movement.ProcessMovement(toWp.normalized);
        return ActorBrainStatus.Running;
    }

    // -------------------------------------------------------------------------
    // Clinch-break logic (called by HandleClinchBreakAction)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Advances the clinch-break countdown. When the threshold is reached a
    /// break is attempted with probability <see cref="clinchBreakChance"/>.
    /// </summary>
    /// <param name="deltaTime">Time.deltaTime passed in from the Behavior node.</param>
    public void TickClinchBreak(float deltaTime)
    {
        if (Clinch == null || !Clinch.CanBreakClinch) return;

        // Initialise a fresh threshold the first time we enter this call
        if (_clinchBreakThreshold <= 0f)
            _clinchBreakThreshold = Random.Range(clinchBreakMinTime, clinchBreakMaxTime);

        _clinchBreakTimer += deltaTime;

        if (_clinchBreakTimer >= _clinchBreakThreshold)
        {
            _clinchBreakTimer     = 0f;
            _clinchBreakThreshold = 0f;

            if (Random.value <= clinchBreakChance)
                Clinch.BreakClinch();
        }
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Detection — enemy notices player inside this ring
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Stalk — enemy stops chasing and begins circling inside this ring
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, stalkRange);

        // Chase-resume — enemy resumes chasing if target moves outside this ring
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, chaseResumeRange);

        // Attack — enemy can land a hit inside this ring
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
#endif
}
