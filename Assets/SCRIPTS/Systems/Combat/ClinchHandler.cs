using System;
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

    [Header("Throw Physics")]
    [SerializeField] private float _throwArcHeight = 2f;
    [SerializeField] private float _throwDistance = 7f;
    [SerializeField] private float _throwRotationSpeed = 2f;
    #endregion

    #region Enemy State (Cached During Clinch)
    private Transform _grabbedEnemy;
    private Animator _enemyAnimator;
    private AnimatorOverrideController _enemyOverrideController;
    private Rigidbody _enemyRigidbody;
    private Collider _enemyCollider;
    private MovementComponent _enemyMovement;
    private float _enemyOriginalAnimSpeed;
    private ParentConstraint _enemyParentConstraint;
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
    private bool _isExecutingThrow;
    private bool _throwLaunchFired;
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

    #region Throw Animation Slot Keys
    private const string ThrowAttackerSlotKey = "ReplaceableThrow-Attacker";
    private const string ThrowVictimSlotKey = "ReplaceableThrow-Victim";
    #endregion

    #region Animator Parameter Hashes
    
    private static readonly int HashInputX = Animator.StringToHash("Input_XFloat");
    private static readonly int HashInputY = Animator.StringToHash("Input_YFloat");
    private static readonly int HashWheelThrow = Animator.StringToHash("t_WheelThrow");
    private static readonly int HashBreakClinch = Animator.StringToHash("t_BreakClinch");
    private static readonly int HashIsRunning = Animator.StringToHash("isRunningBool");
    private int HashInClinch = Animator.StringToHash("b_InClinch");
    private static readonly int HashThrowTori = Animator.StringToHash("ReplaceableThrow-Attacker");
    private static readonly int HashThrowUke = Animator.StringToHash("ReplaceableThrow-Victim");
    private static readonly int HashClinchBreakTori = Animator.StringToHash("clinch-break-tori");
    #endregion

    #region Public Properties
    public bool IsClinching => _animator != null && _animator.GetBool(HashInClinch);
   
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
        _grabbedEnemy = target;
        _enemyAnimator = target.GetComponent<Animator>();
        _enemyMovement = target.GetComponent<MovementComponent>();
        _enemyRigidbody = target.GetComponent<Rigidbody>();
        _enemyCollider = target.GetComponent<Collider>();

        _enemyOverrideController = new AnimatorOverrideController(_enemyAnimator.runtimeAnimatorController);
        _enemyAnimator.runtimeAnimatorController = _enemyOverrideController;

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

        _enemyAnimator.SetBool(HashInClinch, true);
        _animator.SetBool(HashInClinch, true);

        // Lock enemy movement (uke cannot move during clinch)
        // and sync their animations to match the attacker's movement
        if (_enemyMovement != null)
        {
            _enemyMovement.isMovementLocked = true;
            _enemyMovement.syncAnimationSource = _movement;
            _enemyMovement.syncAnimatorSpeed = true;
        }

        SetupEnemyParentConstraint(target);
        ClinchSequence(target);
    }

    public void ExecuteWheelThrow()
    {
        if (!_isClinching) return;
        if (_grabbedEnemy == null || _enemyAnimator == null) return;

        CombatThrow throwData = _combat.currentStyle?.clinchThrowDefault;
        if (throwData == null) return;

        // Override throw clips on both animators before triggering the animation
        AnimatorOverrideController attackerOverride = _animator.runtimeAnimatorController as AnimatorOverrideController;
        if (attackerOverride != null)
            attackerOverride[ThrowAttackerSlotKey] = throwData.attackerThrowClip;

        if (_enemyOverrideController != null)
            _enemyOverrideController[ThrowVictimSlotKey] = throwData.victimThrowClip;

        // Trigger wheel throw animation on both attacker and victim
        _animator.SetTrigger(HashWheelThrow);
        _enemyAnimator.SetTrigger(HashWheelThrow);

        JSAM.AudioManager.PlaySound(_health.characterEffects.sfxThrowVocal);


        _enemyParentConstraint.constraintActive = false; // Disable constraint to allow physics-based throw 
        _animator.applyRootMotion = true;
        _enemyAnimator.applyRootMotion = true;
        



        _isExecutingThrow = true;
        _throwLaunchFired = false;
    }

    private void Update()
    {
        if (!_isExecutingThrow || _throwLaunchFired) return;

        CombatThrow throwData = _combat.currentStyle != null ? _combat.currentStyle.clinchThrowDefault : null;
        if (throwData == null) return;

        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName("ReplaceableThrow-Attacker")) return;

        if (stateInfo.normalizedTime >= throwData.throwLaunchActivation)
        {
            _throwLaunchFired = true;
            Debug.Log($"[ClinchHandler] Throw launch activation reached at normalizedTime={stateInfo.normalizedTime:F3} (threshold={throwData.throwLaunchActivation:F3})");
            Debug.Break();
        }
    }

    private void ResetMovementParams(Animator animator)
    {
        animator.SetFloat(HashInputX, 0f);
        animator.SetFloat(HashInputY, 0f);
        animator.SetBool(HashIsRunning, false);
    }
    private void ClinchSequence(Transform target)
    {
        //Clinch Role:    
        //0. no clinch
        //1. attacker logic
        //2. defender logic 
      
        _animator.SetInteger("ClinchRole", 1);
        _enemyAnimator.SetInteger("ClinchRole", 2);
        
        // Mark clinch as active
        _isClinching = true;
        
        // Disable collision between player and enemy to prevent physics glitches
        if (_playerCollider != null && _enemyCollider != null)
        {
            Physics.IgnoreCollision(_playerCollider, _enemyCollider, true);
        }

    }

    public void EndClinchTori()
    {
        if (!_isClinching) return;

        _isClinching = false;

        _animator.SetInteger("ClinchRole", 0);
        _animator.SetBool(HashInClinch, false);
       // ResetMovementParams(_animator);
        _animator.applyRootMotion = false;
    }

    public void EndClinchUke()
    {
        if (_enemyAnimator == null) return;

        _enemyAnimator.applyRootMotion = false;

        _enemyAnimator.SetInteger("ClinchRole", 0);
        _enemyAnimator.SetBool(HashInClinch, false);
       // ResetMovementParams(_enemyAnimator);

        RemoveEnemyParentConstraint();
        // Unlock enemy movement and clear animation sync
        if (_enemyMovement != null)
        {
            _enemyMovement.isMovementLocked = false;
            _enemyMovement.syncAnimationSource = null;
            _enemyMovement.syncAnimatorSpeed = false;
        }

        // Re-enable collision
        if (_playerCollider != null && _enemyCollider != null)
        {
            Physics.IgnoreCollision(_playerCollider, _enemyCollider, false);
        }

        
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
        ConstraintSource source = new ConstraintSource
        {
            sourceTransform = transform,
            weight = 1f
        };
        
        // Configure constraint to maintain the offset and rotation
        _enemyParentConstraint.AddSource(source);
        
        // Set translation and rotation offsets to maintain relative position/rotation
        _enemyParentConstraint.SetTranslationOffset(0, new Vector3(0f, 0f, _clinchDistance));
        _enemyParentConstraint.SetRotationOffset(0, new Vector3(0f, 180f, 0f));
        
        // Lock axes as needed - typically you want to maintain Y rotation
        _enemyParentConstraint.translationAtRest = Vector3.zero;
        _enemyParentConstraint.rotationAtRest = Vector3.zero;
        
        _enemyParentConstraint.constraintActive = true;
        _enemyParentConstraint.locked = true;
        
        // Force constraint evaluation
        _enemyParentConstraint.weight = 1f;
    }

    private void RemoveEnemyParentConstraint()
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
    public void OnAnimationStateExit(int stateHash, int layerIndex)
    {
        if (stateHash == HashThrowTori)
            HandleThrowExit();
        else if (stateHash == HashClinchBreakTori)
            HandleBreakToriExit();
    }

    private void HandleBreakToriExit()
    {
       
    }

    private void HandleThrowExit()
    {
       EndClinchTori();
    }
    #endregion  
}