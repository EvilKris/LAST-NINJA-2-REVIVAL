using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Main player controller that handles input from Unity's new Input System.
/// Coordinates between movement, combat, and other player systems.
/// Uses camera-relative movement for better third-person controls.
/// REQUIRES : PlayerInput component on the same GameObject.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    // Component references
    private CombatHandler _combat;
    private PlayerControls _controls; // Auto-generated Input Actions class
    private MovementComponent _movement;
    private HealthComponent _health;

    // Attack hold mechanic (hold for medium attack, tap for light)
    private float _attackHoldTimer;
    private bool _isHoldingAttack;

    // Camera and world interaction
    private Camera _mainCamera;
    private int _floorLayerMask;

    // Cached input values from Input System events
    private Vector2 _moveInput;

    private ITargetable _currentTarget; // Our current focus
    [SerializeField] private float lockBreakDistance = 1.0f; // 1 unit meter break distance
    [SerializeField] private float searchRadius = 3.5f; // How far we look for a target when attacking

    /// <summary>
    /// Initialize component references and create Input System instance.
    /// </summary>
    private void Awake()
    {
        _movement = GetComponent<MovementComponent>();
        _health = GetComponent<HealthComponent>();
        _combat = GetComponent<CombatHandler>();
        _mainCamera = Camera.main;
        _floorLayerMask = LayerMask.GetMask("Floor");

        _controls = new PlayerControls();
    }

    /// <summary>
    /// Enable input actions and subscribe to Input System events.
    /// Called when the component is enabled (scene start, object activation).
    /// </summary>
    private void OnEnable()
    {
        _controls.Player.Enable();

        // Movement & Look - Store input values in cached fields
        _controls.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
        _controls.Player.Move.canceled += ctx => _moveInput = Vector2.zero;

      //  _controls.Player.Look.performed += ctx => _mousePosition = ctx.ReadValue<Vector2>();

        // Combat - Subscribe to attack and ability inputs
        _controls.Player.LightAttack.started += OnLightAttackStarted;
        _controls.Player.LightAttack.canceled += OnLightAttackCanceled;
        _controls.Player.HeavyAttack.started += OnHeavyAttackStarted;
        _controls.Player.Block.started += OnBlockInput;
        _controls.Player.Block.canceled += OnBlockInput;
        _controls.Player.KIButton.started += OnKIInput;
        _controls.Player.Acrobatics.started += OnAcrobatics;
    }

    /// <summary>
    /// Disable input actions when component is disabled.
    /// Prevents input processing when player is inactive.
    /// </summary>
    private void OnDisable()
    {
        _controls.Player.Disable();
    }

    /// <summary>
    /// Track how long the attack button has been held.
    /// Used to differentiate between light (tap) and medium (hold) attacks.
    /// </summary>
    private void Update()
    {
        if (_isHoldingAttack)
            _attackHoldTimer += Time.deltaTime;
    }

    /// <summary>
    /// Process movement in physics update for consistent behavior.
    /// Converts input to camera-relative direction and applies movement.
    /// </summary>
    private void FixedUpdate()
    {
        if (_health != null && _health.IsDead) return;

        // 1. Convert input to camera-relative direction (Your existing logic)
        Vector3 moveDir = GetCameraRelativeDirection(_moveInput);

        // 2. Lock-On Logic: Distance Check
        if (_currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, _currentTarget.GetLockOnPoint().position);

            // Break lock if too far or target is dead
            if (dist > lockBreakDistance || !_currentTarget.IsValidTarget())
            {
                _currentTarget = null;
            }
        }

        // 3. Drive the MovementComponent
        if (_currentTarget != null)
        {
            // TARGETED MODE: Strafe relative to enemy
            _movement.ProcessMovement(moveDir, _currentTarget.GetLockOnPoint().position);
        }
        else
        {
            // FREESTYLE MODE: Run where the stick points
            if (_moveInput.sqrMagnitude > 0.01f)
            {
                _movement.ProcessMovement(moveDir);
                _movement.RotateTowardsDirection(moveDir);
            }
            else
            {
                _movement.ProcessMovement(Vector3.zero);
            }
        }
    }

    // --- COMBAT LOCK LOGIC ---

    private void TryLockOn()
    {
        // Only try to lock on if we don't have a target
        if (_currentTarget != null) return;

        Collider[] enemies = Physics.OverlapSphere(transform.position, searchRadius);
        foreach (var col in enemies)
        {
            if (col.TryGetComponent<ITargetable>(out var target))
            {
                // Only lock to Enemies (not allies/self)
                if (target.GetFaction() == Faction.Enemy && target.IsValidTarget())
                {
                    _currentTarget = target;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Converts 2D input (WASD/stick) into 3D world direction relative to camera orientation.
    /// This creates intuitive third-person controls where forward is always camera forward.
    /// </summary>
    /// <param name="input">2D input vector from controls</param>
    /// <returns>Normalized 3D world direction</returns>
    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        // Get camera's forward and right vectors
        Vector3 camForward = _mainCamera.transform.forward;
        Vector3 camRight = _mainCamera.transform.right;

        // Flatten to horizontal plane (ignore camera pitch)
        camForward.y = 0f;
        camRight.y = 0f;

        // Ensure unit vectors after flattening
        camForward.Normalize();
        camRight.Normalize();

        // Combine input with camera basis vectors
        // input.y = forward/back, input.x = left/right
        return (camForward * input.y + camRight * input.x).normalized;
    }

    /// <summary>
    /// Mouse-based rotation (currently disabled).
    /// Could be used for point-and-click style rotation to mouse cursor.
    /// </summary>
    private void HandleRotation()
    {
        /*
        // Using the stored _mousePosition from the C# event
        Ray ray = _mainCamera.ScreenPointToRay(_mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, _floorLayerMask))
        {
            _movement.RotateTowardsPoint(hit.point);
        }*/
    }

    // ═══════════════════════════════════════════════════════════════════
    // COMBAT INPUT CALLBACKS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called when light attack button is first pressed.
    /// Starts the hold timer to track if this is a tap or hold.
    /// </summary>
    private void OnLightAttackStarted(InputAction.CallbackContext _)
    {
        TryLockOn(); // Try to find a target the moment we swing
        _isHoldingAttack = true;
        _attackHoldTimer = 0f;
    }

    /// <summary>
    /// Called when light attack button is released.
    /// Executes medium attack if held >= 1 second, otherwise light attack.
    /// </summary>
    private void OnLightAttackCanceled(InputAction.CallbackContext _)
    {
        _isHoldingAttack = false;
        
        // Check hold duration to determine attack type
        if (_attackHoldTimer >= 1.0f) 
            _combat.ExecuteMediumAttack();
        else 
            _combat.ExecuteLightAttack();
    }

    /// <summary>
    /// Called when heavy attack button is pressed.
    /// Resets hold timer to prevent accidental light attack on release.
    /// </summary>
    private void OnHeavyAttackStarted(InputAction.CallbackContext _)
    {
        // Design note: Heavy attacks reset the hold timer to prevent accidental Light releases
        TryLockOn(); // Try to find a target the moment we swing
        _isHoldingAttack = false;
        _combat.ExecuteHeavyAttack();
    }

    /// <summary>
    /// Called when block button is pressed or released.
    /// Passes the pressed state to combat handler.
    /// </summary>
    private void OnBlockInput(InputAction.CallbackContext context)
    {
        _combat.SetBlocking(context.started);
    }

    /// <summary>
    /// Called when acrobatics button is pressed (dodge/flip).
    /// Rotates player towards movement direction before executing.
    /// </summary>
    private void OnAcrobatics(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        // Face movement direction before flipping
        if (_moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 dir = GetCameraRelativeDirection(_moveInput);
            _movement.RotateTowardsDirection(dir);
        }

        _combat.ExecuteAcrobatics();
    }

    /// <summary>
    /// Called when KI/special ability button is pressed.
    /// Context-sensitive: can trigger parry, power-up, or magic depending on game state.
    /// </summary>
    public void OnKIInput(InputAction.CallbackContext _) => _combat.HandleKIInput();
}