using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MovementComponent : MonoBehaviour
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
    [Tooltip("Controls acrobatic animation speed (flips, climb, etc.). 1.0 = normal, 0.5 = half speed, 2.0 = double speed.")]
    [Range(1.5f, 10f)]
    public float acrobaticSpeed = 5f;   

    [Header("Movement Settings")]

    public float rotationSpeed = 12f;

    private Rigidbody _rb;
    private Animator _animator;

    //[HideInInspector] public float speedMultiplier = 1.0f; // Controlled by CombatHandler
   // [HideInInspector] public float healthSpeedModifier = 1.0f; // Controlled by HealthComponent
    [HideInInspector] public bool canRotate = true; // Controlled by CombatHandler during attacks

    // Current movement direction (exposed for other systems like ClinchHandler)
    [HideInInspector] public Vector3 currentMoveDir;

    // Animator Parameter Hashes (cached for performance)
    private int _hashIsRunning;
    private int _hashXAxis;
    private int _hashYAxis;
    private CombatHandler _combatHandler;

    // Cached animator values to avoid redundant SetFloat/SetBool calls
    private bool _lastIsRunning;
    private float _lastXAxis;
    private float _lastYAxis;

    // ClinchHandler reference (cached) 
    private ClinchHandler _clinchCache;
    private ClinchHandler Clinch
    {
        get
        {
            // If we don't have it cached, try to find it
            if (_clinchCache == null) _clinchCache = GetComponent<ClinchHandler>();
            return _clinchCache;
        }
    }


    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();

        // Constrain rotation so the Ninja doesn't tip over
        // _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;

        _combatHandler = GetComponent<CombatHandler>();

        // Cache animator parameter hashes for better performance
        _hashIsRunning = Animator.StringToHash("isRunningBool");
        _hashXAxis = Animator.StringToHash("Input_XFloat");
        _hashYAxis = Animator.StringToHash("Input_YFloat");
    }

    // --- MODE 1: FREESTYLE (Gauntlet Style) ---
    // Used when exploring or moving without a specific target.
    public void ProcessMovement(Vector3 moveDir)
    {
        currentMoveDir = moveDir;

        #region Clinch Check - Being Grabbed
        // If being grabbed by another entity, make Rigidbody kinematic to allow parenting
        if (_animator != null && _animator.GetBool("b_IsBeingGrabbed"))
        {
            if (!_rb.isKinematic)
                _rb.isKinematic = true;
            return;
        }
        else if (_rb.isKinematic)
        {
            // Restore physics when released
            _rb.isKinematic = false;
        }
        #endregion

        #region Clinch Check - Grabbing Others
        // Use the Lazy Getter. If the style doesn't support clinching, 
        // Clinch will be null and this check is skipped.
        if (Clinch != null && Clinch.IsClinching)
        {
            _rb.linearVelocity = moveDir * (movementSpeed * 0.3f);
            return;
        }
        #endregion

        float sqrMagnitude = moveDir.sqrMagnitude;
        bool isMoving = sqrMagnitude > 0.0001f; // sqrMagnitude of 0.01 is ~0.0001

        UpdateAnimatorBooleans(isMoving);

        //if (speedMultiplier <= 0.01f) { StopVelocity(); return; }

        if (isMoving)
        {
            _rb.linearVelocity = moveDir * movementSpeed;
            //_rb.linearVelocity = moveDir * (movementSpeed * speedMultiplier) * healthSpeedModifier);
            RotateTowardsDirection(moveDir);

            // Freestyle uses Y-axis for speed, X is ignored
            float magnitude = Mathf.Sqrt(sqrMagnitude);
            SetAnimatorFloat(_hashYAxis, magnitude);
            SetAnimatorFloat(_hashXAxis, 0f);
        }
        else
        {
            StopVelocity();
        }
    }

    // --- MODE 2: TARGETED (Dark Souls Style) ---
    // Used by CombatActorBrain or Player Lock-On.
    public void ProcessMovement(Vector3 moveDir, Vector3 lookAtPos)
    {
        currentMoveDir = moveDir;
        
        // If being grabbed by another entity, make Rigidbody kinematic to allow parenting
        if (_animator != null && _animator.GetBool("b_IsBeingGrabbed"))
        {
            if (!_rb.isKinematic)
                _rb.isKinematic = true;
            return;
        }
        else if (_rb.isKinematic)
        {
            // Restore physics when released
            _rb.isKinematic = false;
        }
        
        bool isMoving = moveDir.sqrMagnitude > 0.0001f;
        UpdateAnimatorBooleans(isMoving);

        //  if (speedMultiplier <= 0.01f) { StopVelocity(); return; }

        // 1. Move the Physics Body
        _rb.linearVelocity = moveDir * movementSpeed;
        // _rb.linearVelocity = moveDir * (movementSpeed * speedMultiplier * healthSpeedModifier);

        // 2. Always face the Target
        Vector3 dirToTarget = (lookAtPos - transform.position);
        dirToTarget.y = 0;
        RotateTowardsDirection(dirToTarget);

        // 3. Calculate Local Directions for Strafing
        // This maps world movement to your 2D Blend Tree nodes (Forward, Back, Left, Right)
        Vector3 localDir = transform.InverseTransformDirection(moveDir);

        SetAnimatorFloat(_hashXAxis, localDir.x);
        SetAnimatorFloat(_hashYAxis, localDir.z);
    }
    
    public void RotateTowardsDirection(Vector3 dir)
    {  

        if (!canRotate) return; // Respect rotation lock from combat system
        if (dir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime));
    }

    private void UpdateAnimatorBooleans(bool isMoving)
    {
        if (_lastIsRunning != isMoving)
        {
            _animator.SetBool(_hashIsRunning, isMoving);
            _lastIsRunning = isMoving;
        }
    }

    private void SetAnimatorFloat(int hash, float value)
    {
        // Only update if value changed significantly (prevents micro-updates)
        float current = hash == _hashXAxis ? _lastXAxis : _lastYAxis;
        if (Mathf.Abs(current - value) > 0.001f)
        {
            _animator.SetFloat(hash, value);
            if (hash == _hashXAxis)
                _lastXAxis = value;
            else
                _lastYAxis = value;
        }
    }

    private void StopVelocity()
    {
        _rb.linearVelocity = Vector3.zero;
        SetAnimatorFloat(_hashXAxis, 0f);
        SetAnimatorFloat(_hashYAxis, 0f);
    }


    private void Update()
    {
        // If currently executing a combat move, use attackSpeed
        // If executing an acrobatic move, use acrobaticSpeed
        // Otherwise use movementSpeed for locomotion animations
        bool isAttacking = _combatHandler != null && _combatHandler.IsAttacking;
        bool isAcrobatic = _combatHandler != null && _combatHandler.IsAcrobatic;
        
        if (isAcrobatic)
            _animator.speed = acrobaticSpeed;
        else if (isAttacking)
            _animator.speed = attackSpeed;
        else
            _animator.speed = movementSpeed * movementAnimSpeedModifier;
    }
}