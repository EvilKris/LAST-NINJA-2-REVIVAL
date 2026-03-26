using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MovementComponent : MonoBehaviour, IAnimationStateListener
{

    [Header("Speed Modifiers")]
    [Tooltip("Controls movement and rotation speed. 1.0 = normal, 0.5 = half speed, 2.0 = double speed.")]
    [Range(1f, 10f)]
    public float movementSpeed = 5f;
    [Range(0.1f, 2f)]
    [Tooltip("Modifies the speed of movement animations (walking, running). 1.0 = normal, 0.5 = half speed, 2.0 = double speed.")]
    public float movementAnimSpeedModifier = 1;
    [Tooltip("Controls attack animation speed (punches, kicks, etc.). 1.0 = normal, 0.5 = half speed, 2.0 = double speed.")]
    [Range(0.1f, 3f)]
    public float attackSpeed = 1f;

    [Header("Movement Settings")]
    public float rotationSpeed = 12f;

    [Header("Root Motion Settings")]
    [Tooltip("Use root motion for movement instead of velocity-based movement")]
    public bool useRootMotion = false;
    [Tooltip("Additional scaling applied to root motion movement")]
    public float rootMotionScale = 1f;

    [Header("Animation Smoothing")]
    [Tooltip("How fast the animator X/Y float parameters ramp between values. Higher = snappier, lower = smoother.")]
    [Range(1f, 20f)]
    public float animatorDampSpeed = 10f;

    [Tooltip("Dust Particles")]
    public ParticleSystem dustParticles;

    private Rigidbody _rb;
    private Animator _animator;
    private CombatHandler _combatHandler;

    [HideInInspector] public bool canRotate = true;
    [HideInInspector] public Vector3 currentMoveDir;
    [HideInInspector] public bool isMovementLocked = false;
    [HideInInspector] public bool isImmobilized = false;
    [HideInInspector] public bool isInFlight = false;
    [HideInInspector] public bool isClinchActive = false;
    [HideInInspector] public bool CanBeClinched = true;
    [HideInInspector] public MovementComponent syncAnimationSource = null;
    [HideInInspector] public bool syncAnimatorSpeed = false;

    // Animator Parameter Hashes (cached for performance)
    private int _hashIsRunning;
    private int _hashXAxis;
    private int _hashYAxis;

    // Cached animator values to avoid redundant SetFloat/SetBool calls
    private bool _lastIsRunning;
    private float _lastXAxis;
    private float _lastYAxis;
   

    
    private void OnDisable()
    {
        // When MovementComponent is disabled, stop all physics movement immediately
        // This prevents the Rigidbody from retaining velocity and sliding
        if (_rb != null && !isInFlight)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();

        // CRITICAL: Disable root motion by default - MovementComponent handles ALL movement via physics
        // We manually handle root motion in OnAnimatorMove when useRootMotion is enabled
       // if (_animator != null)
         //   _animator.applyRootMotion = false;

        // Constrain rotation to prevent tipping over
        _rb.constraints = RigidbodyConstraints.FreezeRotation;

        _combatHandler = GetComponent<CombatHandler>();

        // Cache animator parameter hashes for better performance
        _hashIsRunning = Animator.StringToHash("isRunningBool");
        _hashXAxis = Animator.StringToHash("Input_XFloat");
        _hashYAxis = Animator.StringToHash("Input_YFloat");
    }

    /// <summary>
    /// MODE 1: FREESTYLE (Gauntlet Style)
    /// Used when exploring or moving without a specific target.
    /// </summary>
    public void ProcessMovement(Vector3 moveDir)
    {
        if (isImmobilized)
        {
            StopVelocity();
            return;
        }

        if (isMovementLocked)
        {
            SyncAnimationFromSource();
            return;
        }

        currentMoveDir = moveDir;
        float sqrMagnitude = moveDir.sqrMagnitude;
        bool isMoving = sqrMagnitude > 0.0001f;

        UpdateAnimatorBooleans(isMoving);

        if (isInFlight) return;

        if (isMoving)
        {
            // Only apply velocity if NOT using root motion
            // Root motion will handle movement in OnAnimatorMove
            if (!useRootMotion && !isClinchActive)
            {
                float yVel = _rb.linearVelocity.y;
                _rb.linearVelocity = new Vector3(moveDir.x * movementSpeed, yVel, moveDir.z * movementSpeed);
            }

            if (!isClinchActive)
            {
                RotateTowardsDirection(moveDir);
                // Freestyle uses Y-axis for speed, X is ignored
                float magnitude = Mathf.Sqrt(sqrMagnitude);
                SetAnimatorFloat(_hashYAxis, magnitude);
                SetAnimatorFloat(_hashXAxis, 0f);
            }
            else
            {
                // During clinch: rotate toward input and use local-space X/Y so the
                // blend tree drives all directions (forward, back, strafe) correctly.
                RotateTowardsDirection(moveDir);
                Vector3 localDir = transform.InverseTransformDirection(moveDir);
                SetAnimatorFloat(_hashXAxis, localDir.x);
                SetAnimatorFloat(_hashYAxis, localDir.z);
            }
        }
        else
        {
            StopVelocity();
        }
    }

    /// <summary>
    /// MODE 2: TARGETED (Dark Souls Style)
    /// Used by CombatActorBrain or Player Lock-On.
    /// </summary>
    public void ProcessMovement(Vector3 moveDir, Vector3 lookAtPos)
    {
        if (isImmobilized)
        {
            StopVelocity();
            return;
        }

        if (isMovementLocked)
        {
            SyncAnimationFromSource();
            return;
        }

        currentMoveDir = moveDir;
        bool isMoving = moveDir.sqrMagnitude > 0.0001f;

        UpdateAnimatorBooleans(isMoving);

        // Only apply velocity if NOT using root motion
        // Root motion will handle movement in OnAnimatorMove
        if (!useRootMotion && !isInFlight && !isClinchActive)
        {
            float yVel = _rb.linearVelocity.y;
            _rb.linearVelocity = new Vector3(moveDir.x * movementSpeed, yVel, moveDir.z * movementSpeed);
        }

        // Always face the Target
        Vector3 dirToTarget = (lookAtPos - transform.position);
        dirToTarget.y = 0;
        RotateTowardsDirection(dirToTarget);

        // Calculate Local Directions for Strafing (maps world movement to 2D Blend Tree)
        Vector3 localDir = transform.InverseTransformDirection(moveDir);
        SetAnimatorFloat(_hashXAxis, localDir.x);
        SetAnimatorFloat(_hashYAxis, localDir.z);
    }

    /// <summary>
    /// Called by Unity when Animator updates - handle root motion here
    /// Uses root motion's magnitude (animation speed) but applies it in currentMoveDir direction
    /// This prevents the "offset drift" problem while preserving animation-driven speed
    /// </summary>
    /// 


    private void OnAnimatorMove()
    {
        if (isInFlight) return;

        // During acrobatic moves, CombatHandler.FixedUpdate owns all positioning
        // (forward motion via motionCurve, vertical arc via verticalMotionCurve).
        // Skip root motion application here to prevent the two systems fighting.
        if (_combatHandler != null && _combatHandler.State == CombatState.Acrobatic) return;

        // Strip vertical root motion so physics gravity is never overridden.
        // After MovePosition, re-apply the pre-existing Y velocity so gravity
        // continues to accumulate naturally (MovePosition would otherwise zero it).

       

        float yVelocity = _rb.linearVelocity.y;
        Vector3 delta = _animator.deltaPosition;
        delta.y = 0f;
        _rb.MovePosition(_rb.position + delta);
        _rb.MoveRotation(_rb.rotation * _animator.deltaRotation);
        Vector3 vel = _rb.linearVelocity;
        vel.y = yVelocity;
        _rb.linearVelocity = vel;
    }


    public void RotateTowardsDirection(Vector3 dir)
    {
        if (!canRotate || dir.sqrMagnitude < 0.01f)
            return;

        float effectiveRotationSpeed = isClinchActive ? rotationSpeed * 0.25f : rotationSpeed;
        Quaternion targetRot = Quaternion.LookRotation(dir);
        _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, effectiveRotationSpeed * Time.fixedDeltaTime));
    }

    private void UpdateAnimatorBooleans(bool isMoving)
    {
        if (_lastIsRunning != isMoving)
        {
            _animator.SetBool(_hashIsRunning, isMoving);
            _lastIsRunning = isMoving;
        }
    }

    private void SetAnimatorFloat(int hash, float target)
    {
        float current = hash == _hashXAxis ? _lastXAxis : _lastYAxis;
        float smoothed = Mathf.MoveTowards(current, target, animatorDampSpeed * Time.deltaTime);

        // Only write to the Animator when the value has changed meaningfully
        if (Mathf.Abs(current - smoothed) > 0.0001f)
        {
            _animator.SetFloat(hash, smoothed);
            if (hash == _hashXAxis)
                _lastXAxis = smoothed;
            else
                _lastYAxis = smoothed;
        }
    }

    private void StopVelocity()
    {
        if (!isClinchActive && !isInFlight)
        {
            // Preserve vertical velocity so gravity continues to apply while idle
            Vector3 vel = _rb.linearVelocity;
            vel.x = 0f;
            vel.z = 0f;
            _rb.linearVelocity = vel;
        }
        else if (isInFlight)
            return;

        SetAnimatorFloat(_hashXAxis, 0f);
        SetAnimatorFloat(_hashYAxis, 0f);
    }

    public void ZeroVelocity()
    {
        if (isInFlight || isClinchActive) return;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    public void ZeroAnimatorInputs()
    {
        _lastXAxis = 1f;  // force dirty so SetAnimatorFloat actually writes
        _lastYAxis = 1f;
        SetAnimatorFloat(_hashXAxis, 0f);
        SetAnimatorFloat(_hashYAxis, 0f);
        _lastIsRunning = true; // force dirty
        _animator.SetBool(_hashIsRunning, false);
        _lastIsRunning = false;
    }

    private void SyncAnimationFromSource()
    {
        if (!isInFlight)
            _rb.linearVelocity = Vector3.zero;

        if (syncAnimationSource != null)
        {
            // Keep currentMoveDir in sync so any system that reads it (e.g. root motion) gets
            // the same direction the attacker is moving in.
            currentMoveDir = syncAnimationSource.currentMoveDir;

            // Mirror the attacker's animation parameters for synchronized movement.
            // X is negated because the enemy faces 180 degrees opposite, so local left/right is flipped.
            // Y is not negated: both actors share the same forward/backward blend direction.
            SetAnimatorFloat(_hashXAxis, -syncAnimationSource._lastXAxis);
            SetAnimatorFloat(_hashYAxis, syncAnimationSource._lastYAxis);

            // Sync running state as well
            bool sourceIsRunning = syncAnimationSource._lastIsRunning;
            if (_lastIsRunning != sourceIsRunning)
            {
                _animator.SetBool(_hashIsRunning, sourceIsRunning);
                _lastIsRunning = sourceIsRunning;
            }
        }
        else
        {
            // No source to sync from, stop animation
            SetAnimatorFloat(_hashXAxis, 0f);
            SetAnimatorFloat(_hashYAxis, 0f);
        }
    }


    private void Start()
    {
        // Safety net: if this entity dies while immobilized (e.g. killed mid-throw recovery),
        // the AnimationStateExitNotifier will never fire, so clear the flag via the death event.
        if (TryGetComponent<HealthComponent>(out var health))
            health.OnDeath += () => isImmobilized = false;
    }

    private void Update()
    {
        // When locked with a sync source (e.g. enemy grabbed in a clinch), drive animator
        // floats every frame directly — no external caller needed since the AI brain is disabled.
        if (isMovementLocked && syncAnimationSource != null)
            SyncAnimationFromSource();

        // When movement is locked or a clinch is active, an external system owns animator.speed.
        if (isMovementLocked || isClinchActive) return;

        if (_combatHandler == null || !_combatHandler.enabled)
        {
            _animator.speed = movementSpeed * movementAnimSpeedModifier;
            return;
        }

        // Priority: Acrobatic > Attack > Movement
        if (_combatHandler.State == CombatState.Acrobatic)
            _animator.speed = _combatHandler.acrobaticSpeed;
        else if (_combatHandler.IsAttacking)
            _animator.speed = attackSpeed;
        else
            _animator.speed = movementSpeed * movementAnimSpeedModifier;
    }

    /// <summary>
    /// Called by AnimationStateExitNotifier when a state with that behaviour exits.
    /// Filter on exitEvent to react only to relevant notifications.
    /// </summary>
    public void OnAnimationStateExit(int layerIndex, AnimationExitEvent exitEvent)
    {

        if (exitEvent == AnimationExitEvent.EndImmobilized)
        {
            isImmobilized = false;
            CanBeClinched = true; // reset clinch vulnerability when immobilization ends    
        }
    }
}