using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Handles clinch/grab mechanics for Muay Thai style combat.
/// Manages synchronization of player and enemy animations, positions, and physics during clinch state.
/// </summary>
public class ClinchHandler : MonoBehaviour
{
    // Component references
    private CombatHandler _combat;
    private Animator _animator;
    private MovementComponent _movement;
    private Collider _playerCollider;

    [Header("Clinch State")]
    [SerializeField] private float _clinchDistance = 0.65f; // Distance between player and enemy during clinch
    
    // Enemy references (cached during clinch)
    private Transform _grabbedEnemy;
    private Animator _enemyAnimator;
    private Rigidbody _enemyRigidbody;
    private Collider _enemyCollider;
    private MovementComponent _enemyMovement;
    private float _enemyOriginalAnimSpeed; // Stored to restore after clinch ends
    
    // Clinch state tracking
    private bool _isClinching;
    private float _clinchTimer;
    private const float MAX_CLINCH_DURATION = 3f; // Auto-release after 3 seconds
    
    // Throw recovery tracking
    private float _lastThrownTime = -999f; // Time when this entity was last thrown
    private bool _isBeingThrown; // True during throw sequence
    
    // Throw physics settings
    private const float THROW_ARC_HEIGHT = 1.5f; // Height of the arc trajectory
    private const float THROW_DISTANCE = 4f; // Horizontal distance enemy is thrown

    // Cached layer masks (avoid string-based lookups)
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

    // Animator parameter hashes (cached for better performance than string lookups)
    private static readonly int HashIsClinching = Animator.StringToHash("b_IsClinching");
    private static readonly int HashClinchStateStarted = Animator.StringToHash("t_ClinchStateStarted");
    private static readonly int HashIsBeingGrabbed = Animator.StringToHash("b_IsBeingGrabbed");
    private static readonly int HashInputX = Animator.StringToHash("Input_XFloat");
    private static readonly int HashInputY = Animator.StringToHash("Input_YFloat");
    private static readonly int HashWheelThrow = Animator.StringToHash("t_WheelThrow");
    private static readonly int HashBreakClinch = Animator.StringToHash("t_BreakClinch");

    /// <summary>
    /// Returns true if this character is currently in a clinch state.
    /// </summary>
    public bool IsClinching => _isClinching;
    
    /// <summary>
    /// Returns true if this character can be clinched (not recently thrown).
    /// </summary>
    public bool CanBeClinched => Time.time - _lastThrownTime >= _combat.ClinchRecovery && !_isBeingThrown;

    /// <summary>
    /// Initializes the clinch handler with required component references.
    /// Called by CombatHandler during setup.
    /// </summary>
    public void Initialize(CombatHandler combat)
    {
        _combat = combat;
        _animator = GetComponent<Animator>();
        _movement = GetComponent<MovementComponent>();
        _playerCollider = GetComponent<Collider>();
    }

    /// <summary>
    /// Updates the clinch state each frame.
    /// Tracks clinch duration and handles automatic release after max duration.
    /// Updates movement animations for synchronized player/enemy movement.
    /// </summary>
    private void Update()
    {
        if (!_isClinching) return;

        // Track clinch duration
        _clinchTimer += Time.deltaTime;
        if (_clinchTimer >= MAX_CLINCH_DURATION)
        {
            BreakClinch();
            return;
        }

        // Handle synchronized movement during clinch (strafing at reduced speed)
        UpdateClinchMovement();
    }

    /// <summary>
    /// Attempts to initiate a clinch with the target enemy.
    /// Validates that clinching is possible (not already clinching, not attacking).
    /// </summary>
    /// <param name="target">The enemy transform to clinch with</param>
    public void AttemptClinch(Transform target)
    {
        if (_isClinching || _combat.IsAttacking) return;
        
        // Check if target can be clinched (not in recovery from previous throw)
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

        // Cache all enemy components once for performance (avoid repeated GetComponent calls)
        _enemyAnimator = target.GetComponent<Animator>();
        _enemyRigidbody = target.GetComponent<Rigidbody>();
        _enemyCollider = target.GetComponent<Collider>();
        _enemyMovement = target.GetComponent<MovementComponent>();

        // Store and synchronize animator speeds so animations play at the same rate
        if (_enemyAnimator != null)
        {
            _enemyOriginalAnimSpeed = _enemyAnimator.speed;
            _enemyAnimator.speed = _animator.speed;
        }

        // Disable player rotation during clinch (controlled by alignment system)
        _movement.canRotate = false;

        // Trigger clinch animations for player
        _animator.SetBool(HashIsClinching, true);
        _animator.SetTrigger(HashClinchStateStarted);
        ResetMovementParams(); // Ensure movement parameters start at zero for proper blend tree behavior   

        // Calculate alignment positions and rotations
        // Player positions in front of enemy, both face each other
        transform.GetPositionAndRotation(out Vector3 startPos, out Quaternion startRot);
        Vector3 targetPos = target.position + (target.forward * _clinchDistance);
        Quaternion targetRot = Quaternion.LookRotation(-target.forward);
        
        Quaternion enemyStartRot = target.rotation;

        // Trigger clinch animations for enemy (being grabbed state)
        if (_enemyAnimator != null)
        {
            _enemyAnimator.SetBool(HashIsBeingGrabbed, true);
            _enemyAnimator.SetTrigger(HashClinchStateStarted);
        }

        // Make enemy kinematic to allow parenting and prevent physics conflicts
        if (_enemyRigidbody != null)
        {
            _enemyRigidbody.isKinematic = true;
            _enemyRigidbody.interpolation = RigidbodyInterpolation.None;
        }

        // Disable collision between player and enemy to prevent physics glitches
        if (_playerCollider != null && _enemyCollider != null)
        {
            Physics.IgnoreCollision(_playerCollider, _enemyCollider, true);
        }

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
            
            // Make enemy face the player dynamically (they should face each other)
            target.rotation = Quaternion.Slerp(enemyStartRot, Quaternion.LookRotation(-transform.forward), t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure final positions are exact (prevents floating point drift)
        transform.SetPositionAndRotation(targetPos, targetRot);
        target.rotation = Quaternion.LookRotation(-transform.forward);

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

    /// <summary>
    /// Updates movement animations for both player and enemy during clinch.
    /// Converts world movement direction to local space for strafing animations.
    /// Continuously syncs enemy animator speed with player to maintain animation synchronization.
    /// </summary>
    private void UpdateClinchMovement()
    {
        // Convert world movement direction to local space (relative to player facing)
        // localDir.x is side-to-side (strafe), localDir.z is forward-back
        Vector3 localDir = transform.InverseTransformDirection(_movement.currentMoveDir);

        // Update player animator blend tree parameters
        _animator.SetFloat(HashInputX, localDir.x);
        _animator.SetFloat(HashInputY, localDir.z);

        // Mirror movement to enemy animator so they move in sync
        if (_enemyAnimator != null)
        {
            // Keep enemy animator speed matched to player (handles dynamic speed changes)
            _enemyAnimator.speed = _animator.speed;
            
            // Set same blend tree parameters for synchronized leg movement
            _enemyAnimator.SetFloat(HashInputX, localDir.x);
            _enemyAnimator.SetFloat(HashInputY, localDir.z);
        }
    }

    /// <summary>
    /// Executes a light clinch attack (knee strike).
    /// Resets the clinch timer slightly to allow multiple attacks before auto-release.
    /// </summary>
    public void ExecuteClinchLight()
    {
        if (!_isClinching || _combat.currentStyle.clinchKnee == null) return;

        // Subtract time from timer to extend clinch duration (reward for landing attacks)
        _clinchTimer = Mathf.Max(0, _clinchTimer - 0.5f);
        _combat.ExecuteCustomMove(_combat.currentStyle.clinchKnee);
    }

    /// <summary>
    /// Executes a clinch throw move to end the clinch.
    /// Plays the throw animation and schedules clinch cleanup after a brief delay.
    /// </summary>
    public void ExecuteClinchThrow()
    {
        //Out of use - for now 

        if (!_isClinching || _combat.currentStyle.clinchThrowDefault == null) return;

        // Execute throw animation
        _combat.ExecuteCustomThrow(_combat.currentStyle.clinchThrowDefault);
        
        // Delay cleanup to allow throw animation to start playing
        StartCoroutine(EndClinchDelayed(0.1f));
    }

    /// <summary>
    /// Delays the end of the clinch by a specified duration.
    /// Used to allow throw animations to start before releasing the enemy.
    /// </summary>
    private IEnumerator EndClinchDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        EndClinch();
    }

    /// <summary>
    /// Waits for the specified animator to complete its current animation.
    /// </summary>
    /// <param name="animator">The animator to monitor</param>
    /// <param name="completionThreshold">Normalized time threshold (0-1) to consider animation complete</param>
    /// <param name="maxWaitTime">Maximum time to wait before timing out</param>
    /// <returns>True if animation completed, false if timed out</returns>
    private IEnumerator WaitForAnimationComplete(Animator animator, float completionThreshold = 0.95f, float maxWaitTime = 3f)
    {
        if (animator == null) yield break;
        
        float waitElapsed = 0f;
        bool animationFinished = false;
        
        while (!animationFinished && waitElapsed < maxWaitTime)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            
            // Check if animation is nearly complete and not transitioning
            if (stateInfo.normalizedTime >= completionThreshold && !animator.IsInTransition(0))
            {
                animationFinished = true;
                Debug.Log($"Animation completed at {stateInfo.normalizedTime * 100f}%");
            }
            
            waitElapsed += Time.deltaTime;
            yield return null;
        }
        
        if (!animationFinished)
        {
            Debug.LogWarning("Animation did not complete within timeout period");
        }
    }

    /// <summary>
    /// Breaks the clinch with an animation (used when timer expires).
    /// Triggers break animation and waits for it to complete before cleanup.
    /// </summary>
    public void BreakClinch()
    {
        if (!_isClinching) return;

        StartCoroutine(BreakClinchSequence());
    }

    /// <summary>
    /// Coroutine that handles the break clinch sequence:
    /// 1. Triggers break animation on both characters
    /// 2. Waits for break animation to complete
    /// 3. Cleans up clinch state and returns control
    /// </summary>
    private IEnumerator BreakClinchSequence()
    {
        // Reset throw trigger to prevent it from being activated
        _animator.ResetTrigger(HashWheelThrow);

        // Trigger break clinch animation on both characters
        _animator.SetTrigger(HashBreakClinch);
        if (_enemyAnimator != null)
        {
            _enemyAnimator.ResetTrigger(HashWheelThrow);
            _enemyAnimator.SetTrigger(HashBreakClinch);
        }

        Debug.Log("Break clinch animation started");

        // Wait for the break animation to complete
        yield return WaitForAnimationComplete(_animator);

        Debug.Log("Break clinch animation complete");

        // Start recovery cooldown for both characters
        _lastThrownTime = Time.time;
        if (_grabbedEnemy != null && _grabbedEnemy.TryGetComponent<ClinchHandler>(out var enemyClinch))
        {
            enemyClinch._lastThrownTime = Time.time;
        }

        // Clean up clinch state and return control
        EndClinch();
    }

    /// <summary>
    /// Ends the clinch state and restores both characters to normal gameplay.
    /// Cleanup process:
    /// 1. Restores enemy animator parameters and speed
    /// 2. Re-enables collisions between player and enemy
    /// 3. Restores enemy physics (non-kinematic rigidbody)
    /// 4. Unparents enemy from player
    /// 5. Clears all cached enemy component references
    /// 6. Resets player state and animations
    /// </summary>
    public void EndClinch()
    {
        if (!_isClinching) return;

        // Clean up enemy state before unparenting
        if (_grabbedEnemy != null)
        {
            // Restore enemy animator to normal state
            if (_enemyAnimator != null)
            {
                _enemyAnimator.SetBool(HashIsBeingGrabbed, false);
                // Restore original animator speed (may differ from player's speed)
                _enemyAnimator.speed = _enemyOriginalAnimSpeed;
            }

            // Re-enable collision between player and enemy
            if (_playerCollider != null && _enemyCollider != null)
            {
                Physics.IgnoreCollision(_playerCollider, _enemyCollider, false);
            }

            // Restore enemy's rigidbody to normal physics simulation
            if (_enemyRigidbody != null)
            {
                _enemyRigidbody.isKinematic = false;
                _enemyRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            }

            // Unparent enemy so they can move independently again
            _grabbedEnemy.SetParent(null);
            _grabbedEnemy = null;
        }

        // Clear all cached enemy component references
        _enemyAnimator = null;
        _enemyRigidbody = null;
        _enemyCollider = null;
        _enemyMovement = null;

        // Reset player state
        _isClinching = false;
        _animator.SetBool(HashIsClinching, false);
        _animator.ResetTrigger(HashWheelThrow); // Reset throw trigger to prevent auto-fire
        
        // Reset movement animator parameters to idle
        _animator.SetFloat(HashInputX, 0f);
        _animator.SetFloat(HashInputY, 0f);

        // Re-enable player rotation control
        _movement.canRotate = true;
        
        // Zero out rigidbody velocity to prevent residual movement
        if (GetComponent<Rigidbody>() != null)
        {
            GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        }

        // Deactivate target group cameras
        CameraManager.OnActivateTargetGroupCams?.Invoke(false, null);

        // Return player to idle animation
        _animator.Play("Idle");
    }




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
        if (!_isClinching) return;

        // End the clinch state immediately to prevent re-entry
        // The throw sequence will handle its own cleanup
        _isClinching = false;
        _animator.SetBool(HashIsClinching, false);

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
        CameraManager.OnActivateTargetGroupCams?.Invoke(true, new Transform[] { transform, _grabbedEnemy });
        
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
        
        // === PHASE 2: Wait for throw animation to complete on thrower ===
        yield return WaitForAnimationComplete(_animator);
        
        // Disable root motion on thrower
        _animator.applyRootMotion = false;
        
        Debug.Log("Throw Phase 2: Thrower animation complete");
        
        // === PHASE 3: Launch enemy in arc ===
        
        // Disable root motion on enemy and freeze animation
        if (_enemyAnimator != null)
        {
            _enemyAnimator.applyRootMotion = false;
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
            // Horizontal component
            Vector3 horizontalVelocity = throwDir * THROW_DISTANCE;
            
            // Vertical component (calculated to reach arc height)
            float gravity = Mathf.Abs(Physics.gravity.y);
            float timeToApex = Mathf.Sqrt(2 * THROW_ARC_HEIGHT / gravity);
            float verticalVelocity = gravity * timeToApex;
            
            // Apply combined velocity
            Vector3 launchVelocity = horizontalVelocity + Vector3.up * verticalVelocity;
            _enemyRigidbody.linearVelocity = launchVelocity;
            
            // Add slight rotation for visual flair
            _enemyRigidbody.angularVelocity = _grabbedEnemy.right * 2f;
            
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

        ResetMovementParams();

        // Zero out rigidbody velocity to prevent residual movement
        if (_combat.GetComponent<Rigidbody>() != null)
        {
            _combat.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        }
        
        // Clear player's cached enemy references
        Transform thrownEnemy = _grabbedEnemy;
        Animator thrownAnimator = _enemyAnimator;
        Rigidbody thrownRb = _enemyRigidbody;
        Collider thrownCollider = _enemyCollider;
        
        _grabbedEnemy = null;
        _enemyAnimator = null;
        _enemyRigidbody = null;
        _enemyCollider = null;
        _enemyMovement = null;
        
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
        CameraManager.OnActivateTargetGroupCams?.Invoke(false, null);
    }

    private void ResetMovementParams()
    {
        // Reset movement animator parameters to idle
        _animator.SetFloat(HashInputX, 0f);
        _animator.SetFloat(HashInputY, 0f);
        _animator.SetBool("isRunningBool", false);
    }
    #endregion
}