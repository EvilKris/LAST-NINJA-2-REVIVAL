using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;
using JSAM;

[RequireComponent(typeof(Rigidbody))]
public class MovementComponent : MonoBehaviour, IAnimationStateListener, ISMBReceiver
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
    // Cached HealthComponent reference (set in Awake) to avoid repeated TryGetComponent calls
    private HealthComponent _healthComponent;

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

        // Cache HealthComponent for later subscriptions
        _healthComponent = GetComponent<HealthComponent>();

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
        if (_healthComponent != null)
            _healthComponent.OnDeath += () => isImmobilized = false;
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




    public void OnAnimationSignal(string functionName, AnimationStateEvent.StateEvent data)
    {
        if (functionName == "WorshipAndRegainHealth")
        {
            //DONT FORGET TO INCLUDE THIS FUNCTION NAME IN THE ANIMATION EVENT! This is the link between the animation and the code that restores health during the worship sequence.

            // This function is called by an Animation Event at the kneeling part of the worship animation.
            // It restores the player's health to full as part of the worship sequence.

            if (_healthComponent != null)
            {
                _animator.speed = 0f; // Freeze animation to hold the kneeling pose while health is restored
                _healthComponent.OnHealthChanged += OnHealthMax; // Listen for health changes to detect when health is fully restored
                // Start the heal-to-full sequence. HealthComponent exposes HealToFull().
                _healthComponent.HealToFull();
                _healsfx = AudioManager.PlaySound(MasterSingleton.Instance.PrefabBankManager.HealingSound);
                _healsfx.Play();
            }
        }

        if (functionName == "RestoreMovement")
        {
            // This function can be called by an Animation Event at the end of the any animation as a backup to ensure movement is restored.
            // currently used at the end of the worship animation to ensure the player regains control even if something goes wrong with the health restoration.
            RestoreMovement();
        }
    }

    private void OnHealthMax(float hp, float maxhp, Faction faction)
    {
        // Only proceed if we actually hit the target
        if (hp >= maxhp)
        {
            if (_healsfx != null)
                _healsfx.Stop(); // Stop the healing sound effect if it's still playing    


            MasterSingleton.Instance.PlayerManager.RestoreSharedMaterials(gameObject); // Restore original materials to remove any visual effects applied during the worship sequence


            // Only unsubscribe once the condition is met
            if (_healthComponent != null)
            {
                _healthComponent.OnHealthChanged -= OnHealthMax;
            }

            if (_activeWorshipTrigger != null)
            {
                _activeWorshipTrigger.StartCooldown(20f);
                _activeWorshipTrigger = null;
            }

            // IMPORTANT: If you want movement to work immediately, 
            // you might need to toggle this:
            isMovementLocked = false;
        }
    }

    public void RestoreMovement()
    {
        // This function can be called by an Animation Event at the end of the any animation as a backup to ensure movement is restored.
        // currently used at the end of the worship animation to ensure the player regains control even if something goes wrong with the health restoration.
        isImmobilized = false;
        isMovementLocked = false;
        canRotate = true;
        isClinchActive = false;
        CanBeClinched = true;
        syncAnimationSource = null;
        syncAnimatorSpeed = false;

        // Reset the animator back to the base locomotion state so no death/worship/drowning
        // animation is left frozen on screen when the player regains control.
        if (_animator != null)
        {
            // Restore speed to the normal movement rate — it may have been frozen at 0
            // (e.g. during the worship kneel hold) or set to an attack/acrobatic speed.
            //_animator.speed = movementSpeed * movementAnimSpeedModifier;

            // Cross-fade into the base Locomotion blend tree on layer 0 at normalised
            // time 0 so the idle pose is shown immediately rather than mid-clip.
            _animator.CrossFade("Idle", 0.15f, 0, 0f);
        }

        /*
         useRootMotion = false;
         if (_animator != null)
             _animator.applyRootMotion = false;*/
    }

    //Functions for worshipping at the Buddha Shrine. Called by AnimationStateEvent when the worship animation starts. This is where we set up the state for the worship sequence, which includes immobilizing the player, enabling root motion, and playing the worship animation. The actual healing will be handled by an event at the end of the animation that restores health to full.


    // Optional source trigger that initiated worship so we can notify it when finished
    private TriggerDetectorManager _activeWorshipTrigger;
    private SoundChannelHelper _healsfx;
    private SoundChannelHelper _bubblesSfx;

    public void BeginWorshipSequence(TriggerDetectorManager source = null, Vector3? forwardDirection = null)
    {
        // Immediately stop all ongoing movement/combat actions so the worship
        // animation can play cleanly and drive motion via root motion.
        // 1) Stop physics movement
        ZeroVelocity();

        // 2) Clear animator inputs so locomotion blend trees cannot be steered
        ZeroAnimatorInputs();

        // TESTING: set health to 20% at the start of the worship sequence
        if (_healthComponent != null)
        {
            _healthComponent.SetHealthPercentage(0.2f);
        }

        // 3) Lock movement and prevent rotation/input driven changes
        isImmobilized = true;
        isMovementLocked = true;
        canRotate = false;
        isClinchActive = false;
        CanBeClinched = false;
        syncAnimationSource = null;
        syncAnimatorSpeed = false;

        // 4) Reset combat state to cancel any active attacks/blocks/etc.
        if (_combatHandler != null)
            _combatHandler.ResetCombatState();

        // 5) Enable root motion and tell this component to use it
        if (_animator != null)
            _animator.applyRootMotion = true;
        useRootMotion = true;

        // 6) Play the worship animation by name (explicit clip expected in animator)
        //_animator.Play("Worship-Buddha", -1, 0f);
        _animator.CrossFade("Worship-Buddha", 0.3f);

        // remember the triggering detector so we can start its cooldown when healing completes
        _activeWorshipTrigger = source;

        // If a forward direction was supplied by the trigger, rotate the rigidbody to face that
        // direction using DOTween. Use Rigidbody.MoveRotation inside the tween setter so the
        // rotation is applied in a physics-friendly way.
        if (forwardDirection.HasValue)
        {
            Vector3 dir = forwardDirection.Value;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Vector3 targetEuler = Quaternion.LookRotation(dir).eulerAngles;
                DOTween.To(() => _rb.rotation.eulerAngles, x => _rb.MoveRotation(Quaternion.Euler(x)), targetEuler, 0.5f).SetEase(Ease.OutSine);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Drowning Death Sequence
    // ═══════════════════════════════════════════════════════════════════

    [Header("Drowning Settings")]
    [Tooltip("How fast the player sinks when drowning (units/sec).")]
    [SerializeField] private float drownSinkSpeed = 0.4f;
    [Tooltip("Seconds the sinking animation plays before the respawn / game-over check.")]
    //[SerializeField] private float drownSinkDuration = 3f;

    private bool _isDrowning;
    private Coroutine _drowningCoroutine;

    /// <summary>
    /// Called by TriggerDetectorManager when the player enters a Death_By_Drowning trigger.
    /// Locks all controls, plays the drowning animation, sinks the player, then either
    /// respawns at the nearest RespawnPoint (minus a life) or loads the main menu on game over.
    /// </summary>
    public void BeginDrowningSequence(Vector3 splashPoint)
    {
        MasterSingleton.Instance.PlayerManager.ToggleXrayRendererFeatures(false);


        // Spawn a splash particle at the water-surface contact point
       // SpawnSplash(splashPoint, Vector3.up);

        SpawnSplash(transform.position, Vector3.up);

        if (_isDrowning) return;
        _isDrowning = true;

        // 1) Stop physics movement
        ZeroVelocity();
        ZeroAnimatorInputs();

        // 2) Lock all player control
        isImmobilized = true;
        isMovementLocked = true;
        canRotate = false;
        isClinchActive = false;
        CanBeClinched = false;
        syncAnimationSource = null;
        syncAnimatorSpeed = false;

        // 3) Reset combat state
        if (_combatHandler != null)
            _combatHandler.ResetCombatState();

        // 4) Disable root motion — sinking is driven manually
        if (_animator != null)
            _animator.applyRootMotion = true;
        useRootMotion = true;

        // 5) Disable all non-trigger colliders so the sinking body doesn't push geometry
        SetEntityCollidersActive(false);

        // 6) Disable gravity so we control the downward motion ourselves
        _rb.useGravity = false;
        _rb.linearVelocity = Vector3.zero;

        // 6) Play the drowning animation
        if (_animator != null)
            _animator.CrossFade("Death-Drowning", 0.3f);

        // 7) Start the sink + respawn coroutine
        if (_drowningCoroutine != null)
            StopCoroutine(_drowningCoroutine);
        _drowningCoroutine = StartCoroutine(DrowningRoutine());
    }

    /// <summary>
    /// Enables or disables every non-trigger <see cref="Collider"/> in this entity's hierarchy.
    /// Trigger colliders are intentionally skipped — they belong to detection systems
    /// (e.g. <see cref="TriggerDetectorManager"/>) and must remain independent.
    /// </summary>
    public void SetEntityCollidersActive(bool active)
    {
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (!col.isTrigger)
                col.enabled = active;
        }
    }

    private void SpawnSplash(Vector3 position, Vector3 upDirection)
    {
        PrefabBankManager bank = MasterSingleton.Instance.PrefabBankManager;
        if (bank == null || bank.SwampDrowningSplashes == null) return;

        AudioManager.PlaySound(bank.DrowningSound_swamp);
        AudioManager.PlaySound(_healthComponent.characterEffects.drowningDeath);


        _bubblesSfx = AudioManager.PlaySound(MasterSingleton.Instance.PrefabBankManager.Bubbles);
        _bubblesSfx.Play();


        Quaternion rotation = upDirection != Vector3.zero
            ? Quaternion.LookRotation(upDirection)
            : Quaternion.identity;


       

        GameObject instance = Instantiate(bank.SwampDrowningSplashes, position, Quaternion.identity);

        ParticleSystem ps = instance.GetComponent<ParticleSystem>();
        if (ps == null || !ps.main.stopAction.Equals(ParticleSystemStopAction.Destroy))
            Destroy(instance, 5f);

       
    }

    private IEnumerator DrowningRoutine()
    {
        // Sink for a fixed 5 seconds regardless of drownSinkDuration, then hand off to GameManager
        float elapsed = 0f;
        const float maxSinkTime = 5f;
        while (elapsed < maxSinkTime)
        {
            _rb.MovePosition(_rb.position + drownSinkSpeed * Time.fixedDeltaTime * Vector3.down);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if(_bubblesSfx != null) 
            _bubblesSfx.Stop();

        _isDrowning = false;
        _drowningCoroutine = null;

        // Hand the full death sequence (life loss, fade, respawn / game-over) to GameDataManager
        MasterSingleton.Instance.GameDataManager.HandlePlayerDeath(this);
    }


}