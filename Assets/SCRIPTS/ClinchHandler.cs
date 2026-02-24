using System.Collections;
using UnityEngine;

/// <summary>
/// Handles clinch/grab mechanics for Muay Thai style combat.
/// Manages synchronization of player and enemy animations, positions, and physics during clinch state.
/// 
/// Key Features:
/// - Synchronizes animations and movement between clincher and grabbed entity
/// - Supports clinch attacks (knee strikes) and throws (wheel throw)
/// - Implements break clinch animations with proper state cleanup
/// - Final Fight-style throw physics with arc trajectories
/// </summary>
public class ClinchHandler : MonoBehaviour, IAnimationStateListener
{
    #region Component References
    private CombatHandler _combat;
    private Animator _animator;
    private HealthComponent _health;
    private MovementComponent _movement;
    private Collider _playerCollider;
    private Rigidbody _rigidbody; // Cached for performance
    #endregion

    #region Clinch Configuration
    [Header("Clinch State")]
    [SerializeField] private float _clinchDistance = 0.65f;
    
    [Header("Throw Physics")]
    [SerializeField] private float _throwArcHeight = 2f;
    [SerializeField] private float _throwDistance = 7f;
    [SerializeField] private float _throwRotationSpeed = 2f;
    #endregion

    #region Enemy State (Cached During Clinch)
    private Transform _grabbedEnemy;
    private Animator _enemyAnimator;
    private Rigidbody _enemyRigidbody;
    private Collider _enemyCollider;
    private float _enemyOriginalAnimSpeed;
    #endregion

    #region Clinch State Tracking
    private bool _isClinching;
    private bool _isBreakingClinch;
    private float _clinchTimer;
    private const float MAX_CLINCH_DURATION = 3f;
    #endregion

    #region Throw State Tracking
    private float _lastThrownTime = -999f;
    private bool _isBeingThrown;
    private bool _throwFinished;
    private bool _isExecutingThrow; // NEW: Tracks if actively performing a throw
    #endregion

    #region Cached Layer Masks
    private static int _floorLayerMask = -1;

    private static int FloorLayerMask
    {
        get
        {
            if (_floorLayerMask == -1)
                _floorLayerMask = LayerMask.GetMask("Floor");
            return _floorLayerMask;
        }
    }
    #endregion

    #region Animator Parameter Hashes
    // Cached hashes for better performance than string lookups
    private static readonly int HashHasGrabbedEnemy = Animator.StringToHash("b_HasGrabbedEnemy");
    private static readonly int HashClinchStateStarted = Animator.StringToHash("t_ClinchStateStarted");
    private static readonly int HashIsBeingGrabbed = Animator.StringToHash("b_IsBeingGrabbed");
    private static readonly int HashInputX = Animator.StringToHash("Input_XFloat");
    private static readonly int HashInputY = Animator.StringToHash("Input_YFloat");
    private static readonly int HashWheelThrow = Animator.StringToHash("t_WheelThrow");
    private static readonly int HashBreakClinch = Animator.StringToHash("t_BreakClinch");
    private static readonly int HashIsRunning = Animator.StringToHash("isRunningBool");
    private static readonly int HashInClinch = Animator.StringToHash("b_InClinch");
    
    // Animation state hashes (requires Animation State Exit Notifier on clips)
    private static readonly int HashThrowTori = Animator.StringToHash("ReplaceableThrow-Attacker");
    private static readonly int HashClinchBreakTori = Animator.StringToHash("clinch-break-tori");
    #endregion

    #region Public Properties
    public bool IsClinching => _isClinching;
    public bool IsBreakingClinch => _isBreakingClinch;
    public bool IsExecutingThrow => _isExecutingThrow; // NEW: Expose throw state
    public bool CanBeClinched => Time.time - _lastThrownTime >= _combat.ClinchRecovery && !_isBeingThrown;
    #endregion

    #region Unity Lifecycle

    public void Initialize(CombatHandler combat)
    {
        _combat = combat;
        _animator = GetComponent<Animator>();
        _health= GetComponent<HealthComponent>();   
        _movement = GetComponent<MovementComponent>();
        _playerCollider = GetComponent<Collider>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (!_isClinching || _isBreakingClinch) return;

        _clinchTimer += Time.deltaTime;
        if (_clinchTimer >= MAX_CLINCH_DURATION)
        {
            BreakClinch();
            return;
        }

        UpdateClinchMovement();
    }
    #endregion

    #region Clinch Initiation

    public void AttemptClinch(Transform target)
    {
        // Block if already in any clinch-related state (clinching, breaking, or throwing)
        if (_isClinching || _isBreakingClinch || _isExecutingThrow || _combat.IsAttacking)
        {
            Debug.Log($"[ClinchHandler] Cannot start new clinch - already in clinch state: IsClinching={_isClinching}, IsBreaking={_isBreakingClinch}, IsExecutingThrow={_isExecutingThrow}, IsAttacking={_combat.IsAttacking}");
            return;
        }

        // Check if target is in recovery from being thrown
        if (target.TryGetComponent<ClinchHandler>(out var targetClinch) && !targetClinch.CanBeClinched)
        {
            Debug.Log($"{target.name} is still recovering from being thrown!");
            return;
        }

        StartCoroutine(ClinchSequence(target));
    }

    /// <summary>
    /// Coroutine that handles the clinch initialization sequence:
    /// 1. Caches enemy components for performance
    /// 2. Synchronizes animator speeds between player and enemy
    /// 3. Sets animator parameters for both characters
    /// 4. Makes enemy physics kinematic and disables collisions
    /// 5. Smoothly aligns both characters to face each other
    /// 6. Parents enemy to player for synchronized movement
    /// </summary>
    private IEnumerator ClinchSequence(Transform target)
    {

        

        _isClinching = true;
        _grabbedEnemy = target;
        _clinchTimer = 0f;

        // Cache enemy components (reduces repeated GetComponent calls)
        _enemyAnimator = target.GetComponent<Animator>();
        _enemyRigidbody = target.GetComponent<Rigidbody>();
        _enemyCollider = target.GetComponent<Collider>();

       // _enemyAnimator.Play("Idle");
       
        // Store and synchronize animator speeds so animations play at the same rate
        if (_enemyAnimator != null)
        {
            _enemyOriginalAnimSpeed = _enemyAnimator.speed;
            _enemyAnimator.speed = _animator.speed;
        }

       
        // Disable player rotation during clinch (controlled by alignment system)
        _movement.canRotate = false;

        // Set unified clinch state bool for PLAYER only
        _animator.SetBool(HashInClinch, true);

        // Trigger clinch animations for player
        _animator.SetBool(HashHasGrabbedEnemy, true);
        _animator.SetTrigger(HashClinchStateStarted);
        ResetMovementParams(_animator);
        
        // Reset enemy movement params but DON'T trigger animation yet
        if (_enemyAnimator != null)
            ResetMovementParams(_enemyAnimator);


        // Make enemy kinematic FIRST to stop all physics movement
        if (_enemyRigidbody != null)
        {
            _enemyRigidbody.linearVelocity = Vector3.zero;
            _enemyRigidbody.angularVelocity = Vector3.zero;
            _enemyRigidbody.isKinematic = true;
            _enemyRigidbody.interpolation = RigidbodyInterpolation.None;
        }

       

        // Disable enemy root motion to prevent animation from moving them
        if (_enemyAnimator != null)
        {
            _enemyAnimator.applyRootMotion = false;
        }

        // Disable collision between player and enemy to prevent physics glitches
        if (_playerCollider != null && _enemyCollider != null)
        {
            Physics.IgnoreCollision(_playerCollider, _enemyCollider, true);
        }

        // Calculate alignment positions and rotations AFTER stopping enemy movement
        transform.GetPositionAndRotation(out Vector3 startPos, out Quaternion startRot);
        
        // Direction vector from player to enemy
        Vector3 toEnemy = (target.position - transform.position);
        toEnemy.y = 0; // Keep on horizontal plane
        toEnemy.Normalize();
     
        // Player should be at clinch distance from enemy, along the connection line
        Vector3 targetPos = target.position - (toEnemy * _clinchDistance);
        
        // Both characters should face each other (not based on enemy's original forward)
        Quaternion targetRot = Quaternion.LookRotation(toEnemy); // Player faces enemy
        Quaternion enemyTargetRot = Quaternion.LookRotation(-toEnemy); // Enemy faces player
        
        Quaternion enemyStartRot = target.rotation;

        // Smoothly align both characters over 0.15 seconds
        float elapsed = 0;
        float duration = 0.15f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            // Interpolate player position and rotation to target
            transform.SetPositionAndRotation(
                Vector3.Lerp(startPos, targetPos, t),
                Quaternion.Slerp(startRot, targetRot, t)
            );

            // Make enemy face the player dynamically (using pre-calculated target rotation)
            target.rotation = Quaternion.Slerp(enemyStartRot, enemyTargetRot, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure final positions are exact (prevents floating point drift)
        transform.SetPositionAndRotation(targetPos, targetRot);
        target.rotation = enemyTargetRot;

        // NOW trigger clinch animations for enemy after alignment is complete
        if (_enemyAnimator != null)
        {
            _enemyAnimator.SetBool(HashInClinch, true); // Set AFTER alignment
            _enemyAnimator.SetBool(HashIsBeingGrabbed, true);
            _enemyAnimator.SetTrigger(HashClinchStateStarted);
            
            // Force immediate animator evaluation to prevent visual pop
            _enemyAnimator.Update(0f);
        }

        // Parent enemy to player so they move together as one unit
        _grabbedEnemy.SetParent(transform);

        // Zero out physics velocities to prevent unwanted movement
        if (_enemyRigidbody != null)
        {
            _enemyRigidbody.linearVelocity = Vector3.zero;
            _enemyRigidbody.angularVelocity = Vector3.zero;
        }

        Debug.Log("Clinch Synced! (Kurinchi dōki - クリンチ同期)");
    }
    #endregion

    #region Clinch Movement

    private void UpdateClinchMovement()
    {
        Vector3 localDir = transform.InverseTransformDirection(_movement.currentMoveDir);

        // Update animation parameters for visual blending
        _animator.SetFloat(HashInputX, localDir.x);
        _animator.SetFloat(HashInputY, localDir.z);
        
        if (_enemyAnimator != null)
        {
            _enemyAnimator.speed = _animator.speed;
            _enemyAnimator.SetFloat(HashInputX, localDir.x);
            _enemyAnimator.SetFloat(HashInputY, localDir.z);
        }

        // Movement is handled by MovementComponent - this is just for visual synchronization
        // MovementComponent already applies reduced speed (30%) during clinch state
        
        // Ensure enemy stays at exact clinch distance and rotation
        if (_grabbedEnemy != null)
        {
            // Maintain exact local position relative to player
            Vector3 localEnemyPos = _grabbedEnemy.localPosition;
            localEnemyPos.z = _clinchDistance; // Enemy should always be at clinch distance forward
            localEnemyPos.x = 0f; // Enemy should be centered
            localEnemyPos.y = 0f; // Same height as player
            _grabbedEnemy.localPosition = localEnemyPos;
            
            // Maintain exact facing direction (enemy faces opposite to player)
            _grabbedEnemy.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
    }
    #endregion

    #region Clinch Attacks

    public void ExecuteClinchLight()
    {
        if (!_isClinching || _combat.currentStyle.clinchKnee == null) return;

        _clinchTimer = Mathf.Max(0, _clinchTimer - 0.5f);
        _combat.ExecuteCustomMove(_combat.currentStyle.clinchKnee);
    }
    #endregion

    #region Break Clinch


    public void BreakClinch()
    {
        if (!_isClinching) return;

        _isBreakingClinch = true;

        // Ensure both characters are properly positioned and facing each other before break
        if (_grabbedEnemy != null)
        {
            // Recalculate proper positioning (same logic as clinch initiation)
            Vector3 toEnemy = (_grabbedEnemy.position - transform.position);
            toEnemy.y = 0;
            toEnemy.Normalize();
            
            // Snap player to correct distance and rotation
            Vector3 correctPlayerPos = _grabbedEnemy.position - (toEnemy * _clinchDistance);
            transform.SetPositionAndRotation(
                correctPlayerPos,
                Quaternion.LookRotation(toEnemy)
            );
            
            // Snap enemy to face player
            _grabbedEnemy.rotation = Quaternion.LookRotation(-toEnemy);
            
            // Zero out any accumulated velocities before unparenting
            if (_enemyRigidbody != null)
            {
                _enemyRigidbody.linearVelocity = Vector3.zero;
                _enemyRigidbody.angularVelocity = Vector3.zero;
            }
            
            // Unparent enemy AFTER position correction
            _grabbedEnemy.SetParent(null);
            
            // Re-enable enemy physics
            if (_enemyRigidbody != null)
            {
                _enemyRigidbody.isKinematic = false;
                _enemyRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        ResetMovementParams(_animator);
        if (_enemyAnimator != null)
        {
            ResetMovementParams(_enemyAnimator);
            _enemyAnimator.ResetTrigger(HashWheelThrow);
            _enemyAnimator.SetTrigger(HashBreakClinch);
            _enemyAnimator.SetBool(HashInClinch, false);
            _enemyAnimator.applyRootMotion = true;
        }
        
        _animator.SetBool(HashInClinch, false);

        _animator.ResetTrigger(HashWheelThrow);
        _animator.SetTrigger(HashBreakClinch);
        _animator.applyRootMotion = true;
        // Animation completion handled by OnAnimationStateExit callback
    }


    public void EndClinch()
    {
        if (!_isClinching) return;

        if (_grabbedEnemy != null)
        {
            if (_enemyAnimator != null)
            {
                _enemyAnimator.SetBool(HashIsBeingGrabbed, false);
                _enemyAnimator.SetBool(HashInClinch, false);
                _enemyAnimator.speed = _enemyOriginalAnimSpeed;
            }

            if (_playerCollider != null && _enemyCollider != null)
                Physics.IgnoreCollision(_playerCollider, _enemyCollider, false);

            if (_enemyRigidbody != null)
            {
                _enemyRigidbody.isKinematic = false;
                _enemyRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            }

            // Unparent enemy (may already be null if unparented in BreakClinch)
            if (_grabbedEnemy.parent == transform)
                _grabbedEnemy.SetParent(null);
            
            _grabbedEnemy = null;
        }

        _enemyAnimator.applyRootMotion = false;
        _animator.applyRootMotion = false;

        _enemyAnimator = null;
        _enemyRigidbody = null;
        _enemyCollider = null;

        _isClinching = false;
        _animator.SetBool(HashHasGrabbedEnemy, false);
        _animator.SetBool(HashInClinch, false);
        _animator.ResetTrigger(HashWheelThrow);
        ResetMovementParams(_animator);

        _movement.canRotate = true;

        if (_rigidbody != null)
            _rigidbody.linearVelocity = Vector3.zero;

        CameraManager.OnActivateTargetGroupCams?.Invoke(false, null);
      //  _animator.Play("Idle");
        if (_enemyAnimator != null)
            _enemyAnimator.applyRootMotion = false;
    }
    #endregion




    #region Throws 
    /// <summary>
    /// Executes the wheel throw (Sode-tsurikomi-goshi) - Final Fight style.
    /// Implements multi-phase throw sequence:
    /// Phase 0: Spike Out rotation to face throw direction
    /// Phase 1: Unparent and enable root motion on both
    /// Phase 2: Wait for thrower animation to complete
    /// Phase 3: Launch enemy in arc (with temporary collision disable)
    /// Phase 4: Return thrower to normal state
    /// Phase 5: Wait for enemy to land and restore state
    /// </summary>
    /// <param name="throwDirection">World-space direction to throw towards. If zero, uses current forward direction.</param>
    public void ExecuteWheelThrow(Vector3 throwDirection)
    {
        Debug.Log($"[ClinchHandler] ExecuteWheelThrow called - IsClinching: {_isClinching}, IsBreaking: {_isBreakingClinch}, IsExecutingThrow: {_isExecutingThrow}, HasEnemy: {_grabbedEnemy != null}");
        
        if (!_isClinching || _isBreakingClinch || _isExecutingThrow)
        {
            Debug.LogWarning($"[ClinchHandler] Cannot execute throw - IsClinching: {_isClinching}, IsBreaking: {_isBreakingClinch}, IsExecutingThrow: {_isExecutingThrow}");
            return;
        }

        // Set throw flag BEFORE clearing clinch flag to prevent heavy attack from executing
        _isExecutingThrow = true;
        _isClinching = false;
        _animator.SetBool(HashHasGrabbedEnemy, false);
        
        // Clear unified clinch bool for both characters
        _animator.SetBool(HashInClinch, false);
      

        Debug.Log("[ClinchHandler] Starting throw sequence...");
        StartCoroutine(ExecuteThrowSequence(throwDirection));
    }

    /// <summary>
    /// Final Fight-style throw sequence with arc trajectory and recovery phases.
    /// Implements Spike Out-style rotation where both characters spin around their center point to face throw direction.
    /// </summary>
    private IEnumerator ExecuteThrowSequence(Vector3 throwDirection)
    {
        if (_grabbedEnemy == null) yield break;

        // Mark enemy as being thrown
        if (_grabbedEnemy.TryGetComponent<ClinchHandler>(out var enemyClinch))
        {
            enemyClinch._isBeingThrown = true;
        }
        _throwFinished = false;
        // === PHASE 0: Spike Out-style rotation around center point ===
        if (throwDirection.sqrMagnitude > 0.01f)
        {
            throwDirection.y = 0; // Ensure horizontal direction only
            throwDirection.Normalize();

            // Calculate center point between player and enemy
            Vector3 centerPoint = Vector3.Lerp(transform.position, _grabbedEnemy.position, 0.5f);

            // Store starting rotations and positions
            Quaternion playerStartRot = transform.rotation;
            Quaternion enemyStartRot = _grabbedEnemy.rotation;
            Vector3 playerStartPos = transform.position;
            Vector3 enemyStartPos = _grabbedEnemy.position;

            // Calculate target rotations
            Quaternion playerTargetRot = Quaternion.LookRotation(throwDirection);
            Quaternion enemyTargetRot = Quaternion.LookRotation(-throwDirection);

            // Pre-calculate rotation deltas (avoids repeated quaternion inversions)
            Quaternion playerRotDelta = playerTargetRot * Quaternion.Inverse(playerStartRot);
            Quaternion enemyRotDelta = enemyTargetRot * Quaternion.Inverse(enemyStartRot);

            // Rotate both characters around center point over a short duration
            float rotationDuration = 0.2f; // Quick spin to face throw direction
            float elapsed = 0f;

            while (elapsed < rotationDuration)
            {
                float t = elapsed / rotationDuration;

                // Interpolate rotations
                Quaternion playerRot = Quaternion.Slerp(playerStartRot, playerTargetRot, t);
                Quaternion enemyRot = Quaternion.Slerp(enemyStartRot, enemyTargetRot, t);

                // Calculate offset from center
                Vector3 playerOffset = playerStartPos - centerPoint;
                Vector3 enemyOffset = enemyStartPos - centerPoint;

                // Rotate offsets around center point using pre-calculated deltas
                Quaternion playerOffsetRot = Quaternion.Slerp(Quaternion.identity, playerRotDelta, t);
                Quaternion enemyOffsetRot = Quaternion.Slerp(Quaternion.identity, enemyRotDelta, t);

                Vector3 rotatedPlayerOffset = playerOffsetRot * playerOffset;
                Vector3 rotatedEnemyOffset = enemyOffsetRot * enemyOffset;

                // Apply new positions and rotations
                transform.SetPositionAndRotation(centerPoint + rotatedPlayerOffset, playerRot);
                _grabbedEnemy.SetPositionAndRotation(centerPoint + rotatedEnemyOffset, enemyRot);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Ensure final rotations are exact
            transform.rotation = playerTargetRot;
            _grabbedEnemy.rotation = enemyTargetRot;

            Debug.Log("Throw Phase 0: Spike Out-style rotation complete");
        }

        // === PHASE 1: Unparent and enable root motion ===

        // Activate target group cameras with both characters
        //CameraManager.OnActivateTargetGroupCams?.Invoke(true, new Transform[] { transform, _grabbedEnemy });

        // Unparent enemy so they can move independently
        _grabbedEnemy.SetParent(null);

        // Re-enable physics on enemy (prepare for launch)
        if (_enemyRigidbody != null)
        {
            _enemyRigidbody.isKinematic = false;
            _enemyRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        // Release enemy from grabbed state immediately (allows parallel throw animations)
        if (_enemyAnimator != null)
        {
            _enemyAnimator.SetBool(HashIsBeingGrabbed, false);
        }

        // Enable root motion on both characters
        
        _animator.applyRootMotion = true;
        if (_enemyAnimator != null)
        {
            _enemyAnimator.applyRootMotion = true;
        }

        // Trigger throw animations (now run in parallel)
        _animator.SetTrigger(HashWheelThrow);
        if (_enemyAnimator != null)
        {
            _enemyAnimator.SetTrigger(HashWheelThrow);
        }

        Debug.Log("Throw Phase 1: Root motion enabled on both characters");
        JSAM.AudioManager.PlaySound(_health.characterEffects.sfxThrowVocal);

        // === PHASE 2: Wait for throw animation to complete on thrower ===
        yield return new WaitUntil(() => _throwFinished);

        // Disable root motion on thrower
        //_animator.applyRootMotion = false;

        Debug.Log("Throw Phase 2: Thrower animation complete");

        // === PHASE 3: Launch enemy in arc ===

        
        


        // Disable root motion on enemy and freeze animation
        if (_enemyAnimator != null)
        {
        //    _enemyAnimator.applyRootMotion = false;
            _enemyAnimator.speed = 0f;
        }

        // Temporarily disable floor collision for enemy during launch
        int enemyOriginalLayer = 0;
        if (_grabbedEnemy != null)
        {
            enemyOriginalLayer = _grabbedEnemy.gameObject.layer;
            _grabbedEnemy.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        }

        // Launch enemy in arc trajectory (opposite to their forward direction)
        if (_enemyRigidbody != null && _grabbedEnemy != null)
        {
            // Ensure rigidbody is non-kinematic before applying forces
            if (_enemyRigidbody.isKinematic)
            {
                Debug.LogWarning("Enemy rigidbody was still kinematic at launch! Forcing non-kinematic.");
                _enemyRigidbody.isKinematic = false;
            }

            Vector3 throwDir = -_grabbedEnemy.forward; // Opposite to enemy's facing direction
            throwDir.y = 0;
            throwDir.Normalize();

            // Calculate arc trajectory velocity
            Vector3 horizontalVelocity = throwDir * _throwDistance;

            //float gravity = Mathf.Abs(Physics.gravity.y); // Use default gravity
            float gravity = Mathf.Abs(Physics.gravity.y) * 1.2f; // Increased gravity by 20%
            float timeToApex = Mathf.Sqrt(2 * _throwArcHeight / gravity);
            float verticalVelocity = gravity * timeToApex;

            // Apply combined velocity
            Vector3 launchVelocity = horizontalVelocity + Vector3.up * verticalVelocity;
            _enemyRigidbody.linearVelocity = launchVelocity;

            _enemyRigidbody.angularVelocity = _grabbedEnemy.right * _throwRotationSpeed;

            Debug.Log($"Launch velocity applied: {launchVelocity}, RB velocity: {_enemyRigidbody.linearVelocity}");
        }
        else
        {
            Debug.LogError("Cannot launch enemy - rigidbody or transform is null!");
        }

        // Wait briefly before re-enabling collision (prevent immediate ground collision)
        yield return new WaitForSeconds(0.3f);

        // Restore enemy layer to enable floor collision
        if (_grabbedEnemy != null)
        {
            _grabbedEnemy.gameObject.layer = enemyOriginalLayer;
        }

        Debug.Log("Throw Phase 3: Enemy launched in arc trajectory");

        // === PHASE 4: Return thrower to normal ===

        // Return player to normal state
        _animator.ResetTrigger(HashWheelThrow); // Clear the trigger
        _movement.canRotate = true;

        ResetMovementParams(_animator);

        if (_rigidbody != null)
            _rigidbody.linearVelocity = Vector3.zero;

        // Clear player's cached enemy references
        Transform thrownEnemy = _grabbedEnemy;
        Animator thrownAnimator = _enemyAnimator;
        Rigidbody thrownRb = _enemyRigidbody;
        Collider thrownCollider = _enemyCollider;

        _grabbedEnemy = null;
        _enemyAnimator = null;
        _enemyRigidbody = null;
        _enemyCollider = null;

        Debug.Log("Throw Phase 4: Thrower returned to normal state");

        // === PHASE 5: Wait for enemy to hit the floor ===
        if (thrownEnemy != null && thrownRb != null)
        {
            // Wait until enemy hits the floor layer
            bool hasHitFloor = false;
            float maxWaitTime = 5f; // Failsafe timeout
            float waitElapsed = 0f;

            while (!hasHitFloor && waitElapsed < maxWaitTime)
            {
                // Check if enemy is grounded by raycasting down
                if (Physics.Raycast(thrownEnemy.position, Vector3.down, 0.2f, FloorLayerMask))
                {
                    hasHitFloor = true;
                    Debug.Log($"{thrownEnemy.name} hit the floor!");
                    
                    // Play landing sound effect
                    if (thrownEnemy.TryGetComponent<HealthComponent>(out var thrownEnemyHealth))
                    {
                        if (thrownEnemyHealth.characterEffects != null && thrownEnemyHealth.characterEffects.sfxLandAfterThrown != null)
                        {
                            JSAM.AudioManager.PlaySound(thrownEnemyHealth.characterEffects.sfxLandAfterThrown);
                        }
                    }
                }

                waitElapsed += Time.deltaTime;
                yield return null;
            }

            if (!hasHitFloor)
            {
                Debug.LogWarning($"{thrownEnemy.name} did not hit floor within timeout period");
            }
        }

        if (thrownEnemy != null)
        {
            // Resume enemy animation
            if (thrownAnimator != null)
            {
                thrownAnimator.speed = _enemyOriginalAnimSpeed;
                // Note: HashIsBeingGrabbed already set to false in Phase 1
                // Play getup/recovery animation if available
                // thrownAnimator.Play("GetUp"); // Uncomment if you have this animation
            }

            // Stop physics movement
            if (thrownRb != null)
            {
                thrownRb.linearVelocity = Vector3.zero;
                thrownRb.angularVelocity = Vector3.zero;
            }

            // Re-enable collision with player
            if (_playerCollider != null && thrownCollider != null)
            {
                Physics.IgnoreCollision(_playerCollider, thrownCollider, false);
            }

            // Start clinch recovery cooldown on thrown enemy
            if (enemyClinch != null)
            {
                enemyClinch._lastThrownTime = Time.time;
                enemyClinch._isBeingThrown = false;
                Debug.Log($"{thrownEnemy.name} entering clinch recovery for {_combat.ClinchRecovery} seconds");
            }
        }

        Debug.Log("Throw Phase 5: Thrown enemy returned to normal state with recovery cooldown");

        // Deactivate target group cameras
       // CameraManager.OnActivateTargetGroupCams?.Invoke(false, null);


        _animator.applyRootMotion = false;
        if (_enemyAnimator != null)
        {
            _enemyAnimator.applyRootMotion = false;
        }
        
        // Clear throw flag - allow normal attacks again
        _isExecutingThrow = false;
        Debug.Log("[ClinchHandler] Throw sequence complete - _isExecutingThrow cleared");
    }

    /*
    void OnAnimatorMove()
    {
        if (_animator.applyRootMotion)
        {
            // Apply the animator's movement to the transform manually
            transform.position += _animator.deltaPosition;
            transform.rotation *= _animator.deltaRotation;
        }
    }*/
    private void ResetMovementParams(Animator anim)
    {
        if (anim == null) return;
        
        anim.SetFloat(HashInputX, 0f);
        anim.SetFloat(HashInputY, 0f);
        anim.SetBool(HashIsRunning, false);
    }
    #endregion

    #region Animation Callbacks
    public void OnAnimationStateExit(int stateHash, int layerIndex)
    {
        if (stateHash == HashThrowTori)
            HandleThrowExit();
        else if (stateHash == HashClinchBreakTori)
            HandleBreakToriExit();
    }

    private void HandleThrowExit()
    {
        _throwFinished = true;
    }

    private void HandleBreakToriExit()
    {
        if (_enemyAnimator != null)
            _enemyAnimator.SetBool(HashIsBeingGrabbed, false);

        _lastThrownTime = Time.time;
        if (_grabbedEnemy != null && _grabbedEnemy.TryGetComponent<ClinchHandler>(out var enemyClinch))
            enemyClinch._lastThrownTime = Time.time;

        EndClinch();
        _isBreakingClinch = false;
    }
    #endregion
}