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
    private const float MAX_CLINCH_DURATION = 20f; // Auto-release after 20 seconds

    // Animator parameter hashes (cached for better performance than string lookups)
    private static readonly int HashIsClinching = Animator.StringToHash("b_IsClinching");
    private static readonly int HashClinchStateStarted = Animator.StringToHash("t_ClinchStateStarted");
    private static readonly int HashIsBeingGrabbed = Animator.StringToHash("b_IsBeingGrabbed");
    private static readonly int HashInputX = Animator.StringToHash("Input_XFloat");
    private static readonly int HashInputY = Animator.StringToHash("Input_YFloat");

    /// <summary>
    /// Returns true if this character is currently in a clinch state.
    /// </summary>
    public bool IsClinching => _isClinching;

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
            EndClinch();
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
        if (!_isClinching || _combat.currentStyle.clinchThrow == null) return;

        // Execute throw animation
        _combat.ExecuteCustomMove(_combat.currentStyle.clinchThrow);
        
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

        // Re-enable player rotation control
        _movement.canRotate = true;

        // Return player to idle animation
        _animator.Play("Idle");
    }




    #region Throws 
    public void ExecuteWheelThrow()
    {
        if (!_isClinching) return;

               // 1. Trigger the throw on the Ninja
        _animator.SetTrigger("t_WheelThrow");

        // 2. Trigger the throw on the Enemy
        if (_grabbedEnemy != null && _grabbedEnemy.TryGetComponent<Animator>(out var enemyAnim))
        {
            enemyAnim.SetTrigger("t_WheelThrow");
        }

        // 3. Switch to Root Motion for the throw's arc
        _animator.applyRootMotion = true;

        // We don't call EndClinch() here yet! 
        // We wait for the Animation Event 'OnThrowRelease' to unparent them.
    }
    #endregion
}