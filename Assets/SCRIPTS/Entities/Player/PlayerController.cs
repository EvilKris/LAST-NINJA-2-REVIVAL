using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Main player controller that handles input from Unity's new Input System.
/// Coordinates between movement, combat, and the global Inventory system via MasterSingleton.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    // ========================================
    // COMPONENT REFERENCES
    // ========================================
    /// <summary>Handles all combat-related actions (attacks, blocking, abilities).</summary>
    private CombatHandler _combat;
    
    /// <summary>Auto-generated Input Actions class from Unity's new Input System.</summary>
    private PlayerControls _controls;
    
    /// <summary>Manages player movement, rotation, and physics.</summary>
    private MovementComponent _movement;
    
    /// <summary>Tracks player health and death state.</summary>
    private HealthComponent _health;
    
    /// <summary>Manages weapon and item inventory, accessed via MasterSingleton.</summary>
    private InventoryManager _inventoryManager;

    /// <summary>Handles clinch/grappling mechanics.</summary>
    private ClinchHandler _clinchHandler;

    // ========================================
    // ATTACK HOLD MECHANIC
    // ========================================
    /// <summary>Tracks how long the attack button has been held (for charge attacks).</summary>
    private float _attackHoldTimer;
    
    /// <summary>Whether the player is currently holding the attack button.</summary>
    private bool _isHoldingAttack;

    // ========================================
    // CAMERA AND WORLD INTERACTION
    // ========================================
    /// <summary>Reference to the main camera for input direction calculations.</summary>
    private Camera _mainCamera;
    
    /// <summary>Cached transform of the main camera.</summary>
    private Transform _cameraTransform;
    
    /// <summary>Layer mask for the floor layer (used for raycasting).</summary>
    private int _floorLayerMask;

    // ========================================
    // INPUT CACHING
    // ========================================
    /// <summary>Cached movement input from Input System (WASD/Left Stick).</summary>
    private Vector2 _moveInput;
    
    /// <summary>Cached camera Y-axis rotation, updated only when movement state changes for performance.</summary>
    private float _cachedCameraYaw;
    
    /// <summary>Tracks whether the player was moving in the previous frame to detect state changes.</summary>
    private bool _wasMovingLastFrame;

    // ========================================
    // TARGET LOCK SYSTEM
    // ========================================
    /// <summary>The current target the player is locked onto (null if none).</summary>
    private ITargetable _currentTarget;
    
    /// <summary>Distance at which the target lock will automatically break.</summary>
    [SerializeField] private float lockBreakDistance = 1.0f;
    
    /// <summary>Radius in which to search for enemies when attempting to lock on.</summary>
    [SerializeField] private float searchRadius = 3.5f;
    
    // ========================================
    // PERFORMANCE OPTIMIZATION
    // ========================================
    /// <summary>Squared lock break distance (cached to avoid sqrt operations in FixedUpdate).</summary>
    private float _lockBreakDistanceSqr;
    
    /// <summary>Minimum squared magnitude for movement input to register (prevents stick drift).</summary>
    private float _moveThresholdSqr = 0.0001f;

    // ========================================
    // UNITY LIFECYCLE METHODS
    // ========================================
    
    /// <summary>
    /// Initialize component references and create Input System instance.
    /// Called once when the script instance is being loaded.
    /// </summary>
    private void Awake()
    {
        // Get required components attached to this GameObject
        _movement = GetComponent<MovementComponent>();
        _health = GetComponent<HealthComponent>();
        _combat = GetComponent<CombatHandler>();
        _clinchHandler = GetComponent<ClinchHandler>();
        
        // Cache camera references for input direction calculations
        _mainCamera = Camera.main;
        _cameraTransform = _mainCamera.transform;
        
        // Set up layer mask for floor detection
        _floorLayerMask = LayerMask.GetMask("Floor");

        // Create new instance of the Input Actions class
        _controls = new PlayerControls();
        
        // Cache squared distance to avoid expensive sqrt operations in FixedUpdate
        _lockBreakDistanceSqr = lockBreakDistance * lockBreakDistance;
    }

    /// <summary>
    /// Enable input actions and subscribe to Input System events.
    /// </summary>
    private void OnEnable()
    {
        // Enable both the Player and UI action maps
        _controls.Player.Enable();
        _controls.UI.Enable();

        // Cache InventoryManager reference
        if (!TryGetInventoryManager(out _inventoryManager))
        {
            Debug.LogWarning("PlayerController: InventoryManager not found. Inventory switching will not work.");
        }

        // Movement input caching
        _controls.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
        _controls.Player.Move.canceled += ctx => _moveInput = Vector2.zero;

        // Combat - Subscribe to attack and ability inputs
        _controls.Player.LightAttack.started += OnLightAttackStarted;
        _controls.Player.LightAttack.canceled += OnLightAttackCanceled;
        _controls.Player.HeavyAttack.started += OnHeavyAttackStarted;
        _controls.Player.Block.started += OnBlockInput;
        _controls.Player.Block.canceled += OnBlockInput;
        _controls.Player.KIButton.started += OnKIInput;
        _controls.Player.Acrobatics.started += OnAcrobatics;

        // Inventory Switching - Using cached reference
        if (_inventoryManager != null)
        {
            _controls.UI.SwitchWeapons.started += ctx => _inventoryManager.CycleWeapon();
            _controls.UI.SwitchItems.started += ctx => _inventoryManager.CycleItem();
        }
    }

    private void OnDisable()
    {
        _controls.Player.Disable();
        _controls.UI.Disable();
    }

    private void Update()
    {
        if (_isHoldingAttack)
            _attackHoldTimer += Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (_health != null && _health.IsDead) return;
        if (_movement == null || !_movement.enabled) return;
        
        // Don't process movement input if in a clinch
        if (_clinchHandler != null && _clinchHandler.IsClinching)
        {
            _movement.ProcessMovement(Vector3.zero);
            return;
        }

        float moveSqrMagnitude = _moveInput.sqrMagnitude;
        bool isMoving = moveSqrMagnitude > _moveThresholdSqr;

        // Update camera yaw only when movement state changes
        if (isMoving != _wasMovingLastFrame)
        {
            _cachedCameraYaw = GetCurrentCameraYaw();
            _wasMovingLastFrame = isMoving;
        }

        // Convert input to camera-relative direction
        Vector3 moveDir = GetCameraRelativeDirection(_moveInput);

        // Cache position for multiple distance checks
        Vector3 currentPosition = transform.position;

        // Lock-On Logic: Distance Check (using squared distance to avoid sqrt)
        if (_currentTarget != null)
        {
            Vector3 targetPos = _currentTarget.GetLockOnPoint().position;
            float distSqr = (currentPosition - targetPos).sqrMagnitude;
            
            if (distSqr > _lockBreakDistanceSqr || !_currentTarget.IsValidTarget())
            {
                _currentTarget = null;
            }
        }

        // Drive the MovementComponent - all movement and rotation logic is delegated to MovementComponent
        if (_currentTarget != null)
        {
            _movement.ProcessMovement(moveDir, _currentTarget.GetLockOnPoint().position);
        }
        else
        {
            if (_combat != null && _combat.IsAttacking)
            {
                // During attacks, conditionally allow rotation based on combat state
                if (_combat.CanRotateDuringAttack && isMoving)
                {
                    // Temporarily enable rotation and let MovementComponent handle it
                    bool prevCanRotate = _movement.canRotate;
                    _movement.canRotate = true;
                    _movement.ProcessMovement(Vector3.zero);
                    _movement.RotateTowardsDirection(moveDir);
                    _movement.canRotate = prevCanRotate;
                }
                else
                {
                    _movement.ProcessMovement(Vector3.zero);
                }
            }
            else
            {
                // Normal movement - MovementComponent handles everything
                _movement.ProcessMovement(moveDir);
            }
        }
    }

    private static readonly Collider[] _overlapResults = new Collider[16];

    private void TryLockOn()
    {
        if (_currentTarget != null) return;

        // Don't lock on during clinch - player is already locked to grabbed enemy
        ClinchHandler clinch = GetComponent<ClinchHandler>();
        if (clinch != null && clinch.IsClinching) return;

        int count = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, _overlapResults);
        for (int i = 0; i < count; i++)
        {
            var col = _overlapResults[i];
            if (col != null && col.TryGetComponent<ITargetable>(out var target))
            {
                if (target.GetFaction() == Faction.Enemy && target.IsValidTarget())
                {
                    _currentTarget = target;
                    break;
                }
            }
        }
    }

    private float GetCurrentCameraYaw()
    {
        if (CameraZoneManager.Instance != null && CameraZoneManager.Instance.GetCurrentCamera() != null)
        {
            return CameraZoneManager.Instance.GetCurrentCamera().transform.eulerAngles.y;
        }
        return _cameraTransform.eulerAngles.y;
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        Quaternion yRotation = Quaternion.Euler(0f, _cachedCameraYaw, 0f);
        Vector3 camForward = yRotation * Vector3.forward;
        Vector3 camRight = yRotation * Vector3.right;
        return (camForward * input.y + camRight * input.x).normalized;
    }

    private bool TryGetInventoryManager(out InventoryManager manager)
    {
        manager = MasterSingleton.Instance != null ? MasterSingleton.Instance.InventoryManager : null;
        return manager != null;
    }

    // --- Combat Callbacks ---

    private void OnLightAttackStarted(InputAction.CallbackContext _)
    {
        TryLockOn();
        _combat.StartCharging(); // Handler takes care of the clock
    }

    private void OnLightAttackCanceled(InputAction.CallbackContext _)
    {
        _combat.ReleaseCharge(); // Handler decides which move to play
    }

    private void OnHeavyAttackStarted(InputAction.CallbackContext _)
    {
        TryLockOn();
        _isHoldingAttack = false;

        _combat.ExecuteHeavyAttack();        
    }

    private void OnBlockInput(InputAction.CallbackContext context) => _combat.SetBlocking(context.started);

    private void OnAcrobatics(InputAction.CallbackContext context)
    {
        if (!context.started) return;
               
        if (_movement != null && _moveInput.sqrMagnitude > _moveThresholdSqr)
        {
            Vector3 dir = GetCameraRelativeDirection(_moveInput);
            _movement.RotateTowardsDirection(dir);
        }
        
        if (_combat != null)
            _combat.ExecuteAcrobatics();
    }

    public void OnKIInput(InputAction.CallbackContext _) => _combat.HandleKIInput();
}