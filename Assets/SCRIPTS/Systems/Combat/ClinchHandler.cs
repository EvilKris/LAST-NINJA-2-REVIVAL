using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations;

public class ClinchHandler : MonoBehaviour, IAnimationStateListener
{
    #region Component References
    private CombatHandler _combat;
    private Animator _animator;
    private HealthComponent _health;
    private MovementComponent _movement;
    private Collider _playerCollider;
    private Rigidbody _rigidbody;
    #endregion

    #region Clinch Configuration
    [Header("Clinch State")]
    [SerializeField] private float _clinchDistance = 0.65f;
    [SerializeField] [Range(1f, 5f)] private float _throwLaunchSpeedMultiplier = 1f;
        
    #endregion

    #region Enemy State (Cached During Clinch)
    private Transform _grabbedEnemy;
    private Animator _enemyAnimator;
    private Rigidbody _enemyRigidbody;
    private Collider _enemyCollider;
    private MovementComponent _enemyMovement;
    private HealthComponent _enemyHealth;
    private CombatHandler _enemyCombat;
    private CombatActorBrain _enemyBrain;
    private float _enemyOriginalAnimSpeed;
    private float _playerOriginalAnimSpeed;
    private ParentConstraint _enemyParentConstraint;
    #endregion

    #region Clinch State Tracking
    private bool _isClinching;
    private bool _isBreakingClinch;
    private float _clinchTimer;
    private const float MAX_CLINCH_DURATION = 3f;
    private bool _isInClinchRecovery;
    private const float CLINCH_RECOVERY_DURATION = 3f;
    private bool _clinchRootMotionActive;
    #endregion

    #region Throw State Tracking

    private bool _isExecutingThrow;
    private bool _throwLaunchFired;
    private bool _enemyInFlight;
    private bool _enemyLanded;
    private float _colliderReenableTimer;
    private const float ColliderDisableDuration = 0.15f;
    private const float LandedGetUpDelay = 2f;
    private CombatThrow _activeThrowData;
    private Vector3 _throwDirection;
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
    private const string ThrowAttackerSlotKey = "ReplaceableThrow-Attacker"; //do not change the names of these clips in the Animator
    private const string ThrowVictimSlotKey = "ReplaceableThrow-Victim"; //do not change the names of these clips in the Animator
    private const string LightAtkAttackerSlotKey = "ReplaceableLightAtk-Attacker"; //do not change the names of these clips in the Animator
    private const string LightAtkDefenderSlotKey = "ReplaceableLightAtk-Defender"; //do not change the names of these clips in the Animator
    private const string BlockSlotKey = "ReplaceableBlock"; //do not change the names of these clips in the Animator



    private static readonly int HashInputX = Animator.StringToHash("Input_XFloat");
    private static readonly int HashInputY = Animator.StringToHash("Input_YFloat");
    private static readonly int HashRawInputX = Animator.StringToHash("Input_X");
    private static readonly int HashRawInputY = Animator.StringToHash("Input_Y");
    private static readonly int HashRawIsRunning = Animator.StringToHash("isRunning");
    private static readonly int HashWheelThrow = Animator.StringToHash("t_WheelThrow");
    private static readonly int HashBreakClinch = Animator.StringToHash("t_BreakClinch");
    private static readonly int HashIsRunning = Animator.StringToHash("isRunningBool");
    private readonly int HashInClinch = Animator.StringToHash("b_InClinch");
    private readonly int HashIsAction = Animator.StringToHash("isAction");

    private static readonly int HashIsGrounded = Animator.StringToHash("b_isGrounded");
    private static readonly int HashGettingUp = Animator.StringToHash("t_GettingUp");
    private static readonly int HashClinchLightAtk = Animator.StringToHash("t_ClinchLightAtk");
    #endregion

    #region Public Properties
    public bool IsClinching => _animator != null && _animator.GetBool(HashInClinch);
    public bool IsInClinchRecovery => _isInClinchRecovery;
    public bool CanBreakClinch => _isClinching && !_isExecutingThrow && !_isBreakingClinch;
    public Transform GrabbedEnemy => _grabbedEnemy;
    public bool IsClinchRootMotionActive => _clinchRootMotionActive;
    public bool IsExecutingThrow => _isExecutingThrow;
    #endregion

    public void Initialize(CombatHandler combat)
    {
        _combat = combat;
        _animator = GetComponent<Animator>();
        _health = GetComponent<HealthComponent>();
        _movement = GetComponent<MovementComponent>();
        _playerCollider = GetComponent<Collider>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    public void AttemptClinch(Transform target)
    {
        if (_isInClinchRecovery) return;
        if (_animator != null && _animator.GetBool(HashIsAction)) return;

        _enemyMovement = target.GetComponent<MovementComponent>();
        if (!_enemyMovement.CanBeClinched) return;

        
        HealthComponent targetHealth = target.GetComponent<HealthComponent>();
        if (targetHealth != null && targetHealth.IsDead) return;

        // Reset all triggers and state parameters on self before starting a fresh clinch
        _animator.ResetTrigger(HashWheelThrow);
        _animator.ResetTrigger(HashBreakClinch);
        _animator.ResetTrigger(HashGettingUp);       

        _grabbedEnemy = target;
        _enemyAnimator = target.GetComponent<Animator>();


        _animator.ResetTrigger("t_StartClinch");
        _enemyAnimator.ResetTrigger("t_StartClinch");
        _animator.SetInteger("ClinchRole", 0);
        _enemyAnimator.SetInteger("ClinchRole", 0);
        
        _enemyAnimator.ResetTrigger(HashWheelThrow);
        _enemyAnimator.ResetTrigger(HashBreakClinch);
        _enemyAnimator.ResetTrigger(HashGettingUp);

        
        _enemyMovement.isImmobilized = false;

        _enemyRigidbody = target.GetComponent<Rigidbody>();
        _enemyCollider = target.GetComponent<Collider>();
        _enemyHealth = targetHealth;
        _enemyCombat = target.GetComponent<CombatHandler>();
        _enemyBrain = target.GetComponent<CombatActorBrain>();

        if (_enemyHealth != null)
            _enemyHealth.OnDeath += AbortClinchOnEnemyDeath;

        // Immediately stop all physics movement on both actors
        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
        if (_enemyRigidbody != null)
        {
            _enemyRigidbody.linearVelocity = Vector3.zero;
            _enemyRigidbody.angularVelocity = Vector3.zero;
        }

        _animator.SetTrigger("t_StartClinch");
        _enemyAnimator.SetTrigger("t_StartClinch"); 

        _animator.SetBool(HashInClinch, true);
        _enemyAnimator.SetBool(HashInClinch, true);



        // Disable AI brain for the full duration of the clinch
        if (_enemyBrain != null)
            _enemyBrain.enabled = false;

        // Lock enemy movement (uke cannot move during clinch)
        // and sync their animations to match the attacker's movement
        if (_enemyMovement != null)
        {
            _enemyMovement.isMovementLocked = true;
            _enemyMovement.syncAnimationSource = _movement;
        }

        _clinchRootMotionActive = true;

        SetupEnemyParentConstraint(target);
        ClinchSequence(target);
    }

    public void ExecuteWheelThrow()
    {
        if (!_isClinching) return;
        if (_grabbedEnemy == null || _enemyAnimator == null) return;

        // Use clips pre-baked by CombatHandler from the FightingStyle
        AnimationClip attackerClip = _combat.ClinchThrowAttackerClip;
        AnimationClip victimClip = _combat.ClinchThrowVictimClip;
        if (attackerClip == null || victimClip == null) return;

        _activeThrowData = (_combat.currentStyle != null) ? _combat.currentStyle.clinchThrowDefault : null;
        if (_activeThrowData == null) return;

        // Write the attacker clip into the attacker's own override controller
        _combat.OverrideController[ThrowAttackerSlotKey] = attackerClip;

        // Write the victim clip into the enemy's override controller if it has one
        if (_enemyCombat != null)
            _enemyCombat.OverrideController[ThrowVictimSlotKey] = victimClip;

        // Trigger throw animation on both actors
        _animator.SetTrigger(HashWheelThrow);
        _enemyAnimator.SetTrigger(HashWheelThrow);
        if (_enemyMovement != null)
            _enemyMovement.CanBeClinched = false;    

        /*

        // Zero all input parameters on both animators so the throw blend tree
        // cannot be steered by any stale or live input values.
        _animator.SetFloat(HashInputX, 0f);
        _animator.SetFloat(HashInputY, 0f);
        _animator.SetFloat(HashRawInputX, 0f);
        _animator.SetFloat(HashRawInputY, 0f);
        _animator.SetBool(HashRawIsRunning, false);
        _enemyAnimator.SetFloat(HashInputX, 0f);
        _enemyAnimator.SetFloat(HashInputY, 0f);
        _enemyAnimator.SetFloat(HashRawInputX, 0f);
        _enemyAnimator.SetFloat(HashRawInputY, 0f);
        _enemyAnimator.SetBool(HashRawIsRunning, false);

        // Also zero the cached values in MovementComponent so SyncAnimationFromSource
        // does not immediately re-mirror stale non-zero values back onto the enemy animator.
        if (_movement != null)
            _movement.ZeroAnimatorInputs();

            _enemyMovement.ZeroAnimatorInputs();

         RemoveUkeParentConstraint();
        if (_grabbedEnemy != null)
           _grabbedEnemy.position = transform.position + transform.forward * 0.5f;

         SetupEnemyParentConstraint(_grabbedEnemy);  
        */

        // Snap both actors to a shared throw start position: attacker stays put,
        // victim is placed 0.1 units in front of the attacker. Rotations are untouched.

        RemoveUkeParentConstraint();


        if (_health.characterEffects != null && _health.characterEffects.sfxThrowVocal != null)
            JSAM.AudioManager.PlaySound(_health.characterEffects.sfxThrowVocal);

      


       // if (_enemyMovement != null)
         //   _enemyMovement.isImmobilized = true;

        _throwDirection = transform.forward;
        _isExecutingThrow = true;
        
        _throwLaunchFired = false;



        // Lock the attacker's movement so keyboard input cannot rotate the
        // thrower or update Input_XFloat / Input_YFloat during the throw clip.
        if (_movement != null)
        {
            _movement.isMovementLocked = true;
            _movement.canRotate = false;
            
        }

        // Disconnect the animation sync so SyncAnimationFromSource cannot
        // re-mirror player input floats onto the enemy during the throw clip.
        //if (_enemyMovement != null)
          //  _enemyMovement.syncAnimationSource = null;
    }

    public void ExecuteClinchLightAttack()
    {
        if (!_isClinching) return;
        if (_enemyAnimator == null) return;

        AnimationClip attackerClip = _combat.ClinchLightAtkAttackerClip;
        AnimationClip defenderClip = _combat.ClinchLightAtkDefenderClip;
        if (attackerClip == null || defenderClip == null) return;

        // If a clinch attack is already active (combo window repeat), end it cleanly
        // before starting the new one so hitbox, hit cache and state are reset.
       

        _combat.OverrideController[LightAtkAttackerSlotKey] = attackerClip;

        if (_enemyCombat != null)
            _enemyCombat.OverrideController[LightAtkDefenderSlotKey] = defenderClip;


        _animator.Play("ReplaceableLightAtk-Attacker", -1, 0f);  
        _enemyAnimator.Play("ReplaceableLightAtk-Defender", -1, 0f);

        _animator.SetBool(HashIsAction, true);
       
        /*  
          _animator.ResetTrigger(HashClinchLightAtk);
          _enemyAnimator.ResetTrigger(HashClinchLightAtk);
          _animator.SetTrigger(HashClinchLightAtk);
          _enemyAnimator.SetTrigger(HashClinchLightAtk);

          */
    }

    public void BreakClinch()
    {
        if (!CanBreakClinch) return;
                

        _isBreakingClinch = true;

        // Release the constraint before triggering break animations
        // so neither actor is physics-locked during the break
        RemoveUkeParentConstraint();

        _animator.applyRootMotion = false;
        if (_enemyAnimator != null)
            _enemyAnimator.applyRootMotion = true;

        if (_movement != null)
            _movement.isMovementLocked = true;

        _animator.SetTrigger(HashBreakClinch);

        if (_enemyAnimator != null)
            _enemyAnimator.SetTrigger(HashBreakClinch);
    }

    private void Update()
    {
        if (_isExecutingThrow && !_throwLaunchFired)
        {
            if (_activeThrowData != null)
            {
                AnimatorStateInfo throwState = _animator.GetCurrentAnimatorStateInfo(0);
                if (throwState.IsName("ReplaceableThrow-Attacker") &&
                    throwState.normalizedTime >= _activeThrowData.throwLaunchActivation)
                {
                    _throwLaunchFired = true;
                    LaunchEnemy(_activeThrowData);
                    return;
                }
            }
        }

      

        if (_isClinching)
        {
            AnimatorStateInfo lightAtkState = _animator.GetCurrentAnimatorStateInfo(0);
            if (lightAtkState.IsName("ReplaceableLightAtk-Attacker"))
            {
                // If the animation has finished and no AnimationStateNotifier fired ClipEnded
                // (e.g. the notifier is missing or misconfigured on this state), reset manually
                // so IsAttacking and isAction don't stay true indefinitely.
                if (lightAtkState.normalizedTime >= 1f)
                {
                    _animator.SetBool(HashIsAction, false);
                    _combat.ResetCombatState();
                }
                else
                {
                    _combat.TickClinchAttack(lightAtkState.normalizedTime);
                }
            }
        }
    }

    private void LaunchEnemy(CombatThrow throwData)
    {
        if (_enemyRigidbody == null || _grabbedEnemy == null) return;

        RemoveUkeParentConstraint();
       

        // Stop animation-driven movement on the enemy
        _enemyAnimator.applyRootMotion = false;
        _enemyAnimator.SetBool(HashIsGrounded, false);

        // Fully remove the parent constraint so it cannot fight the rigidbody during flight

        // Unlock movement so MovementComponent stops zeroing linearVelocity each frame
        if (_enemyMovement != null)
        {
            _enemyMovement.isMovementLocked = false;
            _enemyMovement.syncAnimationSource = null;
            _enemyMovement.syncAnimatorSpeed = false;
            _enemyMovement.isInFlight = true;
        }

        // Disable collider for the first few frames to avoid immediate floor re-detection
        if (_enemyCollider != null)
        {
            _enemyCollider.enabled = false;
            _colliderReenableTimer = ColliderDisableDuration;
        }

        // Parabolic launch: derive vertical and horizontal velocity components
        // from arc height and horizontal distance using projectile motion equations.




        float gravity = Mathf.Abs(Physics.gravity.y * 0.8f);
        float timeToApex = Mathf.Sqrt(2f * throwData.throwArcHeight / gravity);
        float verticalVelocity = gravity * timeToApex;
        float horizontalSpeed = throwData.throwDistance / (2f * timeToApex);
        Vector3 launchDirection = throwData.flipThrow ? -_throwDirection : _throwDirection;
        Vector3 launchVelocity = (launchDirection * horizontalSpeed + Vector3.up * verticalVelocity) * _throwLaunchSpeedMultiplier;
        _enemyRigidbody.linearVelocity = launchVelocity;

        
        _enemyInFlight = true;
    }

    /*
    private void OnAnimatorMove()
    {
        if (!_clinchRootMotionActive || _rigidbody == null) return;

        _rigidbody.MovePosition(_rigidbody.position + _animator.deltaPosition);
    }*/

    private void FixedUpdate()
    {
        // Countdown before re-enabling the enemy collider post-launch
        if (_colliderReenableTimer > 0f)
        {
            _colliderReenableTimer -= Time.fixedDeltaTime;
            if (_colliderReenableTimer <= 0f && _enemyCollider != null)
                _enemyCollider.enabled = true;
        }

        if (!_enemyInFlight || _enemyLanded) return;
        if (_grabbedEnemy == null || _enemyRigidbody == null) return;

        // Only test for floor once the collider is back on and the enemy is moving downward
        if (_colliderReenableTimer > 0f) return;
        if (_enemyRigidbody.linearVelocity.y > 0f) return;

        // SphereCast downward from the enemy's current position to detect the Floor layer
        float castRadius = 0.2f;
        float castDistance = 0.4f;
        bool hitFloor = Physics.SphereCast(
            _grabbedEnemy.position + Vector3.up * castRadius,
            castRadius,
            Vector3.down,
            out _,
            castDistance,
            FloorLayerMask,
            QueryTriggerInteraction.Ignore);

        if (!hitFloor) return;

        // Enemy has landed — stop immediately and start the get-up delay
        _enemyLanded = true;
        _enemyInFlight = false;

        //_enemyRigidbody.linearVelocity = Vector3.zero;
        //_enemyRigidbody.angularVelocity = Vector3.zero;

        if (_enemyMovement != null)
            _enemyMovement.isInFlight = false;

        // Apply throw damage on landing impact
        if (_enemyHealth != null && _activeThrowData != null)
            _enemyHealth.TakeDamage(_activeThrowData.damage, HitReactionType.None);

        // Play landing SFX
        if (_enemyHealth != null && _enemyHealth.characterEffects != null && _enemyHealth.characterEffects.sfxLandAfterThrown != null)
            JSAM.AudioManager.PlaySound(_enemyHealth.characterEffects.sfxLandAfterThrown);

        // Tell the enemy animator to enter the grounded state
        if (_enemyAnimator != null)
            _enemyAnimator.SetBool(HashIsGrounded, true);

        StartCoroutine(LandedGetUpSequence());
    }

    private void ClinchSequence(Transform target)
    {
        //Clinch Role:    
        //0. no clinch
        //1. attacker logic
        //2. defender logic 
      


        
        // Lock both animators to speed 1 so locomotion blend trees play at the same rate.
        // Original speeds are captured here and restored when the clinch ends.
        _playerOriginalAnimSpeed = _animator.speed;
        _enemyOriginalAnimSpeed = _enemyAnimator.speed;
        _animator.speed = 1f;
        _enemyAnimator.speed = 1f;

        _animator.SetInteger("ClinchRole", 1);
        _enemyAnimator.SetInteger("ClinchRole", 2);


        

        // Lock attacker animator.speed so MovementComponent.Update cannot overwrite the 1f we set.
        if (_movement != null)
            _movement.isClinchActive = true;

        // Mark clinch as active
        _isClinching = true;

        // Disable collision between player and enemy to prevent physics glitches
        if (_playerCollider != null && _enemyCollider != null)
            Physics.IgnoreCollision(_playerCollider, _enemyCollider, true);

        if (CameraZoneManager.Instance != null)
            CameraZoneManager.Instance.SetClinchZoom(true);
    }

    public void EndClinchTori()
    {
        if (!_isClinching) return;

        _isClinching = false;

        _clinchRootMotionActive = false;
        _animator.speed = _playerOriginalAnimSpeed;

        _animator.SetInteger("ClinchRole", 0);
        _animator.SetBool(HashInClinch, false);
        _animator.applyRootMotion = false;

        if (_movement != null)
            _movement.isClinchActive = false;

        if (CameraZoneManager.Instance != null)
            CameraZoneManager.Instance.SetClinchZoom(false);
    }

    public void EndClinchUke()
    {
        if (_enemyAnimator == null) return;

        if (_enemyHealth != null)
            _enemyHealth.OnDeath -= AbortClinchOnEnemyDeath;

        _enemyAnimator.applyRootMotion = false;
        _enemyAnimator.speed = _enemyOriginalAnimSpeed;

        _enemyAnimator.SetInteger("ClinchRole", 0);
        _enemyAnimator.SetBool(HashInClinch, false);
        _enemyAnimator.SetBool(HashIsGrounded, true);
       // ResetMovementParams(_enemyAnimator);

        RemoveUkeParentConstraint();
        // Unlock enemy movement and clear animation sync (no-op if already done at launch)
        // Note: isImmobilized is intentionally NOT cleared here after a throw.
        // It is cleared by AnimationStateExitNotifier on the get-up clip in MovementComponent.OnAnimationStateExit.
        if (_enemyMovement != null)
        {
            _enemyMovement.isMovementLocked = false;
            _enemyMovement.syncAnimationSource = null;
            _enemyMovement.isInFlight = false;
        }

        // Re-enable collision
        if (_playerCollider != null && _enemyCollider != null)
        {
            Physics.IgnoreCollision(_playerCollider, _enemyCollider, false);
        }

        // Re-enable AI brain now that the clinch is fully resolved
        if (_enemyBrain != null)
        {
            _enemyBrain.enabled = true;
            _enemyBrain = null;
        }
    }

    private void AbortClinchOnEnemyDeath()
    {
        if (_enemyHealth != null)
            _enemyHealth.OnDeath -= AbortClinchOnEnemyDeath;

        AbortClinch();
    }

    private void AbortClinch()
    {
        _isExecutingThrow = false;
        _throwLaunchFired = false;
        _enemyInFlight = false;
        _enemyLanded = false;
        _colliderReenableTimer = 0f;
        _isInClinchRecovery = false;
        _clinchRootMotionActive = false;
        StopAllCoroutines();

        if (_enemyCollider != null)
            _enemyCollider.enabled = true;

        RemoveUkeParentConstraint();

        if (_enemyMovement != null)
        {
            _enemyMovement.isMovementLocked = false;
            _enemyMovement.syncAnimationSource = null;
            _enemyMovement.isInFlight = false;
            _enemyMovement.isImmobilized = false;
            _enemyMovement.CanBeClinched = false;
        }

        if (_enemyAnimator != null)
        {
            _enemyAnimator.applyRootMotion = false;
            _enemyAnimator.speed = _enemyOriginalAnimSpeed;
        }

        if (_animator != null)
            _animator.speed = _playerOriginalAnimSpeed;

        if (_playerCollider != null && _enemyCollider != null)
            Physics.IgnoreCollision(_playerCollider, _enemyCollider, false);

        if (_enemyBrain != null)
        {
            _enemyBrain.enabled = true;
            _enemyBrain = null;
        }

        // Reset tori (attacker) side
        _isClinching = false;
        if (_movement != null)
        {
            _movement.isClinchActive = false;
            _movement.isMovementLocked = false;
            _movement.canRotate = true;
        }
        if (_animator != null)
        {
            _animator.applyRootMotion = false;
            _animator.SetInteger("ClinchRole", 0);
            _animator.SetBool(HashInClinch, false);
        }

        if (CameraZoneManager.Instance != null)
            CameraZoneManager.Instance.SetClinchZoom(false);

        _grabbedEnemy = null;
        _enemyAnimator = null;
        _enemyRigidbody = null;
        _enemyCollider = null;
        _enemyMovement = null;
        _enemyHealth = null;
        _enemyCombat = null;
        _activeThrowData = null;
    }

    #region Constraint Management   
    private void SetupEnemyParentConstraint(Transform target)
    {
        // Position the enemy at clinch distance, facing opposite direction
        Vector3 forwardDirection = transform.forward;
        Vector3 targetPosition = transform.position + (forwardDirection * _clinchDistance);
        
        // Set enemy position
        target.position = targetPosition;
        
        // Rotate enemy to face exact opposite direction (180 degrees)
        Quaternion oppositeRotation = transform.rotation * Quaternion.Euler(0f, 180f, 0f);
        target.rotation = oppositeRotation;
        
        // Setup or get existing ParentConstraint
        _enemyParentConstraint = target.GetComponent<ParentConstraint>();
        
        if (_enemyParentConstraint == null)
        {
            _enemyParentConstraint = target.gameObject.AddComponent<ParentConstraint>();
        }
        else
        {
            // Clear existing sources
            for (int i = _enemyParentConstraint.sourceCount - 1; i >= 0; i--)
            {
                _enemyParentConstraint.RemoveSource(i);
            }
        }

        // Create constraint source pointing to the attacker
        ConstraintSource source = new()
        {
            sourceTransform = transform,
            weight = 1f
        };
        
        // Configure constraint to maintain the offset and rotation
        _enemyParentConstraint.AddSource(source);
        
        // Set translation and rotation offsets to maintain relative position/rotation
        Transform clinchPositionMarker = transform.Find("Clinch-Position");
        Vector3 translationOffset = clinchPositionMarker != null
            ? clinchPositionMarker.localPosition
            : new Vector3(0f, 0f, _clinchDistance);
        _enemyParentConstraint.SetTranslationOffset(0, translationOffset);
        _enemyParentConstraint.SetRotationOffset(0, new Vector3(0f, 180f, 0f));
        
        // Lock axes as needed - typically you want to maintain Y rotation
        _enemyParentConstraint.translationAtRest = Vector3.zero;
        _enemyParentConstraint.rotationAtRest = Vector3.zero;
        
        _enemyParentConstraint.constraintActive = true;
        _enemyParentConstraint.locked = true;
        
        // Force constraint evaluation
        _enemyParentConstraint.weight = 1f;
    }

    private void RemoveUkeParentConstraint()
    {
        if (_enemyParentConstraint != null)
        {
            _enemyParentConstraint.constraintActive = false;
            
            if (_enemyParentConstraint.sourceCount > 0)
            {
                for (int i = _enemyParentConstraint.sourceCount - 1; i >= 0; i--)
                {
                    _enemyParentConstraint.RemoveSource(i);
                }
            }
            
            Destroy(_enemyParentConstraint);
            _enemyParentConstraint = null;
        }
    }

    #endregion

    #region Animation State Callbacks   
    public void OnAnimationStateExit(int layerIndex, AnimationExitEvent exitEvent)
    {
        if (exitEvent == AnimationExitEvent.EndThrow)
            HandleThrowExit();
        else if (exitEvent == AnimationExitEvent.BreakClinch)
            HandleBreakToriExit();
        else if (exitEvent == AnimationExitEvent.ClipEnded)
        {
            _animator.SetBool(HashIsAction, false);
            _combat.ResetCombatState();
        }
        else if (exitEvent == AnimationExitEvent.ClipInterrupted)
        {
            // Clinch light attack was interrupted mid-clip (e.g. repeated during combo window).
            // NotifyClinchAttackEnded is not called here because ExecuteClinchLightAttack
            // already called it before starting the new rep.
        }
    }

    private void HandleBreakToriExit()
    {
        _isBreakingClinch = false;
        _isInClinchRecovery = true;
        if (_movement != null)
        {
            _movement.isMovementLocked = false;
            _movement.CanBeClinched = true; 
        }
        EndClinchUke();
        EndClinchTori();
        StartCoroutine(ClinchRecovery());
    }

    private void HandleThrowExit()
    {
        _isExecutingThrow = false;
        _throwLaunchFired = false;

        // Restore attacker movement that was locked in ExecuteWheelThrow
        if (_movement != null)
        {
            _movement.isMovementLocked = false;
            _movement.canRotate = true;
        }

        EndClinchTori();
    }

    private IEnumerator LandedGetUpSequence()
    {
        yield return new WaitForSeconds(LandedGetUpDelay);

        // End the uke side of the clinch
        EndClinchUke();

        // Fire get-up trigger if still alive, otherwise death handles itself
        if (_enemyHealth != null && !_enemyHealth.IsDead)
            _enemyAnimator.SetTrigger(HashGettingUp);

        _enemyLanded = false;

        StartCoroutine(ClinchRecovery());
    }

    private IEnumerator ClinchRecovery()
    {
        _isInClinchRecovery = true;
        yield return new WaitForSeconds(CLINCH_RECOVERY_DURATION);
        _isInClinchRecovery = false;
        if (_movement != null)
            _movement.CanBeClinched = true;
    }
    #endregion  
}