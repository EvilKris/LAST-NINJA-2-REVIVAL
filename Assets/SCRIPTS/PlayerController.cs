using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Main player controller that handles input from Unity's new Input System.
/// Coordinates between movement, combat, and the global Inventory system via MasterSingleton.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    // Component references
    private CombatHandler _combat;
    private PlayerControls _controls; // Auto-generated Input Actions class
    private MovementComponent _movement;
    private HealthComponent _health;
    private InventoryManager _inventoryManager;

    // Attack hold mechanic (hold for medium attack, tap for light)
    private float _attackHoldTimer;
    private bool _isHoldingAttack;

    // Camera and world interaction
    private Camera _mainCamera;
    private Transform _cameraTransform;
    private int _floorLayerMask;

    // Cached input values from Input System events
    private Vector2 _moveInput;
    private float _cachedCameraYaw; // Camera rotation locked while moving
    private bool _wasMovingLastFrame; // Track movement state changes

    private ITargetable _currentTarget; // Our current focus
    [SerializeField] private float lockBreakDistance = 1.0f;
    [SerializeField] private float searchRadius = 3.5f;
    
    // Cached squared distances for performance
    private float _lockBreakDistanceSqr;
    private float _moveThresholdSqr = 0.0001f;

    /// <summary>
    /// Initialize component references and create Input System instance.
    /// </summary>
    private void Awake()
    {
        _movement = GetComponent<MovementComponent>();
        _health = GetComponent<HealthComponent>();
        _combat = GetComponent<CombatHandler>();
        _mainCamera = Camera.main;
        _cameraTransform = _mainCamera.transform;
        _floorLayerMask = LayerMask.GetMask("Floor");

        _controls = new PlayerControls();
        
        // Cache squared distance to avoid sqrt operations in FixedUpdate
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

        // Drive the MovementComponent
        if (_currentTarget != null)
        {
            _movement.ProcessMovement(moveDir, _currentTarget.GetLockOnPoint().position);
        }
        else
        {
            if (_combat.IsAttacking)
            {
                if (_combat.CanRotateDuringAttack && isMoving)
                    _movement.RotateTowardsDirection(moveDir);

                _movement.ProcessMovement(Vector3.zero);
            }
            else if (isMoving)
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

    private void TryLockOn()
    {
        if (_currentTarget != null) return;

        Collider[] enemies = Physics.OverlapSphere(transform.position, searchRadius);
        foreach (var col in enemies)
        {
            if (col.TryGetComponent<ITargetable>(out var target))
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
        manager = MasterSingleton.Instance?.InventoryManager;
        return manager != null;
    }

    // --- Combat Callbacks ---

    private void OnLightAttackStarted(InputAction.CallbackContext _)
    {
        TryLockOn();
        _isHoldingAttack = true;
        _attackHoldTimer = 0f;
    }

    private void OnLightAttackCanceled(InputAction.CallbackContext _)
    {
        _isHoldingAttack = false;
        if (_attackHoldTimer >= 1.0f)
            _combat.ExecuteMediumAttack();
        else
            _combat.ExecuteLightAttack();
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
        if (_moveInput.sqrMagnitude > _moveThresholdSqr)
        {
            Vector3 dir = GetCameraRelativeDirection(_moveInput);
            _movement.RotateTowardsDirection(dir);
        }
        _combat.ExecuteAcrobatics();
    }

    public void OnKIInput(InputAction.CallbackContext _) => _combat.HandleKIInput();
}